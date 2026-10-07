// Smart Solar Microgrid Trading System - station response.
using SolarGridX.Api.Models.Enums;
namespace SolarGridX.Api.DTOs.Responses;

public sealed record StationResponse(
    string Id,
    string StationId,
    string Name,
    string? Description,
    double Latitude,
    double Longitude,
    decimal CapacityKwh,
    int AvailableBatteryStorageSlots,
    StationStatus Status);
