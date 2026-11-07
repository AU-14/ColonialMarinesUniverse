namespace Content.Shared.CMU14.Vehicle;

/// <summary>
/// Raised on a vehicle, server side, when it hits <see cref="Target"/> hard: a crash into an obstacle
/// or another vehicle, or running someone down, at crash speed.
/// </summary>
[ByRefEvent]
public readonly record struct CMUVehicleImpactEvent(EntityUid Target);

/// <summary>
/// Raised on a vehicle, server side, when its hull or one of its parts, <see cref="Hardpoint"/>, loses
/// <see cref="Amount"/> integrity.
/// </summary>
[ByRefEvent]
public readonly record struct CMUVehicleHardpointDamagedEvent(EntityUid Hardpoint, float Amount);
