namespace Content.Server._RMC14.Vehicle;

/// <summary>Raised on vended equipment by the existing vehicle-vendor event owner.</summary>
[ByRefEvent]
public readonly record struct VehicleSupplyVendedEvent(EntityUid User);
