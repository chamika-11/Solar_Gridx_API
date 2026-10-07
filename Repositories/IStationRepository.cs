// =============================================================================
// File   : IStationRepository.cs
// Purpose: Data-access contract for SolarStation MongoDB documents.
//          Defines the read/write operations that StationService depends on.
//          No business logic — only persistence primitives.
// Author : Member 2
// Date   : 2026-09-21
// =============================================================================
using SolarGridX.Api.Models;
using SolarGridX.Api.Models.Enums;

namespace SolarGridX.Api.Repositories;

public interface IStationRepository
{
    // Returns every station in the collection, sorted by name.
    Task<List<SolarStation>> GetAllAsync(bool activeOnly, CancellationToken ct);

    // Finds a single station by its human-readable business identifier (e.g. "SGX-01").
    Task<SolarStation?> FindByStationIdAsync(string stationId, CancellationToken ct);

    // Returns stations within radiusKm kilometres of the supplied coordinate pair.
    Task<List<SolarStation>> FindNearbyAsync(
        double latitude,
        double longitude,
        double radiusKm,
        CancellationToken ct);

    // Persists a brand-new station document.
    Task InsertAsync(SolarStation station, CancellationToken ct);

    // Replaces the full station document in-place (optimistic full replacement).
    Task ReplaceAsync(SolarStation station, CancellationToken ct);

    // Performs a targeted status field update without replacing the entire document.
    Task UpdateStatusAsync(string stationId, StationStatus status, CancellationToken ct);

    // Returns true if a station with the given business identifier already exists.
    Task<bool> ExistsAsync(string stationId, CancellationToken ct);
}
