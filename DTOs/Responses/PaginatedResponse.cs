// Smart Solar Microgrid Trading System - paginated response container.
namespace SolarGridX.Api.DTOs.Responses;

public sealed record PaginatedResponse<T>(
    IEnumerable<T> Items,
    int Page,
    int limit,
    long TotalCount,
    int TotalPages
);