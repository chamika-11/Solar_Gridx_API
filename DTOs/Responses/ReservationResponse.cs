// Smart Solar Microgrid Trading System - reservation response.
using SolarGridX.Api.Models.Enums;
namespace SolarGridX.Api.DTOs.Responses;

public sealed record ReservationResponse(
    string Id,
    string ReservationId,
    string StationId,
    string SlotId,
    DateTime ScheduledStartTime,
    DateTime ScheduledEndTime,
    decimal RequestedCapacity,
    ReservationStatus Status,
    string? TransactionId,
    DateTime? TransactionExpiresAt,
    DateTime? ApprovedAt,
    DateTime? CancelledAt,
    DateTime? CompletedAt,
    string? ProsumerNic = null,
    string? TransferType = null,
    string? Notes = null,
    string? ApprovedByUserId = null,
    string? CompletedByUserId = null);
