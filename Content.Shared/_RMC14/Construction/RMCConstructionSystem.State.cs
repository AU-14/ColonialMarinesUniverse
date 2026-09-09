using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Construction;

public sealed partial class RMCConstructionSystem
{
    private void OnPreventCollideGetState(Entity<RMCConstructionPreventCollideComponent> ent, ref ComponentGetState args)
    {
        TryGetNetEntity(ent.Comp.Target, out var target);
        args.State = new RMCConstructionPreventCollideComponentState(ent.Comp.Range, target);
    }

    private void OnPreventCollideHandleState(Entity<RMCConstructionPreventCollideComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not RMCConstructionPreventCollideComponentState state)
            return;

        ent.Comp.Range = state.Range;
        ent.Comp.Target = EnsureEntity<RMCConstructionPreventCollideComponent>(state.Target, ent);
    }
}

[Serializable, NetSerializable]
public sealed class RMCConstructionPreventCollideComponentState(float range, NetEntity? target) : ComponentState
{
    public float Range { get; } = range;
    public NetEntity? Target { get; } = target;
}
