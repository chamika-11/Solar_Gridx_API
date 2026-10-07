// Smart Solar Microgrid Trading System - reservation lifecycle definition.
namespace SolarGridX.Api.Models.Enums;

public enum ReservationStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled,
    Completed,
    Expired
}
