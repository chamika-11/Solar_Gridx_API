// Smart Solar Microgrid Trading System - staff account management service.
using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using SolarGridX.Api.Common;
using SolarGridX.Api.DTOs.Requests;
using SolarGridX.Api.DTOs.Responses;
using SolarGridX.Api.Infrastructure;
using SolarGridX.Api.Models;
using SolarGridX.Api.Models.Enums;

namespace SolarGridX.Api.Services;

public sealed class StaffService(
    MongoContext db,
    IPasswordService passwords,
    ILogger<StaffService> logger
)
{
    // Returns staff accounts (Backoffice and GridOperator) to Backoffice administrators.
    public async Task<PaginatedResponse<UserResponse>> GetStaffUsersAsync(
        string? search,
        UserRole? role,
        AccountStatus? status,
        string? sortField,
        string? sortOrder,
        int page,
        int limit,
        CancellationToken ct
    )
    {
        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.Or(
                Builders<User>.Filter.Eq(x => x.Role, UserRole.Backoffice),
                Builders<User>.Filter.Eq(x => x.Role, UserRole.GridOperator)
            )
        );

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var normalized = term.ToLowerInvariant();
            filter = Builders<User>.Filter.And(
                filter,
                Builders<User>.Filter.Or(
                    Builders<User>.Filter.Regex(x => x.FirstName, new BsonRegularExpression($"{Regex.Escape(normalized)}", "i")),
                    Builders<User>.Filter.Regex(x => x.LastName, new BsonRegularExpression($"{Regex.Escape(normalized)}", "i")),
                    Builders<User>.Filter.Regex(x => x.Email, new BsonRegularExpression($"{Regex.Escape(normalized)}", "i")),
                    Builders<User>.Filter.Regex(x => x.Nic, new BsonRegularExpression($"{Regex.Escape(normalized)}", "i"))
                )
            );
        }

        if (role.HasValue)
        {
            filter = Builders<User>.Filter.And(filter, Builders<User>.Filter.Eq(x => x.Role, role.Value));
        }

        if (status.HasValue)
        {
            filter = Builders<User>.Filter.And(filter, Builders<User>.Filter.Eq(x => x.AccountStatus, status.Value));
        }

        var normalizedSortField = (sortField ?? "name").Trim();
        var normalizedSortOrder = (sortOrder ?? "asc").Trim();
        var isDescending = string.Equals(normalizedSortOrder, "desc", StringComparison.OrdinalIgnoreCase);

        var sortDefinition = normalizedSortField.ToLowerInvariant() switch
        {
            "email" => isDescending
                ? Builders<User>.Sort.Descending(x => x.Email)
                : Builders<User>.Sort.Ascending(x => x.Email),
            "role" => isDescending
                ? Builders<User>.Sort.Descending(x => x.Role)
                : Builders<User>.Sort.Ascending(x => x.Role),
            "status" => isDescending
                ? Builders<User>.Sort.Descending(x => x.AccountStatus)
                : Builders<User>.Sort.Ascending(x => x.AccountStatus),
            _ => isDescending
                ? Builders<User>.Sort.Combine(
                    Builders<User>.Sort.Descending(x => x.FirstName),
                    Builders<User>.Sort.Descending(x => x.LastName))
                : Builders<User>.Sort.Combine(
                    Builders<User>.Sort.Ascending(x => x.FirstName),
                    Builders<User>.Sort.Ascending(x => x.LastName))
        };

        var safePage = Math.Max(1, page);
        var safeLimit = Math.Clamp(limit, 1, 100);
        var totalCount = await db.Users.CountDocumentsAsync(filter, cancellationToken: ct);
        var users = await db
            .Users.Find(filter)
            .Sort(sortDefinition)
            .Skip((safePage - 1) * safeLimit)
            .Limit(safeLimit)
            .ToListAsync(ct);

        var items = users.Select(x => x.ToResponse()).ToList();
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / safeLimit);

        return new PaginatedResponse<UserResponse>(items, safePage, safeLimit, totalCount, totalPages);
    }

    // Creates only Backoffice or GridOperator accounts; Prosumer accounts use public registration.
    public async Task<UserResponse> CreateStaffAsync(
        CreateUserRequest request,
        CancellationToken ct
    )
    {
        if (request.Role == UserRole.Prosumer)
            throw new ApiException(422, "Prosumer accounts must be created through registration.");

        var email = NormalizeEmail(request.Email);
        if (await db.Users.Find(x => x.Email == email).AnyAsync(ct))
            throw new ApiException(409, "Email address is already in use.");

        if (!string.IsNullOrWhiteSpace(request.Nic) && await db.Users.Find(x => x.Nic == request.Nic.Trim()).AnyAsync(ct))
            throw new ApiException(409, "NIC is already registered.");

        var user = new User
        {
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Phone = request.Phone?.Trim(),
            Address = request.Address?.Trim(),
            Nic = string.IsNullOrWhiteSpace(request.Nic) ? null : request.Nic.Trim(),
            PasswordHash = passwords.Hash(request.Password),
            Role = request.Role,
            AccountStatus = AccountStatus.Active,
        };

        await db.Users.InsertOneAsync(user, cancellationToken: ct);
        logger.LogInformation("Staff account created for {Email} with role {Role}", email, request.Role);
        return user.ToResponse();
    }

    // Activates or deactivates a staff account based on the requested account status.
    public async Task<UserResponse> UpdateStaffStatusAsync(string userId, AccountStatus newStatus, CancellationToken ct)
    {
        var user = await db.Users.Find(x => x.Id == userId).FirstOrDefaultAsync(ct)
            ?? throw new ApiException(404, "User was not found.");

        if (user.Role != UserRole.Backoffice && user.Role != UserRole.GridOperator)
            throw new ApiException(400, "Only staff accounts can be activated or deactivated.");

        if (newStatus != AccountStatus.Active && newStatus != AccountStatus.Deactivated)
            throw new ApiException(400, "Staff status can only be set to Active or Deactivated.");

        if (user.AccountStatus == newStatus)
            throw new ApiException(409, $"Staff account is already {newStatus}.");

        user.AccountStatus = newStatus;
        user.UpdatedAt = DateTime.UtcNow;

        await db.Users.ReplaceOneAsync(x => x.Id == user.Id, user, cancellationToken: ct);
        logger.LogInformation("Staff account {UserId} set to {Status}", user.Id, newStatus);
        return user.ToResponse();
    }

    private static string NormalizeEmail(string value) => value.Trim().ToLowerInvariant();
}
