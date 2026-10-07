// Smart Solar Microgrid Trading System - reservation update request.
using System.ComponentModel.DataAnnotations;
namespace SolarGridX.Api.DTOs.Requests;

public sealed class UpdateReservationRequest
{
    [Required]
    public string StationId { get; init; } = string.Empty;

    [Required]
    public string SlotId { get; init; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal RequestedCapacity { get; init; }
}
