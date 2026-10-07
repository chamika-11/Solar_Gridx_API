// =============================================================================
// File   : IStationService.cs
// Purpose: Business-logic contract for the station management domain.
//          Controllers depend on this interface, not on the concrete service,
//          enabling unit testing with mock implementations and clean DI.
// Author : Member 2
// Date   : 2026-09-21
// =============================================================================
using SolarGridX.Api.DTOs.Requests;
using SolarGridX.Api.DTOs.Responses;

namespace SolarGridX.Api.Services;

public interface IStationService
{
    // Creates and persists a new active solar station node.
    Task<StationResponse> CreateAsync(CreateStationRequest request, CancellationToken ct);

    // Returns the full list of stations; activeOnly restricts to Active status for Prosumer callers.
    Task<List<StationResponse>> GetAllAsync(bool activeOnly, CancellationToken ct);

    // Retrieves one station by its business identifier; throws 404 if not found.
    Task<StationResponse> GetByStationIdAsync(string stationId, bool includeInactive, CancellationToken ct);

    // Updates mutable station fields (name, description, coordinates, capacity, slots).
    Task<StationResponse> UpdateAsync(
        string stationId,
        UpdateStationRequest request,
        CancellationToken ct);

    // Deactivates a station; throws 409 Conflict if active reservations exist.
    Task DeactivateAsync(string stationId, CancellationToken ct);

    // Restores a previously deactivated station back to Active status.
    Task ReactivateAsync(string stationId, CancellationToken ct);

    // Returns active stations within radiusKm kilometres of the given coordinates.
    Task<List<StationResponse>> NearbyAsync(
        double latitude,
        double longitude,
        double radiusKm,
        CancellationToken ct);
}
