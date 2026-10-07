// =============================================================================
// File   : IReservationLookupService.cs
// Purpose: Cross-member integration seam (Member 2 → Member 3).
//          Defines the minimal contract Member 2 needs from Member 3's
//          reservation domain: "does this station have active reservations?"
//          StationService depends only on this interface (DIP), so Member 3's
//          real implementation can be swapped in via DI with zero changes here.
// Author : Member 2
// Date   : 2026-09-21
// =============================================================================
namespace SolarGridX.Api.Integrations;

public interface IReservationLookupService
{
    // Returns true when the station has at least one Pending or Approved reservation,
    // which must block deactivation to protect committed prosumer bookings.
    Task<bool> HasActiveReservationsAsync(string stationId, CancellationToken ct);
}
