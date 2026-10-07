// Smart Solar Microgrid Trading System - entity-to-response mapping helpers.
using SolarGridX.Api.DTOs.Responses;
using SolarGridX.Api.Models;
using SolarGridX.Api.Models.Enums;

namespace SolarGridX.Api.Services;

public static class MappingExtensions
{
    // Maps a user while deliberately omitting password hash and internal security details.
    public static UserResponse ToResponse(this User value) =>
        new(
            value.Id,
            value.Nic,
            value.Email,
            value.FirstName,
            value.LastName,
            value.Phone,
            value.Address,
            value.Role,
            value.AccountStatus
        );

    // Maps a station's GeoJSON location to client-friendly coordinate fields.
    public static StationResponse ToResponse(this SolarStation value) =>
        new(
            value.Id,
            value.StationId,
            value.Name,
            value.Description,
            value.Location.Coordinates[1],
            value.Location.Coordinates[0],
            value.CapacityKwh,
            value.AvailableBatteryStorageSlots,
            value.Status
        );

    public static string ComputeStatus(this EnergyBookingSlot slot)
    {
        if (slot.Status == SlotStatus.Deactivated)
            return "Unavailable";
        if (slot.EndTime < DateTime.UtcNow)
            return "Completed";
        if (slot.AvailableCapacity <= 0)
            return "Reserved";
        return "Available";
    }

    // Maps a slot without exposing database-specific document fields.
    public static SlotResponse ToResponse(this EnergyBookingSlot value, string? stationName = null) =>
        new(
            value.Id,
            value.SlotId,
            value.StationId,
            value.StartTime,
            value.EndTime,
            value.Capacity,
            value.AvailableCapacity,
            value.Status,
            stationName,
            value.ComputeStatus()
        );

    // Maps a reservation and only exposes transaction metadata, never its opaque token.
    public static ReservationResponse ToResponse(this EnergyReservation value) =>
        new(
            value.Id,
            value.ReservationId,
            value.StationId,
            value.SlotId,
            value.ScheduledStartTime,
            value.ScheduledEndTime,
            value.RequestedCapacity,
            value.Status,
            value.Transaction?.TransactionId,
            value.Transaction?.ExpiresAt,
            value.ApprovedAt,
            value.CancelledAt,
            value.CompletedAt,
            value.ProsumerNic,
            value.TransferType,
            value.Notes,
            value.ApprovedByUserId,
            value.CompletedByUserId
        );
}
