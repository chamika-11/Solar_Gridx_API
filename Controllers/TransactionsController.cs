// Smart Solar Microgrid Trading System - QR verification and completion endpoints.
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SolarGridX.Api.Models.Enums;
using SolarGridX.Api.DTOs.Requests;
using SolarGridX.Api.DTOs.Responses;
using SolarGridX.Api.Services;

namespace SolarGridX.Api.Controllers;

[ApiController]
[Route("api/transactions")]
[Authorize(Roles = nameof(UserRole.GridOperator))]
public sealed class TransactionsController(ReservationService reservations) : ControllerBase
{
    // Verifies a raw QR token scanned from the Prosumer's device.
    // Returns operational session details without exposing the token hash or internal identifiers.
    [HttpPost("verify")]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TransactionResponse>> Verify(
        VerifyTransactionRequest request,
        CancellationToken ct)
    {
        var transaction = await reservations.VerifyTransactionAsync(request, ct);
        return Ok(transaction);
    }

    // Completes a verified transaction exactly once using a conditional state transition.
    // Records the operator's identity and timestamps the completion.
    [HttpPost("{transactionId}/complete")]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TransactionResponse>> Complete(
        string transactionId,
        CancellationToken ct)
    {
        var operatorId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var transaction = await reservations.CompleteTransactionAsync(
            transactionId,
            operatorId,
            ct);

        return Ok(transaction);
    }
}
