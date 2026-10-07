// Smart Solar Microgrid Trading System - booking slot response.
using SolarGridX.Api.Models.Enums;
namespace SolarGridX.Api.DTOs.Responses;

public sealed record SlotResponse(
    string Id,
    string SlotId,
    string StationId,
    DateTime StartTime,
    DateTime EndTime,
    decimal Capacity,
    decimal AvailableCapacity,
    SlotStatus Status,
    string? StationName = null,
    string? ComputedStatus = null);
