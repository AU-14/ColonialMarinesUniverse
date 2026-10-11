using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Elevators;

[Serializable, NetSerializable]
public sealed record CMUElevatorFloorSelectedEvent(NetEntity User, NetEntity TargetMap);

[Serializable, NetSerializable]
public sealed record CMUElevatorDisableConfirmedEvent(NetEntity User, bool Disabled);
