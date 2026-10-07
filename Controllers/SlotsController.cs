// Smart Solar Microgrid Trading System - energy booking slot endpoints.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SolarGridX.Api.Models.Enums;
using SolarGridX.Api.DTOs.Requests;
using SolarGridX.Api.DTOs.Responses;
using SolarGridX.Api.Services;

namespace SolarGridX.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class SlotsController(StationSlotService slots) : ControllerBase
{
    // Creates a slot for Backoffice or operational Grid Operator management.
    [Authorize(Roles = nameof(UserRole.Backoffice) + "," + nameof(UserRole.GridOperator))]
    [HttpPost("slots")]
    public async Task<ActionResult<SlotResponse>> Create(
        CreateSlotRequest request,
        CancellationToken ct)
    {
        var slot = await slots.CreateSlotAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, slot);
    }

    // Creates recurring energy booking slots for staff management.
    [Authorize(Roles = nameof(UserRole.Backoffice) + "," + nameof(UserRole.GridOperator))]
    [HttpPost("slots/recurring")]
    public async Task<ActionResult<BatchSlotCreationResponse>> CreateRecurring(
        CreateRecurringSlotsRequest request,
        CancellationToken ct)
    {
        var result = await slots.CreateRecurringSlotsAsync(request, ct);
        return Ok(result);
    }

    // Lists slots, restricting Prosumer views to bookable availability.
    [Authorize]
    [HttpGet("slots")]
    public async Task<ActionResult<List<SlotResponse>>> List(
        [FromQuery] string? stationId,
        [FromQuery] bool includeInactive,
        CancellationToken ct)
    {
        var activeOnly = !includeInactive || !IsStaff;
        var slotList = await slots.GetSlotsAsync(stationId, activeOnly, ct);
        return Ok(slotList);
    }

    // Retrieves a specific booking slot.
    [Authorize]
    [HttpGet("slots/{slotId}")]
    public async Task<ActionResult<SlotResponse>> Get(string slotId, CancellationToken ct)
    {
        var slot = await slots.GetSlotAsync(slotId, ct);
        return Ok(slot);
    }

    // Returns slots associated with a station.
    [Authorize]
    [HttpGet("stations/{stationId}/slots")]
    public async Task<ActionResult<List<SlotResponse>>> ByStation(
        string stationId,
        [FromQuery] bool includeInactive,
        CancellationToken ct)
    {
        var activeOnly = !includeInactive || !IsStaff;
        var slotList = await slots.GetSlotsAsync(stationId, activeOnly, ct);
        return Ok(slotList);
    }

    // Updates slot timing or capacity under staff authorization.
    [Authorize(Roles = nameof(UserRole.Backoffice) + "," + nameof(UserRole.GridOperator))]
    [HttpPut("slots/{slotId}")]
    public async Task<ActionResult<SlotResponse>> Update(
        string slotId,
        UpdateSlotRequest request,
        CancellationToken ct)
    {
        var slot = await slots.UpdateSlotAsync(slotId, request, ct);
        return Ok(slot);
    }

    // Deactivates an unused slot through staff administration.
    [Authorize(Roles = nameof(UserRole.Backoffice) + "," + nameof(UserRole.GridOperator))]
    [HttpPost("slots/{slotId}/deactivate")]
    public async Task<IActionResult> Deactivate(string slotId, CancellationToken ct)
    {
        await slots.DeactivateSlotAsync(slotId, ct);
        return NoContent();
    }
    
    // Distinguishes staff role claims from NIC-keyed Prosumer accounts.
    private bool IsStaff
    {
        get
        {
            return User.IsInRole(nameof(UserRole.Backoffice))
                || User.IsInRole(nameof(UserRole.GridOperator));
        }
    }
}
