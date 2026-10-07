// Smart Solar Microgrid Trading System - booking slot update request.
using System.ComponentModel.DataAnnotations;
namespace SolarGridX.Api.DTOs.Requests;

public sealed class UpdateSlotRequest
{
    public DateTime? StartTime { get; init; }

    public DateTime? EndTime { get; init; }

    [Range(0.01, double.MaxValue)]
    public decimal? Capacity { get; init; }

    [Range(0, double.MaxValue)]
    public decimal? AvailableCapacity { get; init; }
}
