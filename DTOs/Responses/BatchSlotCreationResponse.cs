// Smart Solar Microgrid Trading System - batch slot creation response.
namespace SolarGridX.Api.DTOs.Responses;

public sealed record BatchSlotCreationResponse(
    int TotalCreated,
    int TotalSkipped,
    List<SlotResponse> CreatedSlots,
    List<string> Messages
);
