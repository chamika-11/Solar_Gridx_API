// Smart Solar Microgrid Trading System - staff user creation request.
using System.ComponentModel.DataAnnotations;
using SolarGridX.Api.Models.Enums;
namespace SolarGridX.Api.DTOs.Requests;

public sealed class CreateUserRequest
{
    [Required]
    public UserRole Role { get; init; }

    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; init; } = string.Empty;

    [Required]
    public string FirstName { get; init; } = string.Empty;

    [Required]
    public string LastName { get; init; } = string.Empty;

    public string? Phone { get; init; }

    public string? Nic { get; init; }

    public string? Address { get; init; }
}
