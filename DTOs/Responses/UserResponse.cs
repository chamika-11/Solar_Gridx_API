// Smart Solar Microgrid Trading System - safe user response.
using SolarGridX.Api.Models.Enums;
namespace SolarGridX.Api.DTOs.Responses;

public sealed record UserResponse(
    string Id,
    string? Nic,
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    string? Address,
    UserRole Role,
    AccountStatus AccountStatus);
