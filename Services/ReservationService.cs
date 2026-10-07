// Smart Solar Microgrid Trading System - reservation, capacity and QR transaction workflows.
using System.Security.Cryptography;
using System.Text;
using MongoDB.Driver;
using SolarGridX.Api.Common;
using SolarGridX.Api.DTOs.Requests;
using SolarGridX.Api.DTOs.Responses;
using SolarGridX.Api.Infrastructure;
using SolarGridX.Api.Models;
using SolarGridX.Api.Models.Enums;
using SolarGridX.Api.Utilities;

namespace SolarGridX.Api.Services;

public sealed class ReservationService(
    MongoContext db,
    StationSlotService stations,
    UserService users,
    ILogger<ReservationService> logger
)
{
    private static readonly ReservationStatus[] ActiveStatuses =
    [
        ReservationStatus.Pending,
        ReservationStatus.Approved,
    ];

    // Creates a pending reservation after atomically deducting available slot capacity.
    public async Task<ReservationResponse> CreateAsync(
        string callerId,
        CreateReservationRequest request,
        CancellationToken ct
    )
    {
        var prosumer = await users.FindByIdAsync(callerId, ct);
        if (prosumer.Role != UserRole.Prosumer || prosumer.AccountStatus != AccountStatus.Active)
            throw new ApiException(403, "Only active Prosumer profiles may create reservations.");

        var station = await stations.FindStationAsync(request.StationId, ct);
        if (station.Status != StationStatus.Active)
            throw new ApiException(409, "Station is not active.");
        var slot = await stations.FindSlotAsync(request.SlotId, ct);
        ValidateBookingTarget(station, slot, request.RequestedCapacity);
        ValidateSevenDayWindow(slot.StartTime);

        var update = Builders<EnergyBookingSlot>
            .Update.Inc(x => x.AvailableCapacity, -request.RequestedCapacity)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);
        var capacityFilter =
            Builders<EnergyBookingSlot>.Filter.Eq(x => x.Id, slot.Id)
            & Builders<EnergyBookingSlot>.Filter.Eq(x => x.Status, SlotStatus.Active)
            & Builders<EnergyBookingSlot>.Filter.Gte(
                x => x.AvailableCapacity,
                request.RequestedCapacity
            );
        if (
            (
                await db.Slots.UpdateOneAsync(capacityFilter, update, cancellationToken: ct)
            ).ModifiedCount != 1
        )
            throw new ApiException(409, "Requested capacity is no longer available.");
        var reservation = new EnergyReservation
        {
            ReservationId = $"RSV-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
            ProsumerUserId = prosumer.Id,
            ProsumerNic = prosumer.Nic!,
            StationId = station.StationId,
            SlotId = slot.SlotId,
            ScheduledStartTime = slot.StartTime,
            ScheduledEndTime = slot.EndTime,
            RequestedCapacity = request.RequestedCapacity,
            TransferType = string.IsNullOrWhiteSpace(request.TransferType) ? "DropOff" : request.TransferType.Trim(),
            Notes = request.Notes?.Trim(),
        };
        try
        {
            await db.Reservations.InsertOneAsync(reservation, cancellationToken: ct);
        }
        catch
        {
            await ReleaseCapacityAsync(slot.Id, request.RequestedCapacity, ct);
            throw;
        }
        logger.LogInformation(
            "Reservation {ReservationId} created for Prosumer {ProsumerId}",
            reservation.ReservationId,
            callerId
        );
        return reservation.ToResponse();
    }

    // Retrieves a reservation after enforcing Prosumer resource ownership.
    public async Task<ReservationResponse> GetAsync(
        string reservationId,
        string callerId,
        UserRole role,
        CancellationToken ct
    )
    {
        var reservation = await FindAsync(reservationId, ct);
        EnsureAccess(reservation, callerId, role);
        return reservation.ToResponse();
    }

    // Lists role-scoped reservations with optional authorized filters and status grouping.
    public async Task<List<ReservationResponse>> ListAsync(
        string callerId,
        UserRole role,
        string? group,
        ReservationStatus? status,
        string? stationId,
        string? nic,
        DateTime? from,
        DateTime? to,
        CancellationToken ct
    )
    {
        var filter = Builders<EnergyReservation>.Filter.Empty;
        if (role == UserRole.Prosumer)
        {
            if (!string.IsNullOrWhiteSpace(nic))
            {
                var prosumer = await users.FindByIdAsync(callerId, ct);
                if (!string.Equals(prosumer.Nic, nic.Trim(), StringComparison.OrdinalIgnoreCase))
                    throw new ApiException(403, "You may only access your own reservations.");
            }
            filter &= Builders<EnergyReservation>.Filter.Eq(x => x.ProsumerUserId, callerId);
        }
        else if (!string.IsNullOrWhiteSpace(nic))
            filter &= Builders<EnergyReservation>.Filter.Eq(
                x => x.ProsumerNic,
                nic.Trim().ToUpperInvariant()
            );
        if (!string.IsNullOrWhiteSpace(stationId))
            filter &= Builders<EnergyReservation>.Filter.Eq(
                x => x.StationId,
                stationId.Trim().ToUpperInvariant()
            );
        if (status.HasValue)
            filter &= Builders<EnergyReservation>.Filter.Eq(x => x.Status, status.Value);
        if (from.HasValue)
            filter &= Builders<EnergyReservation>.Filter.Gte(
                x => x.ScheduledStartTime,
                ToUtc(from.Value)
            );
        if (to.HasValue)
            filter &= Builders<EnergyReservation>.Filter.Lte(
                x => x.ScheduledStartTime,
                ToUtc(to.Value)
            );
        if (!string.IsNullOrWhiteSpace(group))
            filter &= group.ToLowerInvariant() switch
            {
                "pending" => Builders<EnergyReservation>.Filter.Eq(
                    x => x.Status,
                    ReservationStatus.Pending
                ),
                "current" => Builders<EnergyReservation>.Filter.In(x => x.Status, ActiveStatuses)
                    & Builders<EnergyReservation>.Filter.Gte(
                        x => x.ScheduledEndTime,
                        DateTime.UtcNow
                    ),
                "history" => Builders<EnergyReservation>.Filter.In(
                    x => x.Status,
                    [
                        ReservationStatus.Completed,
                        ReservationStatus.Cancelled,
                        ReservationStatus.Rejected,
                        ReservationStatus.Expired,
                    ]
                ),
                _ => throw new ApiException(400, "Unsupported reservation group."),
            };
        return (
            await db
                .Reservations.Find(filter)
                .SortByDescending(x => x.ScheduledStartTime)
                .ToListAsync(ct)
        )
            .Select(x => x.ToResponse())
            .ToList();
    }

    // Reassigns a reservation after notice, state, target and capacity checks.
    public async Task<ReservationResponse> UpdateAsync(
        string reservationId,
        string callerId,
        UserRole role,
        UpdateReservationRequest request,
        CancellationToken ct
    )
    {
        var reservation = await FindAsync(reservationId, ct);
        EnsureAccess(reservation, callerId, role);
        EnsureModifiable(reservation);
        var station = await stations.FindStationAsync(request.StationId, ct);
        if (station.Status != StationStatus.Active)
            throw new ApiException(409, "Station is not active.");
        var nextSlot = await stations.FindSlotAsync(request.SlotId, ct);
        ValidateBookingTarget(station, nextSlot, request.RequestedCapacity);
        ValidateSevenDayWindow(nextSlot.StartTime);

        if (reservation.SlotId == nextSlot.SlotId)
        {
            if (reservation.RequestedCapacity == request.RequestedCapacity)
                return reservation.ToResponse();

            var delta = request.RequestedCapacity - reservation.RequestedCapacity;
            if (delta > 0)
            {
                var capacityFilter =
                    Builders<EnergyBookingSlot>.Filter.Eq(x => x.Id, nextSlot.Id)
                    & Builders<EnergyBookingSlot>.Filter.Eq(x => x.Status, SlotStatus.Active)
                    & Builders<EnergyBookingSlot>.Filter.Gte(x => x.AvailableCapacity, delta);

                var update = Builders<EnergyBookingSlot>
                    .Update.Inc(x => x.AvailableCapacity, -delta)
                    .Set(x => x.UpdatedAt, DateTime.UtcNow);

                if (
                    (
                        await db.Slots.UpdateOneAsync(
                            capacityFilter,
                            update,
                            cancellationToken: ct
                        )
                    ).ModifiedCount != 1
                )
                    throw new ApiException(409, "Requested capacity is no longer available.");
            }
            else
            {
                await ReleaseCapacityAsync(nextSlot.Id, -delta, ct);
            }

            reservation.StationId = station.StationId;
            reservation.RequestedCapacity = request.RequestedCapacity;
            reservation.UpdatedAt = DateTime.UtcNow;
            await db.Reservations.ReplaceOneAsync(
                x => x.Id == reservation.Id && x.Status == reservation.Status,
                reservation,
                cancellationToken: ct
            );
            return reservation.ToResponse();
        }

        var reserveFilter =
            Builders<EnergyBookingSlot>.Filter.Eq(x => x.Id, nextSlot.Id)
            & Builders<EnergyBookingSlot>.Filter.Eq(x => x.Status, SlotStatus.Active)
            & Builders<EnergyBookingSlot>.Filter.Gte(
                x => x.AvailableCapacity,
                request.RequestedCapacity
            );
        if (
            (
                await db.Slots.UpdateOneAsync(
                    reserveFilter,
                    Builders<EnergyBookingSlot>
                        .Update.Inc(x => x.AvailableCapacity, -request.RequestedCapacity)
                        .Set(x => x.UpdatedAt, DateTime.UtcNow),
                    cancellationToken: ct
                )
            ).ModifiedCount != 1
        )
            throw new ApiException(409, "Requested capacity is no longer available.");
        try
        {
            await ReleaseCapacityAsync(
                (await stations.FindSlotAsync(reservation.SlotId, ct)).Id,
                reservation.RequestedCapacity,
                ct
            );
            reservation.StationId = station.StationId;
            reservation.SlotId = nextSlot.SlotId;
            reservation.ScheduledStartTime = nextSlot.StartTime;
            reservation.ScheduledEndTime = nextSlot.EndTime;
            reservation.RequestedCapacity = request.RequestedCapacity;
            if (reservation.Transaction != null)
            {
                reservation.Transaction.ExpiresAt = nextSlot.EndTime.AddHours(1);
            }
            reservation.UpdatedAt = DateTime.UtcNow;
            await db.Reservations.ReplaceOneAsync(
                x => x.Id == reservation.Id && x.Status == reservation.Status,
                reservation,
                cancellationToken: ct
            );
            return reservation.ToResponse();
        }
        catch
        {
            await ReleaseCapacityAsync(nextSlot.Id, request.RequestedCapacity, ct);
            throw;
        }
    }

    // Cancels an active reservation once and returns its reserved capacity to the slot.
    public async Task CancelAsync(
        string reservationId,
        string callerId,
        UserRole role,
        CancellationToken ct
    )
    {
        var reservation = await FindAsync(reservationId, ct);
        EnsureAccess(reservation, callerId, role);
        EnsureCancellable(reservation);
        var filter =
            Builders<EnergyReservation>.Filter.Eq(x => x.Id, reservation.Id)
            & Builders<EnergyReservation>.Filter.In(x => x.Status, ActiveStatuses);
        var update = Builders<EnergyReservation>
            .Update.Set(x => x.Status, ReservationStatus.Cancelled)
            .Set(x => x.CancelledAt, DateTime.UtcNow)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);
        if (
            (
                await db.Reservations.UpdateOneAsync(filter, update, cancellationToken: ct)
            ).ModifiedCount != 1
        )
            throw new ApiException(409, "Reservation state changed; refresh and retry.");
        await ReleaseCapacityAsync(
            (await stations.FindSlotAsync(reservation.SlotId, ct)).Id,
            reservation.RequestedCapacity,
            ct
        );
        logger.LogInformation("Reservation {ReservationId} cancelled", reservation.ReservationId);
    }

    // Approves a pending reservation and creates an opaque, server-verifiable transaction token.
    public async Task<ReservationResponse> ApproveAsync(
        string reservationId,
        string? callerId = null,
        CancellationToken ct = default
    )
    {
        var reservation = await FindAsync(reservationId, ct);
        if (reservation.Status != ReservationStatus.Pending)
            throw new ApiException(409, "Only pending reservations can be approved.");
        var token = Convert
            .ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        reservation.Status = ReservationStatus.Approved;
        reservation.ApprovedAt = DateTime.UtcNow;
        reservation.ApprovedByUserId = callerId;
        reservation.UpdatedAt = DateTime.UtcNow;
        reservation.Transaction = new TransactionInfo
        {
            TransactionId = $"TXN-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
            TokenHash = HashToken(token),
            ExpiresAt = reservation.ScheduledEndTime.AddHours(1),
        };
        if (
            (
                await db.Reservations.ReplaceOneAsync(
                    x => x.Id == reservation.Id && x.Status == ReservationStatus.Pending,
                    reservation,
                    cancellationToken: ct
                )
            ).ModifiedCount != 1
        )
            throw new ApiException(409, "Reservation state changed; refresh and retry.");
        // The caller receives the opaque token once through a separate retrieval endpoint in a production flow; this API returns metadata only.
        logger.LogInformation(
            "Reservation {ReservationId} approved with transaction {TransactionId}",
            reservation.ReservationId,
            reservation.Transaction.TransactionId
        );
        return reservation.ToResponse();
    }

    // Issues a fresh opaque QR token to the owning Prosumer without persisting the raw token.
    public async Task<QrTokenResponse> IssueQrTokenAsync(
        string reservationId,
        string callerId,
        CancellationToken ct
    )
    {
        var reservation = await FindAsync(reservationId, ct);
        EnsureAccess(reservation, callerId, UserRole.Prosumer);
        if (
            reservation.Status != ReservationStatus.Approved
            || reservation.Transaction is null
            || reservation.Transaction.Status != TransactionStatus.Active
        )
            throw new ApiException(409, "Reservation does not have an active transaction.");
        if (reservation.Transaction.ExpiresAt < DateTime.UtcNow)
            throw new ApiException(409, "Transaction has expired.");
        var token = Convert
            .ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var update = Builders<EnergyReservation>
            .Update.Set("Transaction.TokenHash", HashToken(token))
            .Set(x => x.UpdatedAt, DateTime.UtcNow);
        await db.Reservations.UpdateOneAsync(
            x => x.Id == reservation.Id && x.Status == ReservationStatus.Approved,
            update,
            cancellationToken: ct
        );
        return new QrTokenResponse(
            reservation.Transaction.TransactionId,
            token,
            reservation.Transaction.ExpiresAt
        );
    }

    // Rejects a pending reservation and releases its capacity exactly once.
    public async Task RejectAsync(string reservationId, CancellationToken ct)
    {
        var reservation = await FindAsync(reservationId, ct);
        if (reservation.Status != ReservationStatus.Pending)
            throw new ApiException(409, "Only pending reservations can be rejected.");
        if (
            (
                await db.Reservations.UpdateOneAsync(
                    x => x.Id == reservation.Id && x.Status == ReservationStatus.Pending,
                    Builders<EnergyReservation>
                        .Update.Set(x => x.Status, ReservationStatus.Rejected)
                        .Set(x => x.UpdatedAt, DateTime.UtcNow),
                    cancellationToken: ct
                )
            ).ModifiedCount != 1
        )
            throw new ApiException(409, "Reservation state changed; refresh and retry.");
        await ReleaseCapacityAsync(
            (await stations.FindSlotAsync(reservation.SlotId, ct)).Id,
            reservation.RequestedCapacity,
            ct
        );
    }

    // Verifies an opaque QR token and provides minimal operational details to a Grid Operator.
    public async Task<TransactionResponse> VerifyTransactionAsync(
        VerifyTransactionRequest request,
        CancellationToken ct
    )
    {
        var hash = HashToken(request.Token);
        var reservation =
            await db
                .Reservations.Find(x => x.Transaction != null && x.Transaction.TokenHash == hash)
                .FirstOrDefaultAsync(ct)
            ?? throw new ApiException(404, "Invalid QR Code");
        var transaction = reservation.Transaction!;

        if (reservation.Status == ReservationStatus.Cancelled)
            throw new ApiException(409, "Reservation Cancelled");

        if (reservation.Status == ReservationStatus.Completed || transaction.Status == TransactionStatus.Completed)
            throw new ApiException(409, "Reservation Already Completed");

        if (reservation.Status != ReservationStatus.Approved)
            throw new ApiException(409, "Reservation Is Not Approved");

        if (transaction.ExpiresAt < DateTime.UtcNow)
            throw new ApiException(409, "Reservation QR pass has expired");

        if (!string.IsNullOrWhiteSpace(request.StationId) &&
            !string.Equals(request.StationId.Trim(), reservation.StationId, StringComparison.OrdinalIgnoreCase))
            throw new ApiException(409, "Reservation is for a different microgrid node");

        if (transaction.Status != TransactionStatus.Active)
            throw new ApiException(409, "Transaction is not eligible for completion.");

        var prosumer = await users.FindByIdAsync(reservation.ProsumerUserId, ct);
        var prosumerName = prosumer != null ? $"{prosumer.FirstName} {prosumer.LastName}".Trim() : null;
        var station = await db.Stations.Find(x => x.StationId == reservation.StationId).FirstOrDefaultAsync(ct);
        var stationName = station?.Name;

        return new TransactionResponse(
            transaction.TransactionId,
            reservation.ReservationId,
            reservation.StationId,
            reservation.ScheduledStartTime,
            reservation.ScheduledEndTime,
            reservation.RequestedCapacity,
            transaction.Status,
            transaction.ExpiresAt,
            ProsumerName: prosumerName,
            ProsumerNic: reservation.ProsumerNic,
            StationName: stationName,
            TransferType: reservation.TransferType,
            SlotId: reservation.SlotId
        );
    }

    // Completes an approved transaction once with a conditional state transition for idempotency safety.
    public async Task<TransactionResponse> CompleteTransactionAsync(
        string transactionId,
        string operatorId,
        CancellationToken ct
    )
    {
        var reservation =
            await db
                .Reservations.Find(x =>
                    x.Transaction != null
                    && x.Transaction.TransactionId == transactionId.Trim().ToUpperInvariant()
                )
                .FirstOrDefaultAsync(ct)
            ?? throw new ApiException(404, "Transaction was not found.");
        var transaction = reservation.Transaction!;
        if (transaction.ExpiresAt < DateTime.UtcNow)
            throw new ApiException(409, "Transaction has expired.");
        var filter =
            Builders<EnergyReservation>.Filter.Eq(x => x.Id, reservation.Id)
            & Builders<EnergyReservation>.Filter.Eq(x => x.Status, ReservationStatus.Approved)
            & Builders<EnergyReservation>.Filter.Eq("Transaction.Status", TransactionStatus.Active);
        var now = DateTime.UtcNow;
        var update = Builders<EnergyReservation>
            .Update.Set(x => x.Status, ReservationStatus.Completed)
            .Set(x => x.CompletedAt, now)
            .Set(x => x.CompletedByUserId, operatorId)
            .Set(x => x.UpdatedAt, now)
            .Set("Transaction.Status", TransactionStatus.Completed)
            .Set("Transaction.CompletedAt", now)
            .Set("Transaction.CompletedByUserId", operatorId);
        if (
            (
                await db.Reservations.UpdateOneAsync(filter, update, cancellationToken: ct)
            ).ModifiedCount != 1
        )
            throw new ApiException(
                409,
                "Transaction has already been completed or is not eligible."
            );

        // Record TransactionLog audit entry
        var log = new TransactionLog
        {
            TransactionId = transaction.TransactionId,
            ReservationId = reservation.ReservationId,
            StationId = reservation.StationId,
            ProsumerNic = reservation.ProsumerNic,
            TransferredCapacity = reservation.RequestedCapacity,
            TransferType = reservation.TransferType ?? "DropOff",
            OperatorUserId = operatorId,
            CompletedAt = now,
        };
        await db.TransactionLogs.InsertOneAsync(log, cancellationToken: ct);

        logger.LogInformation(
            "Transaction {TransactionId} completed by operator {OperatorId}",
            transactionId,
            operatorId
        );

        var prosumer = await users.FindByIdAsync(reservation.ProsumerUserId, ct);
        var prosumerName = prosumer != null ? $"{prosumer.FirstName} {prosumer.LastName}".Trim() : null;
        var station = await db.Stations.Find(x => x.StationId == reservation.StationId).FirstOrDefaultAsync(ct);
        var stationName = station?.Name;

        return new TransactionResponse(
            transaction.TransactionId,
            reservation.ReservationId,
            reservation.StationId,
            reservation.ScheduledStartTime,
            reservation.ScheduledEndTime,
            reservation.RequestedCapacity,
            TransactionStatus.Completed,
            transaction.ExpiresAt,
            ProsumerName: prosumerName,
            ProsumerNic: reservation.ProsumerNic,
            StationName: stationName,
            TransferType: reservation.TransferType,
            SlotId: reservation.SlotId
        );
    }

    // Calculates role-scoped dashboard counts from live MongoDB reservation data.
    public async Task<DashboardSummaryResponse> SummaryAsync(
        string callerId,
        UserRole role,
        CancellationToken ct
    )
    {
        var baseFilter = role == UserRole.Prosumer
            ? Builders<EnergyReservation>.Filter.Eq(x => x.ProsumerUserId, callerId)
            : Builders<EnergyReservation>.Filter.Empty;
        async Task<int> Count(FilterDefinition<EnergyReservation> extra) =>
            (int)
                await db.Reservations.CountDocumentsAsync(
                    baseFilter & extra,
                    cancellationToken: ct
                );
        var now = DateTime.UtcNow;
        return new DashboardSummaryResponse(
            await Count(
                Builders<EnergyReservation>.Filter.Eq(x => x.Status, ReservationStatus.Pending)
            ),
            await Count(
                Builders<EnergyReservation>.Filter.Eq(x => x.Status, ReservationStatus.Approved)
                    & Builders<EnergyReservation>.Filter.Gt(x => x.ScheduledStartTime, now)
            ),
            await Count(
                Builders<EnergyReservation>.Filter.In(x => x.Status, ActiveStatuses)
                    & Builders<EnergyReservation>.Filter.Gte(x => x.ScheduledEndTime, now)
            ),
            await Count(
                Builders<EnergyReservation>.Filter.Eq(x => x.Status, ReservationStatus.Completed)
            )
        );
    }

    // Finds a reservation from its public business identifier.
    private async Task<EnergyReservation> FindAsync(string id, CancellationToken ct) =>
        await db
            .Reservations.Find(x => x.ReservationId == id.Trim().ToUpperInvariant())
            .FirstOrDefaultAsync(ct)
        ?? throw new ApiException(404, "Reservation was not found.");

    // Enforces Prosumer ownership while staff roles retain operational access.
    private static void EnsureAccess(EnergyReservation reservation, string callerId, UserRole role)
    {
        if (role == UserRole.Prosumer && reservation.ProsumerUserId != callerId)
            throw new ApiException(403, "You may only access your own reservations.");
    }

    // Enforces the twelve-hour modification notice and valid editable state.
    internal static void EnsureModifiable(EnergyReservation reservation)
    {
        if (!ActiveStatuses.Contains(reservation.Status))
            throw new ApiException(409, "Reservation cannot be updated in its current state.");
        if (reservation.ScheduledStartTime - DateTime.UtcNow < TimeSpan.FromHours(12))
            throw new ApiException(409, "Reservations require at least 12 hours notice to update.");
    }

    // Enforces the twelve-hour cancellation notice and valid cancellable state.
    internal static void EnsureCancellable(EnergyReservation reservation)
    {
        if (!ActiveStatuses.Contains(reservation.Status))
            throw new ApiException(409, "Reservation cannot be cancelled in its current state.");
        if (reservation.ScheduledStartTime - DateTime.UtcNow < TimeSpan.FromHours(12))
            throw new ApiException(409, "Reservations require at least 12 hours notice to cancel.");
    }

    // Enforces the mandatory future and seven-day reservation horizon using UTC server time.
    internal static void ValidateSevenDayWindow(DateTime start)
    {
        start = ToUtc(start);
        var now = DateTime.UtcNow;
        if (start <= now)
            throw new ApiException(422, "Reservation time must be in the future.");
        if (start > now.AddDays(7))
            throw new ApiException(422, "Reservations must start within the next 7 days.");
    }

    // Verifies consistent station/slot association and a positive requested capacity.
    internal static void ValidateBookingTarget(
        SolarStation station,
        EnergyBookingSlot slot,
        decimal requested
    )
    {
        if (slot.StationId != station.StationId)
            throw new ApiException(422, "Slot does not belong to the supplied station.");
        if (slot.Status != SlotStatus.Active)
            throw new ApiException(409, "Slot is not active.");
        if (requested <= 0)
            throw new ApiException(422, "Requested capacity must be positive.");
    }

    // Restores capacity after a state transition or compensates an unsuccessful multi-document workflow.
    private async Task ReleaseCapacityAsync(string slotId, decimal amount, CancellationToken ct) =>
        await db.Slots.UpdateOneAsync(
            x => x.Id == slotId,
            Builders<EnergyBookingSlot>
                .Update.Inc(x => x.AvailableCapacity, amount)
                .Set(x => x.UpdatedAt, DateTime.UtcNow),
            cancellationToken: ct
        );

    // Hashes opaque QR tokens before persistence so raw tokens cannot be replayed from the database.
    internal static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    // Normalizes date inputs to UTC before applying server-side business time calculations.
    private static DateTime ToUtc(DateTime value) =>
        TimeHelper.ToUtc(value);
}
