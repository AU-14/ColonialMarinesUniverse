namespace Content.Shared._RMC14.Vehicle;

[ByRefEvent]
public readonly record struct CMUVehicleOccupantEjectedEvent(EntityUid Vehicle, bool CookOff);
