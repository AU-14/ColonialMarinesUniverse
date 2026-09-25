namespace Content.Shared._RMC14.Vehicle.Supply;

/// <summary>Allows a non-drivable vehicle to be recovered by the supply lift.</summary>
[RegisterComponent]
public sealed partial class VehicleSupplyStorageComponent : Component;

/// <summary>Lets a vehicle reject storage while occupied or operating.</summary>
[ByRefEvent]
public record struct VehicleSupplyStorageAttemptEvent
{
    public bool Cancelled;
}
