// Smart Solar Microgrid Trading System - Prosumer lifecycle endpoints.
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SolarGridX.Api.Models.Enums;
using SolarGridX.Api.DTOs.Requests;
using SolarGridX.Api.DTOs.Responses;
using SolarGridX.Api.Services;

namespace SolarGridX.Api.Controllers;

[ApiController]
[Route("api/prosumers")]
[Authorize]
public sealed class ProsumersController(UserService users) : ControllerBase
{
    // Lists only Prosumer accounts for Backoffice and Grid Operator administration with search, status and pagination controls.
    [Authorize(Roles = $"{nameof(UserRole.Backoffice)},{nameof(UserRole.GridOperator)}")]
    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<UserResponse>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] AccountStatus? status,
        [FromQuery] string? sortField = "name",
        [FromQuery] string? sortOrder = "asc",
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        CancellationToken ct = default)
    {
        var prosumers = await users.GetProsumersAsync(search, status, sortField, sortOrder, page, limit, ct);
        return Ok(prosumers);
    }

    // Creates a new Prosumer account directly from Backoffice or Grid Operator administration.
    [Authorize(Roles = $"{nameof(UserRole.Backoffice)},{nameof(UserRole.GridOperator)}")]
    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(CreateProsumerRequest request, CancellationToken ct)
    {
        var user = await users.CreateProsumerAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, user);
    }

    // Returns a Prosumer profile after Backoffice, Grid Operator or self-ownership authorization.
    [HttpGet("{nic}")]
    public async Task<ActionResult<UserResponse>> Get(string nic, CancellationToken ct)
    {
        var user = await users.FindProsumerByNicAsync(nic, ct);
        var isStaff = User.IsInRole(nameof(UserRole.Backoffice)) || User.IsInRole(nameof(UserRole.GridOperator));
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!isStaff && user.Id != currentUserId)
        {
            return Forbid();
        }

        return Ok(user.ToResponse());
    }

    // Updates a profile only for its owner, Backoffice, or Grid Operator.
    [HttpPut("{nic}")]
    public async Task<ActionResult<UserResponse>> Update(string nic, UpdateProsumerRequest request, CancellationToken ct)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var isStaff = User.IsInRole(nameof(UserRole.Backoffice)) || User.IsInRole(nameof(UserRole.GridOperator));
        var user = await users.UpdateProsumerAsync(
            nic,
            currentUserId,
            isStaff,
            request,
            ct);

        return Ok(user);
    }

    // Records a self-service deactivation request for later administrative handling.
    [Authorize]
    [HttpPost("{nic}/deactivation-request")]
    public async Task<IActionResult> RequestDeactivation(string nic, CancellationToken ct)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await users.RequestDeactivationAsync(nic, currentUserId, ct);
        return NoContent();
    }

    // Cancels a pending self-service deactivation request.
    [Authorize]
    [HttpPost("{nic}/cancel-deactivation")]
    public async Task<IActionResult> CancelDeactivation(string nic, CancellationToken ct)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await users.CancelDeactivationRequestAsync(nic, currentUserId, ct);
        return NoContent();
    }

    // Updates a Prosumer account status directly from Backoffice administration or Grid Operator.
    [Authorize(Roles = $"{nameof(UserRole.Backoffice)},{nameof(UserRole.GridOperator)}")]
    [HttpPatch("{userId}/status")]
    public async Task<ActionResult<UserResponse>> UpdateStatus(
        string userId,
        [FromBody] UpdateStaffStatusRequest request,
        CancellationToken ct)
    {
        if (!Enum.TryParse<AccountStatus>(request.AccountStatus, true, out var status))
            return BadRequest("Status must be Active or Deactivated.");

        var user = await users.UpdateProsumerStatusAsync(userId, status, ct);
        return Ok(user);
    }

    // Lists Prosumer accounts requiring Backoffice or Grid Operator action.
    [Authorize(Roles = $"{nameof(UserRole.Backoffice)},{nameof(UserRole.GridOperator)}")]
    [HttpGet("pending")]
    public async Task<ActionResult<List<UserResponse>>> Pending(CancellationToken ct)
    {
        var pendingUsers = await users.GetPendingAsync(ct);
        return Ok(pendingUsers);
    }

    // Reactivates a non-active Prosumer account through Backoffice or Grid Operator.
    [Authorize(Roles = $"{nameof(UserRole.Backoffice)},{nameof(UserRole.GridOperator)}")]
    [HttpPost("{nic}/reactivate")]
    public async Task<ActionResult<UserResponse>> Reactivate(string nic, CancellationToken ct)
    {
        var user = await users.ReactivateAsync(nic, ct);
        return Ok(user);
    }
}
