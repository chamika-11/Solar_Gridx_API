// Smart Solar Microgrid Trading System - MongoDB collections and index initialization.
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using SolarGridX.Api.Configuration;
using SolarGridX.Api.Models;
using SolarGridX.Api.Models.Enums;

namespace SolarGridX.Api.Infrastructure;

public sealed class MongoContext
{
    public IMongoDatabase Database { get; }
    public IMongoCollection<User> Users => Database.GetCollection<User>("users");
    public IMongoCollection<SolarStation> Stations => Database.GetCollection<SolarStation>("solarStations");
    public IMongoCollection<EnergyBookingSlot> Slots => Database.GetCollection<EnergyBookingSlot>("energyBookingSlots");
    public IMongoCollection<EnergyReservation> Reservations => Database.GetCollection<EnergyReservation>("energyReservations");
    public IMongoCollection<TransactionLog> TransactionLogs => Database.GetCollection<TransactionLog>("transactionLogs");

    // Creates the database handle from configured server-only connection settings.
    public MongoContext(IOptions<MongoDbOptions> options)
    {
        var value = options.Value;
        if (string.IsNullOrWhiteSpace(value.ConnectionString)) throw new InvalidOperationException("MongoDb:ConnectionString is required.");
        Database = new MongoClient(value.ConnectionString).GetDatabase(value.DatabaseName);
    }

    // Creates essential uniqueness and query indexes during safe application startup.
    public async Task InitializeIndexesAsync(CancellationToken cancellationToken = default)
    {
        await Users.Indexes.CreateManyAsync([
            new CreateIndexModel<User>(Builders<User>.IndexKeys.Ascending(x => x.Email), new CreateIndexOptions { Unique = true, Name = "ux_users_email" }),
            new CreateIndexModel<User>(Builders<User>.IndexKeys.Ascending(x => x.Nic), new CreateIndexOptions { Unique = true, Sparse = true, Name = "ux_users_nic" }),
            new CreateIndexModel<User>(Builders<User>.IndexKeys.Ascending(x => x.Role).Ascending(x => x.AccountStatus))], cancellationToken);
        await Stations.Indexes.CreateManyAsync([
            new CreateIndexModel<SolarStation>(Builders<SolarStation>.IndexKeys.Ascending(x => x.StationId), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<SolarStation>(Builders<SolarStation>.IndexKeys.Geo2DSphere("Location")),
            new CreateIndexModel<SolarStation>(Builders<SolarStation>.IndexKeys.Ascending(x => x.Status))], cancellationToken);
        await Slots.Indexes.CreateManyAsync([
            new CreateIndexModel<EnergyBookingSlot>(Builders<EnergyBookingSlot>.IndexKeys.Ascending(x => x.SlotId), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<EnergyBookingSlot>(Builders<EnergyBookingSlot>.IndexKeys.Ascending(x => x.StationId).Ascending(x => x.StartTime).Ascending(x => x.Status))], cancellationToken);
        await Reservations.Indexes.CreateManyAsync([
            new CreateIndexModel<EnergyReservation>(Builders<EnergyReservation>.IndexKeys.Ascending(x => x.ReservationId), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<EnergyReservation>(Builders<EnergyReservation>.IndexKeys.Ascending(x => x.ProsumerNic).Ascending(x => x.Status).Descending(x => x.ScheduledStartTime)),
            new CreateIndexModel<EnergyReservation>(Builders<EnergyReservation>.IndexKeys.Ascending(x => x.StationId).Ascending(x => x.Status).Ascending(x => x.ScheduledStartTime)),
            new CreateIndexModel<EnergyReservation>(Builders<EnergyReservation>.IndexKeys.Ascending(x => x.SlotId).Ascending(x => x.Status)),
            new CreateIndexModel<EnergyReservation>(Builders<EnergyReservation>.IndexKeys.Ascending("Transaction.TransactionId"), new CreateIndexOptions { Unique = true, Sparse = true })], cancellationToken);
        await TransactionLogs.Indexes.CreateManyAsync([
            new CreateIndexModel<TransactionLog>(Builders<TransactionLog>.IndexKeys.Ascending(x => x.TransactionId), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<TransactionLog>(Builders<TransactionLog>.IndexKeys.Ascending(x => x.ReservationId)),
            new CreateIndexModel<TransactionLog>(Builders<TransactionLog>.IndexKeys.Ascending(x => x.OperatorUserId))], cancellationToken);
    }

    // Migrates accounts created before Prosumer became a first-class user role.
    public async Task MigrateLegacyProsumerAccountsAsync(CancellationToken cancellationToken = default)
    {
        var legacyProsumer = Builders<User>.Filter.Eq("IsProsumer", true)
            & (Builders<User>.Filter.Exists("Role", false)
                | Builders<User>.Filter.Eq("Role", BsonNull.Value));
        await Users.UpdateManyAsync(
            legacyProsumer,
            Builders<User>.Update
                .Set(x => x.Role, UserRole.Prosumer)
                .Unset("IsProsumer"),
            cancellationToken: cancellationToken
        );
    }
}
