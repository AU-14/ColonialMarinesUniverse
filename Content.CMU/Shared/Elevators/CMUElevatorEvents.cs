using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Elevators;

[Serializable, NetSerializable]
public sealed record CMUElevatorMoveConfirmedEvent(NetEntity User);

[Serializable, NetSerializable]
public sealed record CMUElevatorDisableConfirmedEvent(NetEntity User, bool Disabled);
