// =============================================================================
// File   : StationService.cs
// Purpose: FAT service — all station business rules enforced here.
//          Depends only on IStationRepository (data access) and
//          IReservationLookupService (cross-member integration seam).
//          Controller is a thin adapter; this class owns every decision.
// Author : Member 2
// Date   : 2026-09-21
// =============================================================================
using SolarGridX.Api.Common;
using SolarGridX.Api.DTOs.Requests;
using SolarGridX.Api.DTOs.Responses;
using SolarGridX.Api.Integrations;
using SolarGridX.Api.Models;
using SolarGridX.Api.Models.Enums;
using SolarGridX.Api.Repositories;

namespace SolarGridX.Api.Services;

public sealed class StationService(
    IStationRepository repository,
    IReservationLookupService reservationLookup
) : IStationService
{
    // Coordinate and capacity limits kept as named constants to avoid magic numbers.
    private const double MinLatitude = -90.0;
    private const double MaxLatitude = 90.0;
    private const double MinLongitude = -180.0;
    private const double MaxLongitude = 180.0;
    private const double MinCapacityKwh = 0.01;
    private const int MaxStationIdLength = 50;
    private const int MaxStationNameLength = 30;
    private const int MinBatterySlots = 0;
    private const double MaxRadiusKm = 100.0;

    // Creates a new Active station after validating coordinates, capacity, and ID uniqueness.
    public async Task<StationResponse> CreateAsync(
        CreateStationRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.StationId))
            throw new ApiException(422, "Station ID is required.");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ApiException(422, "Station name is required.");
        if (request.StationId.Trim().Length > MaxStationIdLength)
            throw new ApiException(422, $"Station ID must be {MaxStationIdLength} characters or fewer.");
        if (request.Name.Trim().Length > MaxStationNameLength)
            throw new ApiException(422, $"Station name must be {MaxStationNameLength} characters or fewer.");

        // Normalise the business key to uppercase for consistent indexing.
        var stationId = request.StationId.Trim().ToUpperInvariant();

        ValidateCoordinates(request.Latitude, request.Longitude);
        ValidateCapacity(request.CapacityKwh);
        ValidateBatterySlots(request.AvailableBatteryStorageSlots);

        if (await repository.ExistsAsync(stationId, ct))
            throw new ApiException(409, $"Station ID '{stationId}' is already in use.");

        var station = new SolarStation
        {
            StationId = stationId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            // MongoDB GeoJSON requires [longitude, latitude] order.
            Location = new GeoPoint { Coordinates = [request.Longitude!.Value, request.Latitude!.Value] },
            CapacityKwh = request.CapacityKwh,
            AvailableBatteryStorageSlots = request.AvailableBatteryStorageSlots,
            Status = StationStatus.Active,
        };

        await repository.InsertAsync(station, ct);
        return station.ToResponse();
    }

    // Returns stations sorted by name; active-only is enforced for Prosumer callers.
    public async Task<List<StationResponse>> GetAllAsync(bool activeOnly, CancellationToken ct)
    {
        var stations = await repository.GetAllAsync(activeOnly, ct);
        return stations.Select(s => s.ToResponse()).ToList();
    }

    // Looks up a station by its business identifier and maps it to a response DTO.
    public async Task<StationResponse> GetByStationIdAsync(
        string stationId,
        bool includeInactive,
        CancellationToken ct)
    {
        var station = await FindOrThrowAsync(stationId, ct);
        if (!includeInactive && station.Status != StationStatus.Active)
            throw new ApiException(404, $"Station '{station.StationId}' was not found.");
        return station.ToResponse();
    }

    // Updates mutable station fields; partial updates are supported (null fields are preserved).
    public async Task<StationResponse> UpdateAsync(
        string stationId,
        UpdateStationRequest request,
        CancellationToken ct)
    {
        var station = await FindOrThrowAsync(stationId, ct);

        if (request.Name is not null && string.IsNullOrWhiteSpace(request.Name))
            throw new ApiException(422, "Station name cannot be blank.");
        if (request.Name?.Trim().Length > MaxStationNameLength)
            throw new ApiException(422, $"Station name must be {MaxStationNameLength} characters or fewer.");

        // Latitude and longitude must be provided together or not at all.
        if (request.Latitude.HasValue != request.Longitude.HasValue)
            throw new ApiException(400, "Latitude and longitude must be supplied together.");

        if (request.Latitude.HasValue)
            ValidateCoordinates(request.Latitude.Value, request.Longitude!.Value);

        if (request.CapacityKwh.HasValue)
            ValidateCapacity(request.CapacityKwh.Value);

        if (request.AvailableBatteryStorageSlots.HasValue)
            ValidateBatterySlots(request.AvailableBatteryStorageSlots.Value);

        station.Name = request.Name?.Trim() ?? station.Name;
        station.Description = request.Description?.Trim() ?? station.Description;
        station.CapacityKwh = request.CapacityKwh ?? station.CapacityKwh;
        station.AvailableBatteryStorageSlots =
            request.AvailableBatteryStorageSlots ?? station.AvailableBatteryStorageSlots;

        if (request.Latitude.HasValue)
            station.Location = new GeoPoint
            {
                Coordinates = [request.Longitude!.Value, request.Latitude.Value],
            };

        station.UpdatedAt = DateTime.UtcNow;
        await repository.ReplaceAsync(station, ct);
        return station.ToResponse();
    }

    // Blocks deactivation if IReservationLookupService reports active reservations (409 Conflict).
    public async Task DeactivateAsync(string stationId, CancellationToken ct)
    {
        var station = await FindOrThrowAsync(stationId, ct);

        // Delegate the cross-member check to the injected integration seam.
        var hasActive = await reservationLookup.HasActiveReservationsAsync(
            station.StationId, ct);

        if (hasActive)
            throw new ApiException(
                409,
                $"Station '{station.StationId}' cannot be deactivated: " +
                "it has active reservations (pending or approved) that must be resolved first.");

        await repository.UpdateStatusAsync(station.StationId, StationStatus.Deactivated, ct);
    }

    // Restores a deactivated station so prosumers can book against it again.
    public async Task ReactivateAsync(string stationId, CancellationToken ct)
    {
        var station = await FindOrThrowAsync(stationId, ct);
        await repository.UpdateStatusAsync(station.StationId, StationStatus.Active, ct);
    }

    // Returns active stations within the specified radius; radius is bounded to 100 km.
    public async Task<List<StationResponse>> NearbyAsync(
        double latitude,
        double longitude,
        double radiusKm,
        CancellationToken ct)
    {
        ValidateCoordinates(latitude, longitude);

        if (radiusKm <= 0 || radiusKm > MaxRadiusKm)
            throw new ApiException(
                400, $"radiusKm must be greater than zero and at most {MaxRadiusKm}.");

        var stations = await repository.FindNearbyAsync(latitude, longitude, radiusKm, ct);
        return stations.Select(s => s.ToResponse()).ToList();
    }

    // Retrieves a station or throws a standardised 404 to the caller.
    private async Task<SolarStation> FindOrThrowAsync(string stationId, CancellationToken ct) =>
        await repository.FindByStationIdAsync(stationId, ct)
        ?? throw new ApiException(404, $"Station '{stationId}' was not found.");

    // Ensures latitude is within the WGS-84 valid range.
    private static void ValidateCoordinates(double? latitude, double? longitude)
    {
        if (!latitude.HasValue || !longitude.HasValue)
            throw new ApiException(422, "Latitude and longitude are required.");

        ValidateCoordinates(latitude.Value, longitude.Value);
    }

    // Ensures latitude is within the WGS-84 valid range.
    private static void ValidateCoordinates(double lat, double lon)
    {
        if (lat is < MinLatitude or > MaxLatitude)
            throw new ApiException(422, $"Latitude must be between {MinLatitude} and {MaxLatitude}.");
        if (lon is < MinLongitude or > MaxLongitude)
            throw new ApiException(422, $"Longitude must be between {MinLongitude} and {MaxLongitude}.");
    }

    // Ensures capacity is a meaningful positive value.
    private static void ValidateCapacity(decimal capacity)
    {
        if (capacity < (decimal)MinCapacityKwh)
            throw new ApiException(422, $"CapacityKwh must be at least {MinCapacityKwh}.");
    }

    // Ensures battery slot count is non-negative.
    private static void ValidateBatterySlots(int slots)
    {
        if (slots < MinBatterySlots)
            throw new ApiException(422, "AvailableBatteryStorageSlots must be zero or positive.");
    }

}
