// Smart Solar Microgrid Trading System - opaque QR token response.
namespace SolarGridX.Api.DTOs.Responses;

public sealed record QrTokenResponse(
    string TransactionId,
    string Token,
    DateTime ExpiresAt);
