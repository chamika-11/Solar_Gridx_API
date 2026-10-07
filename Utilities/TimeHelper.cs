// Smart Solar Microgrid Trading System - shared time and formatting helpers.
namespace SolarGridX.Api.Utilities;

// Provides reusable time zone utilities used by multiple services.
public static class TimeHelper
{
    // Converts any DateTime to UTC, treating Unspecified kind as UTC rather than local time.
    // Services always store and compare timestamps in UTC.
    public static DateTime ToUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value.ToUniversalTime(),
        };

    // Returns the current server UTC time.
    // Used as a single source of truth so tests can verify time-based logic.
    public static DateTime UtcNow() => DateTime.UtcNow;
}
