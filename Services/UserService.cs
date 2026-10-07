// Smart Solar Microgrid Trading System - authentication and user workflows.
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;
using SolarGridX.Api.Common;
using SolarGridX.Api.DTOs.Requests;
using SolarGridX.Api.DTOs.Responses;
using SolarGridX.Api.Infrastructure;
using SolarGridX.Api.Models;
using SolarGridX.Api.Models.Enums;

namespace SolarGridX.Api.Services;

public sealed class UserService(
    MongoContext db,
    IPasswordService passwords,
    ITokenService tokens,
    ILogger<UserService> logger
)
{
    // Registers a Prosumer user in pending state so Backoffice can manage activation.
    public async Task<UserResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var nic = NormalizeNic(request.Nic);
        var email = NormalizeEmail(request.Email);
        await EnsureAvailableAsync(nic, email, ct);
        var user = new User
        {
            Nic = nic,
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Phone = request.Phone?.Trim(),
            Address = request.Address?.Trim(),
            PasswordHash = passwords.Hash(request.Password),
            Role = UserRole.Prosumer,
            AccountStatus = AccountStatus.Pending,
        };
        await db.Users.InsertOneAsync(user, cancellationToken: ct);
        logger.LogInformation("Prosumer registration created for NIC {Nic}", nic);
        return user.ToResponse();
    }

    // Authenticates an active account and returns a minimal JWT response.
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await db
            .Users.Find(x => x.Email == NormalizeEmail(request.Email))
            .FirstOrDefaultAsync(ct);
        if (user is null || !passwords.Verify(request.Password, user.PasswordHash))
            throw new ApiException(401, "Invalid email or password.");
        if (user.AccountStatus == AccountStatus.Deactivated)
            throw new ApiException(403, "This account has been deactivated.");
        if (user.AccountStatus == AccountStatus.Pending)
            throw new ApiException(403, "This account is pending approval.");
        if (user.AccountStatus != AccountStatus.Active && user.AccountStatus != AccountStatus.DeactivationRequested)
            throw new ApiException(403, "This account is not active.");
        var token = tokens.Create(user);
        logger.LogInformation("Login succeeded for user {UserId}", user.Id);
        System.Console.WriteLine(user);
        return new LoginResponse(token.Token, token.ExpiresAt, user.ToResponse());
    }

    // Finds the current authenticated account from the JWT subject.
    public async Task<UserResponse> GetCurrentAsync(string userId, CancellationToken ct) =>
        (await FindByIdAsync(userId, ct)).ToResponse();

    // Lets any authenticated user update their own display profile fields.
    public async Task<UserResponse> UpdateProfileAsync(
        string userId,
        UpdateProfileRequest request,
        CancellationToken ct)
    {
        var user = await FindByIdAsync(userId, ct);
        user.FirstName  = request.FirstName?.Trim()  ?? user.FirstName;
        user.LastName   = request.LastName?.Trim()   ?? user.LastName;
        user.Phone      = request.Phone?.Trim()      ?? user.Phone;
        user.Address    = request.Address?.Trim()    ?? user.Address;
        user.UpdatedAt  = DateTime.UtcNow;
        await db.Users.ReplaceOneAsync(x => x.Id == user.Id, user, cancellationToken: ct);
        logger.LogInformation("Profile updated for user {UserId}", userId);
        return user.ToResponse();
    }

    // Verifies the current password then replaces it with a new hash.
    public async Task ChangePasswordAsync(
        string userId,
        ChangePasswordRequest request,
        CancellationToken ct)
    {
        var user = await FindByIdAsync(userId, ct);
        if (!passwords.Verify(request.CurrentPassword, user.PasswordHash))
            throw new ApiException(400, "Current password is incorrect.");
        user.PasswordHash = passwords.Hash(request.NewPassword);
        user.UpdatedAt    = DateTime.UtcNow;
        await db.Users.ReplaceOneAsync(x => x.Id == user.Id, user, cancellationToken: ct);
        logger.LogInformation("Password changed for user {UserId}", userId);
    }

    // Updates permitted profile fields while enforcing self ownership in the controller/service boundary.
    public async Task<UserResponse> UpdateProsumerAsync(
        string nic,
        string callerId,
        bool isBackoffice,
        UpdateProsumerRequest request,
        CancellationToken ct
    )
    {
        var user = await FindProsumerByNicAsync(nic, ct);
        if (!isBackoffice && user.Id != callerId)
            throw new ApiException(403, "You may only update your own profile.");
        if (request.Email is not null)
        {
            var email = NormalizeEmail(request.Email);
            var conflict = await db
                .Users.Find(x => x.Email == email && x.Id != user.Id)
                .AnyAsync(ct);
            if (conflict)
                throw new ApiException(409, "Email address is already in use.");
            user.Email = email;
        }
        user.FirstName = request.FirstName?.Trim() ?? user.FirstName;
        user.LastName = request.LastName?.Trim() ?? user.LastName;
        user.Phone = request.Phone?.Trim() ?? user.Phone;
        user.Address = request.Address?.Trim() ?? user.Address;
        user.UpdatedAt = DateTime.UtcNow;
        await db.Users.ReplaceOneAsync(x => x.Id == user.Id, user, cancellationToken: ct);
        return user.ToResponse();
    }

    // Records the prosumer's request; Backoffice/Operator later controls deactivation or reactivation.
    public async Task RequestDeactivationAsync(string nic, string callerId, CancellationToken ct)
    {
        var user = await FindProsumerByNicAsync(nic, ct);
        if (user.Id != callerId)
            throw new ApiException(403, "You may only request deactivation of your own account.");
        if (user.AccountStatus != AccountStatus.Active)
            throw new ApiException(409, "Only active accounts can request deactivation.");
        await db.Users.UpdateOneAsync(
            x => x.Id == user.Id,
            Builders<User>
                .Update.Set(x => x.AccountStatus, AccountStatus.DeactivationRequested)
                .Set(x => x.UpdatedAt, DateTime.UtcNow),
            cancellationToken: ct
        );
    }

    // Cancels a pending deactivation request submitted by the prosumer.
    public async Task CancelDeactivationRequestAsync(string nic, string callerId, CancellationToken ct)
    {
        var user = await FindProsumerByNicAsync(nic, ct);
        if (user.Id != callerId)
            throw new ApiException(403, "You may only cancel deactivation of your own account.");
        if (user.AccountStatus != AccountStatus.DeactivationRequested)
            throw new ApiException(409, "Account does not have a pending deactivation request.");
        await db.Users.UpdateOneAsync(
            x => x.Id == user.Id,
            Builders<User>
                .Update.Set(x => x.AccountStatus, AccountStatus.Active)
                .Set(x => x.UpdatedAt, DateTime.UtcNow),
            cancellationToken: ct
        );
    }

    // Lets Backoffice activate pending or deactivated Prosumer accounts.
    public async Task<UserResponse> ReactivateAsync(string nic, CancellationToken ct)
    {
        var user = await FindProsumerByNicAsync(nic, ct);
        if (user.AccountStatus == AccountStatus.Active)
            throw new ApiException(409, "Prosumer is already active.");
        user.AccountStatus = AccountStatus.Active;
        user.UpdatedAt = DateTime.UtcNow;
        await db.Users.ReplaceOneAsync(x => x.Id == user.Id, user, cancellationToken: ct);
        return user.ToResponse();
    }

    // Activates or deactivates a Prosumer account directly from Backoffice administration.
    public async Task<UserResponse> UpdateProsumerStatusAsync(string userId, AccountStatus newStatus, CancellationToken ct)
    {
        var user = await db.Users.Find(x => x.Id == userId && x.Role == UserRole.Prosumer).FirstOrDefaultAsync(ct)
            ?? throw new ApiException(404, "Prosumer was not found.");

        if (newStatus != AccountStatus.Active && newStatus != AccountStatus.Deactivated)
            throw new ApiException(400, "Prosumer status can only be set to Active or Deactivated.");

        if (user.AccountStatus == newStatus)
            throw new ApiException(409, $"Prosumer account is already {newStatus}.");

        user.AccountStatus = newStatus;
        user.UpdatedAt = DateTime.UtcNow;

        await db.Users.ReplaceOneAsync(x => x.Id == user.Id, user, cancellationToken: ct);
        return user.ToResponse();
    }

    // Returns pending and deactivation-requested Prosumer accounts to Backoffice.
    public Task<List<UserResponse>> GetPendingAsync(CancellationToken ct) =>
        db
            .Users.Find(x =>
                x.Role == UserRole.Prosumer
                && (
                    x.AccountStatus == AccountStatus.Pending
                    || x.AccountStatus == AccountStatus.DeactivationRequested
                )
            )
            .ToListAsync(ct)
            .ContinueWith(t => t.Result.Select(x => x.ToResponse()).ToList(), ct);

    // Lists only Prosumer accounts for Backoffice administration with search, status, sort and pagination.
    public async Task<PaginatedResponse<UserResponse>> GetProsumersAsync(
        string? search,
        AccountStatus? status,
        string? sortField,
        string? sortOrder,
        int page,
        int limit,
        CancellationToken ct)
    {
        var filter = Builders<User>.Filter.Eq(x => x.Role, UserRole.Prosumer);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var normalized = Regex.Escape(term);
            filter = Builders<User>.Filter.And(
                filter,
                Builders<User>.Filter.Or(
                    Builders<User>.Filter.Regex(x => x.FirstName, new MongoDB.Bson.BsonRegularExpression(normalized, "i")),
                    Builders<User>.Filter.Regex(x => x.LastName, new MongoDB.Bson.BsonRegularExpression(normalized, "i")),
                    Builders<User>.Filter.Regex(x => x.Email, new MongoDB.Bson.BsonRegularExpression(normalized, "i")),
                    Builders<User>.Filter.Regex(x => x.Nic, new MongoDB.Bson.BsonRegularExpression(normalized, "i"))
                )
            );
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

    // Returns staff accounts (Backoffice and GridOperator) to Backoffice administrators.
    public async Task<List<UserResponse>> GetStaffUsersAsync(CancellationToken ct)
    {
        var staffUsers = await db
            .Users.Find(x => x.Role == UserRole.Backoffice || x.Role == UserRole.GridOperator)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
        return staffUsers.Select(x => x.ToResponse()).ToList();
    }

    // Creates a new Prosumer directly from Backoffice administration.
    public async Task<UserResponse> CreateProsumerAsync(CreateProsumerRequest request, CancellationToken ct)
    {
        var nic = NormalizeNic(request.Nic);
        var email = NormalizeEmail(request.Email);
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            throw new ApiException(400, "Password must be at least 8 characters long.");

        await EnsureAvailableAsync(nic, email, ct);

        var user = new User
        {
            Nic = nic,
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Phone = request.Phone?.Trim(),
            Address = request.Address?.Trim(),
            PasswordHash = passwords.Hash(request.Password),
            Role = UserRole.Prosumer,
            AccountStatus = AccountStatus.Pending,
        };

        await db.Users.InsertOneAsync(user, cancellationToken: ct);
        logger.LogInformation("Backoffice-created Prosumer account for NIC {Nic}", nic);
        return user.ToResponse();
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
        await EnsureAvailableAsync(null, email, ct);
        var user = new User
        {
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PasswordHash = passwords.Hash(request.Password),
            Role = request.Role,
            AccountStatus = AccountStatus.Active,
        };
        await db.Users.InsertOneAsync(user, cancellationToken: ct);
        return user.ToResponse();
    }

    // Retrieves a user or returns a safe not-found response.
    public async Task<User> FindByIdAsync(string id, CancellationToken ct) =>
        await db.Users.Find(x => x.Id == id).FirstOrDefaultAsync(ct)
        ?? throw new ApiException(404, "User was not found.");

    // Retrieves a Prosumer by NIC after normalizing the business identifier.
    public async Task<User> FindProsumerByNicAsync(string nic, CancellationToken ct) =>
        await db.Users.Find(x => x.Nic == NormalizeNic(nic) && x.Role == UserRole.Prosumer).FirstOrDefaultAsync(ct)
        ?? throw new ApiException(404, "Prosumer was not found.");

    // Avoids duplicate business identifiers before insertion while database unique indexes remain authoritative.
    private async Task EnsureAvailableAsync(string? nic, string email, CancellationToken ct)
    {
        if (await db.Users.Find(x => x.Email == email).AnyAsync(ct))
            throw new ApiException(409, "Email address is already in use.");
        if (nic is not null && await db.Users.Find(x => x.Nic == nic).AnyAsync(ct))
            throw new ApiException(409, "NIC is already registered.");
    }

    // Standardizes email comparisons for unique identity checks.
    private static string NormalizeEmail(string value) => value.Trim().ToLowerInvariant();

    // Standardizes NIC without imposing an unconfirmed national NIC format.
    private static string NormalizeNic(string value)
    {
        var result = value.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(result))
            throw new ApiException(400, "NIC is required.");
        return result;
    }
}
