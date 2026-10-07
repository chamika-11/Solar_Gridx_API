// Smart Solar Microgrid Trading System - energy reservation MongoDB document.
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SolarGridX.Api.Models.Enums;

namespace SolarGridX.Api.Models;

public sealed class EnergyReservation : AuditedDocument
{
    public string ReservationId { get; set; } = string.Empty;
    // Compatibility reference retained until reservation workflows are migrated to NIC-only Prosumer management.
    public string ProsumerUserId { get; set; } = string.Empty;
    public string ProsumerNic { get; set; } = string.Empty;
    public string StationId { get; set; } = string.Empty;
    public string SlotId { get; set; } = string.Empty;
    public DateTime ScheduledStartTime { get; set; }
    public DateTime ScheduledEndTime { get; set; }
    public decimal RequestedCapacity { get; set; }
    public string TransferType { get; set; } = "DropOff";
    public string? Notes { get; set; }
    [BsonRepresentation(BsonType.String)] public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
    public TransactionInfo? Transaction { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovedByUserId { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedByUserId { get; set; }
}
