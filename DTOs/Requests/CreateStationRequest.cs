// Smart Solar Microgrid Trading System - station creation request.
using System.ComponentModel.DataAnnotations;
namespace SolarGridX.Api.DTOs.Requests;

public sealed class CreateStationRequest
{
    [Required]
    [StringLength(50, ErrorMessage = "Station ID must be 50 characters or fewer.")]
    public string StationId { get; init; } = string.Empty;

    [Required(ErrorMessage = "Station name is required.")]
    [StringLength(30, ErrorMessage = "Station name must be 30 characters or fewer.")]
    public string Name { get; init; } = string.Empty;

    [StringLength(50, ErrorMessage = "Description must be 50 characters or fewer.")]
    public string? Description { get; init; }

    [Required(ErrorMessage = "Latitude is required.")]
    [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90.")]
    public double? Latitude { get; init; }

    [Required(ErrorMessage = "Longitude is required.")]
    [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180.")]
    public double? Longitude { get; init; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Capacity must be greater than 0.")]
    public decimal CapacityKwh { get; init; }

    [Range(0, int.MaxValue, ErrorMessage = "Battery storage slots cannot be negative.")]
    public int AvailableBatteryStorageSlots { get; init; }
}

