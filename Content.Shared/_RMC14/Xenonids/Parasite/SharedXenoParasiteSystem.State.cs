using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Xenonids.Parasite;

public abstract partial class SharedXenoParasiteSystem
{
    private void OnParasiteGetState(Entity<XenoParasiteComponent> ent, ref ComponentGetState args)
    {
        // These references can outlive their entities; a missing entity is an invalid network reference.
        TryGetNetEntity(ent.Comp.InfectedVictim, out var infectedVictim);

        args.State = new XenoParasiteComponentState
        {
            ManualAttachDelay = ent.Comp.ManualAttachDelay,
            SelfAttachDelay = ent.Comp.SelfAttachDelay,
            ParalyzeTime = ent.Comp.ParalyzeTime,
            InfectRange = ent.Comp.InfectRange,
            InfectedVictim = infectedVictim,
            FallOffDelay = ent.Comp.FallOffDelay,
            FallOffAt = ent.Comp.FallOffAt,
            FellOff = ent.Comp.FellOff,
            BaseTemporaryCollisionMask = ent.Comp.BaseTemporaryCollisionMask,
            LeapCollisionActive = ent.Comp.LeapCollisionActive,
            ThrownCollisionActive = ent.Comp.ThrownCollisionActive,
        };
    }

    private void OnParasiteHandleState(Entity<XenoParasiteComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not XenoParasiteComponentState state)
            return;

        ent.Comp.ManualAttachDelay = state.ManualAttachDelay;
        ent.Comp.SelfAttachDelay = state.SelfAttachDelay;
        ent.Comp.ParalyzeTime = state.ParalyzeTime;
        ent.Comp.InfectRange = state.InfectRange;
        ent.Comp.InfectedVictim = EnsureEntity<XenoParasiteComponent>(state.InfectedVictim, ent);
        ent.Comp.FallOffDelay = state.FallOffDelay;
        ent.Comp.FallOffAt = state.FallOffAt;
        ent.Comp.FellOff = state.FellOff;
        ent.Comp.BaseTemporaryCollisionMask = state.BaseTemporaryCollisionMask;
        ent.Comp.LeapCollisionActive = state.LeapCollisionActive;
        ent.Comp.ThrownCollisionActive = state.ThrownCollisionActive;
    }
}

[Serializable, NetSerializable]
public sealed class XenoParasiteComponentState : ComponentState
{
    public TimeSpan ManualAttachDelay { get; init; }
    public TimeSpan SelfAttachDelay { get; init; }
    public TimeSpan ParalyzeTime { get; init; }
    public float InfectRange { get; init; }
    public NetEntity? InfectedVictim { get; init; }
    public TimeSpan FallOffDelay { get; init; }
    public TimeSpan? FallOffAt { get; init; }
    public bool FellOff { get; init; }
    public int BaseTemporaryCollisionMask { get; init; }
    public bool LeapCollisionActive { get; init; }
    public bool ThrownCollisionActive { get; init; }
}
