// Smart Solar Microgrid Trading System - shared MongoDB audit fields.
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SolarGridX.Api.Models;

[BsonIgnoreExtraElements(Inherited = true)]
public abstract class AuditedDocument
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)] public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
