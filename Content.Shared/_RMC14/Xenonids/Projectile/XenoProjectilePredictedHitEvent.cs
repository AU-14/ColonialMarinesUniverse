using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Projectile;

[Serializable, NetSerializable]
public sealed class XenoProjectilePredictedHitEvent(
    int id,
    NetEntity target,
    GameTick lastRealTick,
    GameTick tick,
    int substep,
    GameTick shotTick) : EntityEventArgs
{
    public readonly int Id = id;
    public readonly NetEntity Target = target;
    public readonly GameTick LastRealTick = lastRealTick;
    public readonly GameTick Tick = tick;
    public readonly int Substep = substep;
    public readonly GameTick ShotAtTick = shotTick;
}
