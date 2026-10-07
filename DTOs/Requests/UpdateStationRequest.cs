// Smart Solar Microgrid Trading System - station update request.
using System.ComponentModel.DataAnnotations;
namespace SolarGridX.Api.DTOs.Requests;

public sealed class UpdateStationRequest
{
    [StringLength(30, ErrorMessage = "Station name must be 30 characters or fewer.")]
    public string? Name { get; init; }

    [StringLength(50, ErrorMessage = "Description must be 50 characters or fewer.")]
    public string? Description { get; init; }

    [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90.")]
    public double? Latitude { get; init; }

    [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180.")]
    public double? Longitude { get; init; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Capacity must be greater than 0.")]
    public decimal? CapacityKwh { get; init; }

    [Range(0, int.MaxValue, ErrorMessage = "Battery storage slots cannot be negative.")]
    public int? AvailableBatteryStorageSlots { get; init; }
}

