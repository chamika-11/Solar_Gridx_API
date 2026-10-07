// Smart Solar Microgrid Trading System - station and booking-slot business workflows.
using MongoDB.Driver;
using SolarGridX.Api.Common;
using SolarGridX.Api.DTOs.Requests;
using SolarGridX.Api.DTOs.Responses;
using SolarGridX.Api.Infrastructure;
using SolarGridX.Api.Models;
using SolarGridX.Api.Models.Enums;

namespace SolarGridX.Api.Services;

public sealed class StationSlotService(MongoContext db)
{
    // Creates a slot only for an active station and initializes its available capacity to full capacity.
    public async Task<SlotResponse> CreateSlotAsync(CreateSlotRequest request, CancellationToken ct)
    {
        var station = await FindStationAsync(request.StationId, ct);
        if (station.Status != StationStatus.Active)
            throw new ApiException(409, "Slots cannot be created for a deactivated station.");
        ValidateSlotTimes(request.StartTime, request.EndTime);
        var slot = new EnergyBookingSlot
        {
            SlotId = $"SLT-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
            StationId = station.StationId,
            StartTime = EnsureUtc(request.StartTime),
            EndTime = EnsureUtc(request.EndTime),
            Capacity = request.Capacity,
            AvailableCapacity = request.Capacity,
        };
        await db.Slots.InsertOneAsync(slot, cancellationToken: ct);
        return slot.ToResponse(station.Name);
    }

    // Creates recurring slots over a date range, matching selected days of the week and daily time slots.
    public async Task<BatchSlotCreationResponse> CreateRecurringSlotsAsync(
        CreateRecurringSlotsRequest request,
        CancellationToken ct
    )
    {
        var station = await FindStationAsync(request.StationId, ct);
        if (station.Status != StationStatus.Active)
            throw new ApiException(409, "Slots cannot be created for a deactivated station.");

        var startDate = request.StartDate.Date;
        var endDate = request.EndDate.Date;

        if (startDate > endDate)
            throw new ApiException(422, "Start date cannot be after end date.");

        if (request.TimeSlots == null || request.TimeSlots.Count == 0)
            throw new ApiException(422, "At least one daily time slot must be specified.");

        foreach (var ts in request.TimeSlots)
        {
            if (ts.StartTime >= ts.EndTime)
                throw new ApiException(422, $"Time slot start time ({ts.StartTime}) must be before end time ({ts.EndTime}).");
        }

        // Fetch existing active slots for this station to check for overlaps
        var existingSlots = await db.Slots
            .Find(x => x.StationId == station.StationId && x.Status == SlotStatus.Active)
            .ToListAsync(ct);

        var daysFilter = request.DaysOfWeek != null && request.DaysOfWeek.Count > 0
            ? new HashSet<DayOfWeek>(request.DaysOfWeek)
            : null;

        var slotsToInsert = new List<EnergyBookingSlot>();
        var messages = new List<string>();
        int totalSkipped = 0;

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if (daysFilter != null && !daysFilter.Contains(date.DayOfWeek))
                continue;

            foreach (var timeSlot in request.TimeSlots)
            {
                var startDateTime = EnsureUtc(date.Add(timeSlot.StartTime));
                var endDateTime = EnsureUtc(date.Add(timeSlot.EndTime));

                // Check for overlapping existing slots in DB or in current batch
                bool isOverlapDB = existingSlots.Any(s =>
                    s.StartTime < endDateTime && s.EndTime > startDateTime);

                bool isOverlapBatch = slotsToInsert.Any(s =>
                    s.StartTime < endDateTime && s.EndTime > startDateTime);

                if (isOverlapDB || isOverlapBatch)
                {
                    if (request.SkipExistingConflicts)
                    {
                        totalSkipped++;
                        messages.Add($"Skipped slot on {date:yyyy-MM-dd} ({timeSlot.StartTime:hh\\:mm}-{timeSlot.EndTime:hh\\:mm}) due to time conflict.");
                        continue;
                    }
                    else
                    {
                        throw new ApiException(409, $"Slot conflict detected on {date:yyyy-MM-dd} ({timeSlot.StartTime:hh\\:mm}-{timeSlot.EndTime:hh\\:mm}).");
                    }
                }

                var slot = new EnergyBookingSlot
                {
                    SlotId = $"SLT-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
                    StationId = station.StationId,
                    StartTime = startDateTime,
                    EndTime = endDateTime,
                    Capacity = request.Capacity,
                    AvailableCapacity = request.Capacity,
                    Status = SlotStatus.Active
                };
                slotsToInsert.Add(slot);
            }
        }

        if (slotsToInsert.Count > 0)
        {
            await db.Slots.InsertManyAsync(slotsToInsert, cancellationToken: ct);
        }

        var createdResponses = slotsToInsert.Select(s => s.ToResponse(station.Name)).ToList();
        return new BatchSlotCreationResponse(
            TotalCreated: createdResponses.Count,
            TotalSkipped: totalSkipped,
            CreatedSlots: createdResponses,
            Messages: messages
        );
    }

    // Returns slots for administration or active future slots for Prosumer booking.
    public async Task<List<SlotResponse>> GetSlotsAsync(
        string? stationId,
        bool availableOnly,
        CancellationToken ct
    )
    {
        var filter = Builders<EnergyBookingSlot>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(stationId))
            filter &= Builders<EnergyBookingSlot>.Filter.Eq(
                x => x.StationId,
                stationId.Trim().ToUpperInvariant()
            );
        if (availableOnly)
            filter &=
                Builders<EnergyBookingSlot>.Filter.Eq(x => x.Status, SlotStatus.Active)
                & Builders<EnergyBookingSlot>.Filter.Gt(x => x.AvailableCapacity, 0)
                & Builders<EnergyBookingSlot>.Filter.Gt(x => x.StartTime, DateTime.UtcNow);

        var slots = await db.Slots.Find(filter).SortBy(x => x.StartTime).ToListAsync(ct);
        var stationIds = slots.Select(s => s.StationId).Distinct().ToList();
        var stations = await db.Stations.Find(x => stationIds.Contains(x.StationId)).ToListAsync(ct);
        var stationNameMap = stations.ToDictionary(s => s.StationId, s => s.Name, StringComparer.OrdinalIgnoreCase);

        return slots
            .Select(x => x.ToResponse(stationNameMap.GetValueOrDefault(x.StationId)))
            .ToList();
    }

    // Retrieves a single slot by its generated business identifier.
    public async Task<SlotResponse> GetSlotAsync(string slotId, CancellationToken ct)
    {
        var slot = await FindSlotAsync(slotId, ct);
        var station = await db.Stations.Find(x => x.StationId == slot.StationId).FirstOrDefaultAsync(ct);
        return slot.ToResponse(station?.Name);
    }

    // Updates slot times/capacity while preventing capacity below already committed reservations.
    public async Task<SlotResponse> UpdateSlotAsync(
        string slotId,
        UpdateSlotRequest request,
        CancellationToken ct
    )
    {
        var slot = await FindSlotAsync(slotId, ct);
        var start = request.StartTime ?? slot.StartTime;
        var end = request.EndTime ?? slot.EndTime;
        ValidateSlotTimes(start, end);
        var capacity = request.Capacity ?? slot.Capacity;
        var reserved = slot.Capacity - slot.AvailableCapacity;
        if (capacity < reserved)
            throw new ApiException(
                409,
                "Capacity cannot be reduced below already reserved capacity."
            );
        slot.StartTime = EnsureUtc(start);
        slot.EndTime = EnsureUtc(end);
        slot.Capacity = capacity;
        slot.AvailableCapacity = request.AvailableCapacity ?? capacity - reserved;
        if (slot.AvailableCapacity < 0 || slot.AvailableCapacity > slot.Capacity - reserved)
            throw new ApiException(
                422,
                "Available capacity conflicts with committed reservations."
            );
        slot.UpdatedAt = DateTime.UtcNow;
        await db.Slots.ReplaceOneAsync(x => x.Id == slot.Id, slot, cancellationToken: ct);
        var station = await db.Stations.Find(x => x.StationId == slot.StationId).FirstOrDefaultAsync(ct);
        return slot.ToResponse(station?.Name);
    }

    // Deactivates a slot only where it has no capacity held by active reservations.
    public async Task DeactivateSlotAsync(string slotId, CancellationToken ct)
    {
        var slot = await FindSlotAsync(slotId, ct);
        var hasActiveReservations = await db.Reservations.Find(x =>
            x.SlotId == slot.SlotId &&
            (x.Status == ReservationStatus.Pending || x.Status == ReservationStatus.Approved)
        ).AnyAsync(ct);

        if (hasActiveReservations)
            throw new ApiException(
                409,
                "Slot cannot be deactivated while it has pending or confirmed reservations."
            );

        if (slot.AvailableCapacity != slot.Capacity)
            throw new ApiException(409, "Slot cannot be deactivated while capacity is reserved.");

        await db.Slots.UpdateOneAsync(
            x => x.Id == slot.Id,
            Builders<EnergyBookingSlot>
                .Update.Set(x => x.Status, SlotStatus.Deactivated)
                .Set(x => x.UpdatedAt, DateTime.UtcNow),
            cancellationToken: ct
        );
    }

    // Finds a station or standardizes the not-found response.
    public async Task<SolarStation> FindStationAsync(string stationId, CancellationToken ct) =>
        await db
            .Stations.Find(x => x.StationId == stationId.Trim().ToUpperInvariant())
            .FirstOrDefaultAsync(ct)
        ?? throw new ApiException(404, "Station was not found.");

    // Finds a slot or standardizes the not-found response.
    public async Task<EnergyBookingSlot> FindSlotAsync(string slotId, CancellationToken ct) =>
        await db
            .Slots.Find(x => x.SlotId == slotId.Trim().ToUpperInvariant())
            .FirstOrDefaultAsync(ct)
        ?? throw new ApiException(404, "Slot was not found.");

    // Validates temporal bounds for an energy availability interval.
    private static void ValidateSlotTimes(DateTime start, DateTime end)
    {
        if (EnsureUtc(start) >= EnsureUtc(end))
            throw new ApiException(422, "Slot end time must be after start time.");
    }

    // Interprets unspecified input as UTC at the HTTP boundary and stores UTC in MongoDB.
    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
