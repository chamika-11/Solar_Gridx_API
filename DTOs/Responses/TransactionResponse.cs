// Smart Solar Microgrid Trading System - QR transaction response.
using SolarGridX.Api.Models.Enums;
namespace SolarGridX.Api.DTOs.Responses;

public sealed record TransactionResponse(
    string TransactionId,
    string ReservationId,
    string StationId,
    DateTime ScheduledStartTime,
    DateTime ScheduledEndTime,
    decimal RequestedCapacity,
    TransactionStatus Status,
    DateTime ExpiresAt,
    string? ProsumerName = null,
    string? ProsumerNic = null,
    string? StationName = null,
    string? TransferType = null,
    string? SlotId = null);
