// Smart Solar Microgrid Trading System - one-time Backoffice bootstrap account.
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using SolarGridX.Api.Configuration;
using SolarGridX.Api.Models;
using SolarGridX.Api.Models.Enums;
using SolarGridX.Api.Services;

namespace SolarGridX.Api.Infrastructure;

public sealed class InitialBackofficeSeeder(
    MongoContext db,
    IPasswordService passwords,
    IOptions<SeedOptions> options,
    ILogger<InitialBackofficeSeeder> logger
)
{
    // Creates the configured first Backoffice only when no Backoffice exists yet.
    // Subsequent staff accounts must be created through the authenticated API.
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var seed = options.Value;
        if (!seed.Enabled)
            return;

        if (await db.Users.Find(x => x.Role == UserRole.Backoffice).AnyAsync(ct))
        {
            logger.LogInformation("Initial Backoffice seed skipped because a Backoffice account already exists.");
            return;
        }

        var email = seed.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(seed.Password)
            || seed.Password.Length < 8
            || string.IsNullOrWhiteSpace(seed.FirstName)
            || string.IsNullOrWhiteSpace(seed.LastName))
        {
            throw new InvalidOperationException(
                "When Seed:Enabled is true, configure Seed:Email, Seed:Password (at least 8 characters), Seed:FirstName and Seed:LastName."
            );
        }

        var user = new User
        {
            Email = email,
            PasswordHash = passwords.Hash(seed.Password),
            FirstName = seed.FirstName.Trim(),
            LastName = seed.LastName.Trim(),
            Role = UserRole.Backoffice,
            AccountStatus = AccountStatus.Active,
        };

        try
        {
            await db.Users.InsertOneAsync(user, cancellationToken: ct);
            logger.LogInformation("Initial Backoffice account was created for {Email}.", email);
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // Another application instance may have completed this one-time seed concurrently.
            if (!await db.Users.Find(x => x.Role == UserRole.Backoffice).AnyAsync(ct))
            {
                throw new InvalidOperationException(
                    "The configured Seed:Email is already in use by a non-Backoffice account. Choose a different Seed:Email."
                );
            }
        }

        // 1. Seed Grid Operator if not exists
        var operatorEmail = "operator@solargridx.local";
        if (!await db.Users.Find(x => x.Email == operatorEmail).AnyAsync(ct))
        {
            var op = new User
            {
                Email = operatorEmail,
                PasswordHash = passwords.Hash("Operator@1234"),
                FirstName = "Grid",
                LastName = "Operator",
                Role = UserRole.GridOperator,
                AccountStatus = AccountStatus.Active
            };
            try { await db.Users.InsertOneAsync(op, cancellationToken: ct); } catch { /* ignore concurrency */ }
        }

        // 2. Seed Prosumer Nimal if not exists
        var nimalEmail = "nimal@solargridx.local";
        var nimalNic = "199012345678";
        var existingNimal = await db.Users.Find(x => x.Email == nimalEmail || x.Nic == nimalNic).FirstOrDefaultAsync(ct);
        if (existingNimal == null)
        {
            var nimal = new User
            {
                Nic = nimalNic,
                Email = nimalEmail,
                PasswordHash = passwords.Hash("Prosumer@1234"),
                FirstName = "Nimal",
                LastName = "Perera",
                Role = UserRole.Prosumer,
                AccountStatus = AccountStatus.Active,
                Phone = "0771234567",
                Address = "Kottawa, Sri Lanka"
            };
            try { await db.Users.InsertOneAsync(nimal, cancellationToken: ct); } catch { /* ignore concurrency */ }
        }
        else if (existingNimal.AccountStatus != AccountStatus.Active)
        {
            await db.Users.UpdateOneAsync(
                x => x.Id == existingNimal.Id,
                Builders<User>.Update.Set(x => x.AccountStatus, AccountStatus.Active),
                cancellationToken: ct
            );
        }

        // 3. Seed Station SGXST-003 - Kottawa Solar Energy Hub
        var kottawaStationId = "SGXST-003";
        if (!await db.Stations.Find(x => x.StationId == kottawaStationId).AnyAsync(ct))
        {
            var station = new SolarStation
            {
                StationId = kottawaStationId,
                Name = "Kottawa Solar Energy Hub",
                Description = "Kottawa regional microgrid battery storage node",
                Location = new GeoPoint { Coordinates = [79.9658, 6.8404] },
                CapacityKwh = 100.0m,
                AvailableBatteryStorageSlots = 4,
                Status = StationStatus.Active,
            };
            try { await db.Stations.InsertOneAsync(station, cancellationToken: ct); } catch { /* ignore concurrency */ }
        }

        // 4. Seed slot on 25 September 2026 2:00 PM – 3:00 PM (14:00 - 15:00 UTC) with 10 kWh available capacity
        var slotStart = new DateTime(2026, 9, 25, 14, 0, 0, DateTimeKind.Utc);
        var slotEnd = new DateTime(2026, 9, 25, 15, 0, 0, DateTimeKind.Utc);
        if (!await db.Slots.Find(x => x.StationId == kottawaStationId && x.StartTime == slotStart).AnyAsync(ct))
        {
            var slot = new EnergyBookingSlot
            {
                SlotId = "SLT-KOTTAWA-01",
                StationId = kottawaStationId,
                StartTime = slotStart,
                EndTime = slotEnd,
                Capacity = 10.0m,
                AvailableCapacity = 10.0m,
                Status = SlotStatus.Active
            };
            try { await db.Slots.InsertOneAsync(slot, cancellationToken: ct); } catch { /* ignore concurrency */ }
        }
    }
}
