using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Xenonids.Spray;

public sealed partial class XenoSprayAcidSystem
{
    [SubscribeLocalEvent]
    private void OnSplatterGetState(Entity<XenoAcidSplatterComponent> ent, ref ComponentGetState args)
    {
        TryGetNetEntity(ent.Comp.Xeno, out var xeno);
        args.State = new CMUAcidSplatterState(xeno);
    }

    [SubscribeLocalEvent]
    private void OnSplatterHandleState(Entity<XenoAcidSplatterComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is CMUAcidSplatterState state)
            ent.Comp.Xeno = EnsureEntity<XenoAcidSplatterComponent>(state.Xeno, ent);
    }
}

[Serializable, NetSerializable]
public sealed class CMUAcidSplatterState(NetEntity? xeno) : ComponentState
{
    public NetEntity? Xeno { get; } = xeno;
}
