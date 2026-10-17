namespace Content.Shared._RMC14.Vehicle.Supply;

/// <summary>Raised on each delivered vehicle and bundle item, including recovered vehicles.</summary>
[ByRefEvent]
public readonly record struct VehicleSupplyDeliveredEvent(EntityUid Lift, EntityUid? Requester);
