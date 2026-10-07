// =============================================================================
// File   : StationsController.cs
// Purpose: Thin HTTP adapter for the station management domain.
//          Receives HTTP requests, delegates ALL decisions to IStationService,
//          and maps service results to the correct HTTP status codes.
//          Contains no business logic — validation and rules live in the service.
// Author : Member 2
// Date   : 2026-09-21
// =============================================================================
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SolarGridX.Api.Models.Enums;
using SolarGridX.Api.DTOs.Requests;
using SolarGridX.Api.DTOs.Responses;
using SolarGridX.Api.Services;

namespace SolarGridX.Api.Controllers;

[ApiController]
[Route("api/stations")]
public sealed class StationsController(IStationService stationService) : ControllerBase
{
    // Creates a station node; restricted to Backoffice administration.
    [Authorize(Roles = nameof(UserRole.Backoffice))]
    [HttpPost]
    [ProducesResponseType(typeof(StationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<StationResponse>> Create(
        [FromBody] CreateStationRequest request,
        CancellationToken ct)
    {
        var station = await stationService.CreateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, station);
    }

    // Returns active stations by default; Backoffice may pass includeInactive=true.
    [Authorize]
    [HttpGet]
    [ProducesResponseType(typeof(List<StationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<StationResponse>>> List(
        [FromQuery] bool includeInactive,
        CancellationToken ct)
    {
        var activeOnly = !includeInactive || !User.IsInRole(nameof(UserRole.Backoffice));
        var stations = await stationService.GetAllAsync(activeOnly, ct);
        return Ok(stations);
    }

    // Returns an active station to authenticated callers; inactive details are Backoffice-only.
    [Authorize]
    [HttpGet("{stationId}")]
    [ProducesResponseType(typeof(StationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StationResponse>> Get(string stationId, CancellationToken ct)
    {
        var station = await stationService.GetByStationIdAsync(
            stationId,
            User.IsInRole(nameof(UserRole.Backoffice)),
            ct);
        return Ok(station);
    }

    // Updates mutable station details; Backoffice only.
    [Authorize(Roles = nameof(UserRole.Backoffice))]
    [HttpPut("{stationId}")]
    [ProducesResponseType(typeof(StationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<StationResponse>> Update(
        string stationId,
        [FromBody] UpdateStationRequest request,
        CancellationToken ct)
    {
        var station = await stationService.UpdateAsync(stationId, request, ct);
        return Ok(station);
    }

    // Deactivates a station; returns 409 if active reservations block the transition.
    [Authorize(Roles = nameof(UserRole.Backoffice))]
    [HttpPost("{stationId}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Deactivate(string stationId, CancellationToken ct)
    {
        await stationService.DeactivateAsync(stationId, ct);
        return NoContent();
    }

    // Restores a deactivated station back to Active; Backoffice only.
    [Authorize(Roles = nameof(UserRole.Backoffice))]
    [HttpPost("{stationId}/reactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reactivate(string stationId, CancellationToken ct)
    {
        await stationService.ReactivateAsync(stationId, ct);
        return NoContent();
    }

    // Returns active stations within radiusKm kilometres of the supplied coordinate.
    [Authorize]
    [HttpGet("nearby")]
    [ProducesResponseType(typeof(List<StationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<StationResponse>>> Nearby(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromQuery] double radiusKm,
        CancellationToken ct)
    {
        var stations = await stationService.NearbyAsync(latitude, longitude, radiusKm, ct);
        return Ok(stations);
    }
}
