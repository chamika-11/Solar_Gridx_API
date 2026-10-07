// Smart Solar Microgrid Trading System - standard safe error response.
namespace SolarGridX.Api.DTOs.Responses;

public sealed record ErrorResponse(
    int Status,
    string Message,
    Dictionary<string, string[]>? Errors,
    DateTime Timestamp);
