// Smart Solar Microgrid Trading System - reservation QR transaction data.
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SolarGridX.Api.Models.Enums;

namespace SolarGridX.Api.Models;

public sealed class TransactionInfo
{
    public string TransactionId { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedByUserId { get; set; }
    [BsonRepresentation(BsonType.String)] public TransactionStatus Status { get; set; } = TransactionStatus.Active;
}
