using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ForceOnForce;

/// <summary>Raised after a faction hijack has launched and its ownership is recorded.</summary>
[ByRefEvent]
public readonly record struct ForceOnForceHijackStartedEvent(EntityUid Hijacker, EntityUid Destination);

[Serializable, NetSerializable]
public sealed class ForceOnForceHijackJoinState(bool attacking) : EuiStateBase
{
    public bool Attacking = attacking;
}

[Serializable, NetSerializable]
public sealed class ForceOnForceHijackJoinMessage(bool join) : EuiMessageBase
{
    public bool Join = join;
}
