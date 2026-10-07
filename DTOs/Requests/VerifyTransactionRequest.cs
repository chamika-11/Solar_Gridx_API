// Smart Solar Microgrid Trading System - QR verification request.
using System.ComponentModel.DataAnnotations;
namespace SolarGridX.Api.DTOs.Requests;

public sealed class VerifyTransactionRequest
{
    [Required]
    public string Token { get; init; } = string.Empty;

    public string? StationId { get; init; }
}
