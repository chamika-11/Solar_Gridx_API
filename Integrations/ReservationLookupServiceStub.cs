// =============================================================================
// File   : ReservationLookupServiceStub.cs
// Purpose: Placeholder implementation of IReservationLookupService for use
//          until Member 3 delivers the real integration.
//          Throws NotImplementedException deliberately so that any call to
//          DeactivateAsync surfaces a loud, obvious failure rather than silently
//          allowing deactivation with unchecked reservations.
//          To wire the real implementation: replace this registration in
//          Program.cs — no other file needs to change.
// Author : Member 2
// Date   : 2026-09-21
// =============================================================================
namespace SolarGridX.Api.Integrations;

public sealed class ReservationLookupServiceStub : IReservationLookupService
{
    // Placeholder: throws until Member 3 provides the concrete implementation.
    public Task<bool> HasActiveReservationsAsync(string stationId, CancellationToken ct) =>
        throw new NotImplementedException(
            "IReservationLookupService has not been wired to Member 3's reservation domain yet. " +
            "Replace ReservationLookupServiceStub with the real implementation in Program.cs."
        );
}
