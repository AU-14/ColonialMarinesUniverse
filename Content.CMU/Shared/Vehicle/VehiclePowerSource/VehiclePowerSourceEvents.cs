using Content.Shared.Actions;
using Robust.Shared.Serialization;
using Content.Shared.DoAfter;

namespace Content.Shared.CMU14.Vehicle;

[ByRefEvent]
public record struct VehicleFuelDepletedEvent;

[ByRefEvent]
public record struct VehicleRefuelAttemptEvent(EntityUid user, EntityUid fuel_cell);

[Serializable, NetSerializable]
public sealed partial class VehicleRefuelDoAfterEvent : SimpleDoAfterEvent;
