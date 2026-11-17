namespace Content.Shared.CMU14.Fluids;

/// <summary>Allows an active process to reserve tank capacity before more fluid is accepted.</summary>
[ByRefEvent]
public record struct CMUFluidTankRoomEvent(float Available);
