// =============================================================================
// File   : ReservationLookupService.cs
// Purpose: Production implementation of IReservationLookupService.
//          Queries MongoDB Reservations collection to check whether a station
//          has any active (Pending or Approved) reservations that block deactivation.
// Author : Member 3
// Date   : 2026-09-29
// =============================================================================
using MongoDB.Driver;
using SolarGridX.Api.Infrastructure;
using SolarGridX.Api.Models;
using SolarGridX.Api.Models.Enums;

namespace SolarGridX.Api.Integrations;

public sealed class ReservationLookupService(MongoContext db) : IReservationLookupService
{
    private static readonly ReservationStatus[] ActiveStatuses =
    [
        ReservationStatus.Pending,
        ReservationStatus.Approved,
    ];

    public async Task<bool> HasActiveReservationsAsync(string stationId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(stationId))
            return false;

        var normalizedStationId = stationId.Trim().ToUpperInvariant();
        var filter = Builders<EnergyReservation>.Filter.Eq(x => x.StationId, normalizedStationId)
                     & Builders<EnergyReservation>.Filter.In(x => x.Status, ActiveStatuses);

        return await db.Reservations.Find(filter).AnyAsync(ct);
    }
}
