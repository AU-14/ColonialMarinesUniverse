using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Threats.Mobs.Ape;

[Serializable, NetSerializable]
public sealed class ApeLeapPredictedHitEvent(NetEntity target, GameTick lastRealTick, int substep = 0) : EntityEventArgs
{
    public readonly GameTick LastRealTick = lastRealTick;
    public readonly NetEntity Target = target;
    public readonly int Substep = substep;
}
