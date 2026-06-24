using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Ape;

[Serializable, NetSerializable]
public sealed class ApeLeapPredictedHitEvent(NetEntity target, GameTick lastRealTick) : EntityEventArgs
{
    public readonly NetEntity Target = target;
    public readonly GameTick LastRealTick = lastRealTick;
}

