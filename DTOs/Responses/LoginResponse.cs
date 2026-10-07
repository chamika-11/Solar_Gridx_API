// Smart Solar Microgrid Trading System - authenticated login response.
namespace SolarGridX.Api.DTOs.Responses;

public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    UserResponse User);
