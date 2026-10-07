// Smart Solar Microgrid Trading System - reservation state and query endpoints.
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SolarGridX.Api.Models.Enums;
using SolarGridX.Api.DTOs.Requests;
using SolarGridX.Api.DTOs.Responses;
using SolarGridX.Api.Services;

namespace SolarGridX.Api.Controllers;

[ApiController]
[Route("api/reservations")]
[Authorize]
public sealed class ReservationsController(ReservationService reservations) : ControllerBase
{
    // Creates a pending reservation for the authenticated active Prosumer.
    // Any authenticated user may call this endpoint; the service enforces the Prosumer role and active status.
    [HttpPost]
    [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ReservationResponse>> Create(
        [FromBody] CreateReservationRequest request,
        CancellationToken ct)
    {
        var reservation = await reservations.CreateAsync(CurrentId, request, ct);
        return StatusCode(StatusCodes.Status201Created, reservation);
    }

    // Retrieves pending reservations directly without query string parameters.
    [HttpGet("pending")]
    [ProducesResponseType(typeof(List<ReservationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<ReservationResponse>>> Pending(CancellationToken ct)
    {
        var reservationList = await reservations.ListAsync(
            CurrentId,
            CurrentRole,
            "pending",
            null,
            null,
            null,
            null,
            null,
            ct);

        return Ok(reservationList);
    }

    // Retrieves booking history directly without query string parameters.
    [HttpGet("history")]
    [ProducesResponseType(typeof(List<ReservationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<ReservationResponse>>> History(CancellationToken ct)
    {
        var reservationList = await reservations.ListAsync(
            CurrentId,
            CurrentRole,
            "history",
            null,
            null,
            null,
            null,
            null,
            ct);

        return Ok(reservationList);
    }

    // Retrieves reservations by Prosumer NIC directly.
    // Prosumers are restricted to their own NIC; staff roles can look up any NIC.
    [HttpGet("prosumer/{nic}")]
    [HttpGet("~/api/prosumers/{nic}/reservations")]
    [ProducesResponseType(typeof(List<ReservationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<ReservationResponse>>> ByProsumerNic(
        string nic,
        CancellationToken ct)
    {
        var reservationList = await reservations.ListAsync(
            CurrentId,
            CurrentRole,
            null,
            null,
            null,
            nic,
            null,
            null,
            ct);

        return Ok(reservationList);
    }

    // Retrieves one reservation by its public identifier.
    // Prosumers may only access their own reservations; staff roles access any.
    [HttpGet("{reservationId}")]
    [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReservationResponse>> Get(
        string reservationId,
        CancellationToken ct)
    {
        var reservation = await reservations.GetAsync(
            reservationId,
            CurrentId,
            CurrentRole,
            ct);

        return Ok(reservation);
    }

    // Supports role-scoped reservation listing with optional filters.
    // Prosumer results are automatically scoped to the caller.
    // Staff may filter by NIC, station, status, date range, or group (pending / current / history).
    [HttpGet]
    [ProducesResponseType(typeof(List<ReservationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<ReservationResponse>>> List(
        [FromQuery] string? group,
        [FromQuery] ReservationStatus? status,
        [FromQuery] string? stationId,
        [FromQuery] string? nic,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var reservationList = await reservations.ListAsync(
            CurrentId,
            CurrentRole,
            group,
            status,
            stationId,
            nic,
            from,
            to,
            ct);

        return Ok(reservationList);
    }

    // Updates a reservation's slot, station, or requested capacity.
    // Requires at least 12 hours' notice before the scheduled start time.
    // Prosumers may only update their own reservations; staff roles may update any.
    [HttpPut("{reservationId}")]
    [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ReservationResponse>> Update(
        string reservationId,
        [FromBody] UpdateReservationRequest request,
        CancellationToken ct)
    {
        var reservation = await reservations.UpdateAsync(
            reservationId,
            CurrentId,
            CurrentRole,
            request,
            ct);

        return Ok(reservation);
    }

    // Cancels a reservation and restores its capacity to the slot.
    // Requires at least 12 hours' notice before the scheduled start time.
    // Prosumers may only cancel their own reservations; Grid Operators may assist operationally.
    [HttpPost("{reservationId}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(string reservationId, CancellationToken ct)
    {
        await reservations.CancelAsync(reservationId, CurrentId, CurrentRole, ct);
        return NoContent();
    }

    // Approves a pending reservation and creates an opaque QR transaction token.
    // Restricted to Grid Operator and Backoffice roles.
    [Authorize(Roles = nameof(UserRole.GridOperator) + "," + nameof(UserRole.Backoffice))]
    [HttpPost("{reservationId}/approve")]
    [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservationResponse>> Approve(
        string reservationId,
        CancellationToken ct)
    {
        var reservation = await reservations.ApproveAsync(reservationId, CurrentId, ct);
        return Ok(reservation);
    }

    // Rejects a pending reservation and releases its held capacity back to the slot.
    // Restricted to Grid Operator and Backoffice roles.
    [Authorize(Roles = nameof(UserRole.GridOperator) + "," + nameof(UserRole.Backoffice))]
    [HttpPost("{reservationId}/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(string reservationId, CancellationToken ct)
    {
        await reservations.RejectAsync(reservationId, ct);
        return NoContent();
    }

    // Issues a fresh opaque QR token to the owning Prosumer for an approved reservation.
    // Each call rotates the stored token hash, invalidating the previous token.
    [HttpPost("{reservationId}/transaction-token")]
    [ProducesResponseType(typeof(QrTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<QrTokenResponse>> TransactionToken(
        string reservationId,
        CancellationToken ct)
    {
        var token = await reservations.IssueQrTokenAsync(reservationId, CurrentId, ct);
        return Ok(token);
    }

    // Resolves the authenticated subject claim once and uses it across all reservation operations.
    private string CurrentId
    {
        get
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Authenticated subject is missing.");
        }
    }

    // Resolves the required role assigned to every authenticated user.
    private UserRole CurrentRole
    {
        get
        {
            return Enum.TryParse<UserRole>(User.FindFirstValue(ClaimTypes.Role), out var role)
                ? role
                : throw new InvalidOperationException("Authenticated user is missing a valid role.");
        }
    }
}
