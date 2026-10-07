// Smart Solar Microgrid Trading System - Backoffice staff user administration endpoints.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SolarGridX.Api.Models.Enums;
using SolarGridX.Api.DTOs.Requests;
using SolarGridX.Api.DTOs.Responses;
using SolarGridX.Api.Services;

namespace SolarGridX.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = nameof(UserRole.Backoffice))]
public sealed class UsersController(UserService users) : ControllerBase
{
    // Existing user-specific endpoints remain here.
}
