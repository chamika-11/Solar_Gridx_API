// Smart Solar Microgrid Trading System - recurring booking slot creation request.
using System.ComponentModel.DataAnnotations;

namespace SolarGridX.Api.DTOs.Requests;

public sealed class DailyTimeSlotRequest
{
    [Required]
    public TimeSpan StartTime { get; init; }

    [Required]
    public TimeSpan EndTime { get; init; }
}

public sealed class CreateRecurringSlotsRequest
{
    [Required]
    public string StationId { get; init; } = string.Empty;

    [Required]
    public DateTime StartDate { get; init; }

    [Required]
    public DateTime EndDate { get; init; }

    // Selected days of the week (e.g. Monday, Wednesday). If empty, applies to all days in range.
    public List<DayOfWeek> DaysOfWeek { get; init; } = [];

    // Selected daily time slots (e.g. 08:00 to 10:00, 14:00 to 16:00).
    [Required]
    [MinLength(1, ErrorMessage = "At least one daily time slot must be specified.")]
    public List<DailyTimeSlotRequest> TimeSlots { get; init; } = [];

    [Range(0.01, double.MaxValue)]
    public decimal Capacity { get; init; }

    // When true, skips slots that overlap with existing active slots rather than throwing an error.
    public bool SkipExistingConflicts { get; init; } = true;
}
