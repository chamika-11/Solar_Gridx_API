// Smart Solar Microgrid Trading System - finalized transaction audit log.
using MongoDB.Bson.Serialization.Attributes;

namespace SolarGridX.Api.Models;

[BsonIgnoreExtraElements]
public sealed class TransactionLog : AuditedDocument
{
    public string TransactionId { get; set; } = string.Empty;
    public string ReservationId { get; set; } = string.Empty;
    public string StationId { get; set; } = string.Empty;
    public string ProsumerNic { get; set; } = string.Empty;
    public decimal TransferredCapacity { get; set; }
    public string TransferType { get; set; } = "DropOff";
    public string OperatorUserId { get; set; } = string.Empty;
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}
