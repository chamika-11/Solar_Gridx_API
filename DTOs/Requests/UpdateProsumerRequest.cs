// Smart Solar Microgrid Trading System - Prosumer profile update request.
using System.ComponentModel.DataAnnotations;
namespace SolarGridX.Api.DTOs.Requests;

public sealed class UpdateProsumerRequest
{
    [StringLength(100)]
    public string? FirstName { get; init; }

    [StringLength(100)]
    public string? LastName { get; init; }

    [EmailAddress]
    public string? Email { get; init; }

    public string? Phone { get; init; }

    public string? Address { get; init; }
}
