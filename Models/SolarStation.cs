// Smart Solar Microgrid Trading System - solar station MongoDB document.
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SolarGridX.Api.Models.Enums;

namespace SolarGridX.Api.Models;

public sealed class SolarStation : AuditedDocument
{
    public string StationId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public GeoPoint Location { get; set; } = new();
    public decimal CapacityKwh { get; set; }
    public int AvailableBatteryStorageSlots { get; set; }
    [BsonRepresentation(BsonType.String)] public StationStatus Status { get; set; } = StationStatus.Active;
}
