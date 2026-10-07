// Smart Solar Microgrid Trading System - role-scoped dashboard response.
namespace SolarGridX.Api.DTOs.Responses;

public sealed record DashboardSummaryResponse(
    int PendingReservations,
    int ApprovedFutureReservations,
    int CurrentReservations,
    int CompletedReservations);
