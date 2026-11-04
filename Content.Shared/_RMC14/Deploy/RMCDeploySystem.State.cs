using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Deploy;

public sealed partial class RMCDeploySystem
{
    [SubscribeLocalEvent]
    private void OnRMCDeployedEntityComponentGetState(Entity<RMCDeployedEntityComponent> ent, ref ComponentGetState args)
    {
        // References can outlive their entities. Build a safe snapshot without mutating gameplay state.
        TryGetNetEntity(ent.Comp.OriginalEntity, out var netOriginalEntity);
        args.State = new RMCDeployedEntityComponentState
        {
            OriginalEntity = netOriginalEntity ?? NetEntity.Invalid,
            SetupIndex = ent.Comp.SetupIndex,
            InShutdown = ent.Comp.InShutdown,
        };
    }

    [SubscribeLocalEvent]
    private void OnRMCDeployedEntityComponentHandleState(Entity<RMCDeployedEntityComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not RMCDeployedEntityComponentState state)
            return;

        ent.Comp.OriginalEntity = EnsureEntity<RMCDeployedEntityComponent>(state.OriginalEntity, ent);
        ent.Comp.SetupIndex = state.SetupIndex;
        ent.Comp.InShutdown = state.InShutdown;
    }
}

[Serializable, NetSerializable]
public sealed class RMCDeployedEntityComponentState : ComponentState
{
    public NetEntity OriginalEntity { get; init; }
    public int SetupIndex { get; init; }
    public bool InShutdown { get; init; }
}
