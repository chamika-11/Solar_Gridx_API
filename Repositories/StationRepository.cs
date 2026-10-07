// =============================================================================
// File   : StationRepository.cs
// Purpose: MongoDB implementation of IStationRepository.
//          Wraps MongoContext collection access; contains zero business logic.
//          All queries and index-supported operations live here.
// Author : Member 2
// Date   : 2026-09-21
// =============================================================================
using MongoDB.Driver;
using SolarGridX.Api.Infrastructure;
using SolarGridX.Api.Models;
using SolarGridX.Api.Models.Enums;

namespace SolarGridX.Api.Repositories;

public sealed class StationRepository(MongoContext db) : IStationRepository
{
    // Retrieves all stations, optionally restricting to active-only for Prosumer callers.
    public async Task<List<SolarStation>> GetAllAsync(bool activeOnly, CancellationToken ct)
    {
        var filter = activeOnly
            ? Builders<SolarStation>.Filter.Eq(x => x.Status, StationStatus.Active)
            : Builders<SolarStation>.Filter.Empty;

        return await db.Stations
            .Find(filter)
            .SortBy(x => x.Name)
            .ToListAsync(ct);
    }

    // Queries by the normalised StationId business key, not the internal MongoDB _id.
    public async Task<SolarStation?> FindByStationIdAsync(string stationId, CancellationToken ct) =>
        await db.Stations
            .Find(x => x.StationId == stationId.Trim().ToUpperInvariant())
            .FirstOrDefaultAsync(ct);

    // Uses the 2dsphere index created in MongoContext.InitializeIndexesAsync for
    // efficient bounding-sphere queries; only returns Active stations.
    public async Task<List<SolarStation>> FindNearbyAsync(
        double latitude,
        double longitude,
        double radiusKm,
        CancellationToken ct)
    {
        var point =
            new MongoDB.Driver.GeoJsonObjectModel.GeoJsonPoint<
                MongoDB.Driver.GeoJsonObjectModel.GeoJson2DGeographicCoordinates
            >(new(longitude, latitude));

        var filter =
            Builders<SolarStation>.Filter.NearSphere(x => x.Location, point, radiusKm * 1_000)
            & Builders<SolarStation>.Filter.Eq(x => x.Status, StationStatus.Active);

        return await db.Stations.Find(filter).ToListAsync(ct);
    }

    // Inserts a newly constructed SolarStation document into the collection.
    public async Task InsertAsync(SolarStation station, CancellationToken ct) =>
        await db.Stations.InsertOneAsync(station, cancellationToken: ct);

    // Replaces the entire document; uses the internal _id for the filter predicate.
    public async Task ReplaceAsync(SolarStation station, CancellationToken ct) =>
        await db.Stations.ReplaceOneAsync(
            x => x.Id == station.Id,
            station,
            cancellationToken: ct);

    // Applies a surgical update to only the Status and UpdatedAt fields.
    public async Task UpdateStatusAsync(
        string stationId,
        StationStatus status,
        CancellationToken ct) =>
        await db.Stations.UpdateOneAsync(
            x => x.StationId == stationId.Trim().ToUpperInvariant(),
            Builders<SolarStation>.Update
                .Set(x => x.Status, status)
                .Set(x => x.UpdatedAt, DateTime.UtcNow),
            cancellationToken: ct);

    // Returns true when the unique StationId index would be violated by a new insertion.
    public async Task<bool> ExistsAsync(string stationId, CancellationToken ct) =>
        await db.Stations
            .Find(x => x.StationId == stationId.Trim().ToUpperInvariant())
            .AnyAsync(ct);
}
