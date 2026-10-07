// Smart Solar Microgrid Trading System - booking slot creation request.
using System.ComponentModel.DataAnnotations;
namespace SolarGridX.Api.DTOs.Requests;

public sealed class CreateSlotRequest
{
    [Required]
    public string StationId { get; init; } = string.Empty;

    public DateTime StartTime { get; init; }

    public DateTime EndTime { get; init; }

    [Range(0.01, double.MaxValue)]
    public decimal Capacity { get; init; }
}
