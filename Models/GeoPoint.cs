using MongoDB.Bson.Serialization.Attributes;

namespace SolarGridX.Api.Models;

public sealed class GeoPoint
{
    [BsonElement("type")]
    public string Type { get; set; } = "Point";

    [BsonElement("coordinates")]
    public double[] Coordinates { get; set; } = [];
}
