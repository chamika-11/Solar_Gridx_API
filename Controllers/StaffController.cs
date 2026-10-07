// Smart Solar Microgrid Trading System - staff management endpoints.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SolarGridX.Api.DTOs.Requests;
using SolarGridX.Api.DTOs.Responses;
using SolarGridX.Api.Models.Enums;
using SolarGridX.Api.Services;

namespace SolarGridX.Api.Controllers;

[ApiController]
[Route("api/staff")]
[Authorize(Roles = nameof(UserRole.Backoffice))]
public sealed class StaffController(StaffService staffService) : ControllerBase
{
    // Retrieves all Backoffice and Grid Operator staff accounts with search, filtering and pagination.
    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<UserResponse>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] UserRole? role,
        [FromQuery] AccountStatus? status,
        [FromQuery] string? sortField = "name",
        [FromQuery] string? sortOrder = "asc",
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        CancellationToken ct = default)
    {
        var staffList = await staffService.GetStaffUsersAsync(search, role, status, sortField, sortOrder, page, limit, ct);
        return Ok(staffList);
    }

    // Creates an active Backoffice or Grid Operator account.
    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(
        CreateUserRequest request,
        CancellationToken ct)
    {
        var user = await staffService.CreateStaffAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, user);
    }

    // Activates or deactivates a staff account through Backoffice administration.
    [HttpPatch("{userId}/status")]
    public async Task<ActionResult<UserResponse>> UpdateStatus(
        string userId,
        [FromBody] UpdateStaffStatusRequest request,
        CancellationToken ct)
    {
        if (!Enum.TryParse<AccountStatus>(request.AccountStatus, true, out var status))
            return BadRequest("Status must be Active or Deactivated.");

        var user = await staffService.UpdateStaffStatusAsync(userId, status, ct);
        return Ok(user);
    }
}
