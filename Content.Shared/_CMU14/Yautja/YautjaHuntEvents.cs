using Robust.Shared.Serialization;
using Content.Shared._RMC14.Dialog;
using Robust.Shared.Map;

namespace Content.Shared._CMU14.Yautja;

[Serializable, NetSerializable]
public sealed record YautjaHuntingGroundSelectedEvent(NetEntity User, string Id);

[Serializable, NetSerializable]
public sealed record YautjaHuntCallSelectedEvent(NetEntity User, string Id);

[Serializable, NetSerializable]
public sealed record YautjaHuntConsoleDialogCancelledEvent(NetEntity User);

[Serializable, NetSerializable]
public sealed record YautjaHuntEscapeActionSelectedEvent(NetEntity User, YautjaHuntEscapeAction Action);

[Serializable, NetSerializable]
public sealed record YautjaPreserveEscapeChoiceEvent(NetEntity User, bool Escape);

[Serializable, NetSerializable]
public sealed record YautjaYoungbloodDeployConfirmedEvent(NetEntity User);

[Serializable, NetSerializable]
public sealed record YautjaYoungbloodExecutionTargetSelectedEvent(NetEntity User, NetEntity Target);

[Serializable, NetSerializable]
public sealed record YautjaYoungbloodExecutionReasonEvent(NetEntity User, NetEntity Target, string Message = "") : DialogInputEvent(Message);

[Serializable, NetSerializable]
public sealed record YautjaRelayBeaconNameDestinationEvent(NetEntity User, NetCoordinates Coordinates, string Message = "") : DialogInputEvent(Message);

[Serializable, NetSerializable]
public enum YautjaHuntEscapeAction : byte
{
    Open,
    Close,
}
