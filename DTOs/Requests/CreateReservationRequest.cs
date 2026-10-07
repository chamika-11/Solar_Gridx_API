// Smart Solar Microgrid Trading System - reservation creation request.
using System.ComponentModel.DataAnnotations;
namespace SolarGridX.Api.DTOs.Requests;

public sealed class CreateReservationRequest
{
    [Required]
    public string StationId { get; init; } = string.Empty;

    [Required]
    public string SlotId { get; init; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal RequestedCapacity { get; init; }

    public string TransferType { get; init; } = "DropOff";

    public string? Notes { get; init; }
}
