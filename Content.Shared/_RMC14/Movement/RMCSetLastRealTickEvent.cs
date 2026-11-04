using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Movement;

[Serializable, NetSerializable]
public sealed class RMCSetLastRealTickEvent(GameTick tick, int substep = 0) : EntityEventArgs // CMU14: substep
{
    public readonly GameTick Tick = tick;
    public readonly int Substep = substep; // CMU14
}
