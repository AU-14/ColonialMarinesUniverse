using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Lunge;

[Serializable, NetSerializable]
public sealed class XenoLungePredictedHitEvent(NetEntity target, GameTick lastRealTick, int substep = 0) : EntityEventArgs // CMU14: substep
{
    public readonly NetEntity Target = target;
    public readonly GameTick LastRealTick = lastRealTick;
    public readonly int Substep = substep; // CMU14
}
