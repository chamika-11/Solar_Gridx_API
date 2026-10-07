// Smart Solar Microgrid Trading System - energy booking slot MongoDB document.
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SolarGridX.Api.Models.Enums;

namespace SolarGridX.Api.Models;

public sealed class EnergyBookingSlot : AuditedDocument
{
    public string SlotId { get; set; } = string.Empty;
    public string StationId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public decimal Capacity { get; set; }
    public decimal AvailableCapacity { get; set; }
    [BsonRepresentation(BsonType.String)] public SlotStatus Status { get; set; } = SlotStatus.Active;
}
