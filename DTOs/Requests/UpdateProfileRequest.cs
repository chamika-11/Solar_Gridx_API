// Smart Solar Microgrid Trading System - self-service profile update request.
using System.ComponentModel.DataAnnotations;
namespace SolarGridX.Api.DTOs.Requests;

public sealed class UpdateProfileRequest
{
    [StringLength(50, ErrorMessage = "First name must be 50 characters or fewer.")]
    public string? FirstName { get; init; }

    [StringLength(50, ErrorMessage = "Last name must be 50 characters or fewer.")]
    public string? LastName { get; init; }

    [Phone(ErrorMessage = "Phone number is not valid.")]
    [StringLength(20, ErrorMessage = "Phone number must be 20 characters or fewer.")]
    public string? Phone { get; init; }

    [StringLength(200, ErrorMessage = "Address must be 200 characters or fewer.")]
    public string? Address { get; init; }
}
