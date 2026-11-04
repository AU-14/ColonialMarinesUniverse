using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Xenonids.ManageHive.Boons;

public sealed partial class HiveBoonSystem
{
    private void OnBoonsGetState(Entity<HiveBoonsComponent> ent, ref ComponentGetState args)
    {
        // Expired boons may still be recorded in Active after their entities despawn.
        var active = new Dictionary<EntProtoId<HiveBoonDefinitionComponent>, NetEntity>(ent.Comp.Active.Count);
        foreach (var (id, uid) in ent.Comp.Active)
        {
            TryGetNetEntity(uid, out var netEntity);
            active[id] = netEntity ?? NetEntity.Invalid;
        }

        args.State = new HiveBoonsComponentState
        {
            RoyalResin = ent.Comp.RoyalResin,
            RoyalResinMax = ent.Comp.RoyalResinMax,
            UnlockAt = new(ent.Comp.UnlockAt),
            UsedAt = new(ent.Comp.UsedAt),
            Active = active,
            KingAnnounced = ent.Comp.KingAnnounced,
        };
    }

    private void OnBoonsHandleState(Entity<HiveBoonsComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not HiveBoonsComponentState state)
            return;

        ent.Comp.RoyalResin = state.RoyalResin;
        ent.Comp.RoyalResinMax = state.RoyalResinMax;
        ent.Comp.UnlockAt = new(state.UnlockAt);
        ent.Comp.UsedAt = new(state.UsedAt);
        EnsureEntityDictionary<HiveBoonsComponent, EntProtoId<HiveBoonDefinitionComponent>>(state.Active, ent, ent.Comp.Active);
        ent.Comp.KingAnnounced = state.KingAnnounced;
    }
}

[Serializable, NetSerializable]
public sealed class HiveBoonsComponentState : ComponentState
{
    public int RoyalResin { get; init; }
    public int RoyalResinMax { get; init; }
    public Dictionary<EntProtoId<HiveBoonDefinitionComponent>, TimeSpan> UnlockAt { get; init; } = new();
    public Dictionary<EntProtoId<HiveBoonDefinitionComponent>, TimeSpan> UsedAt { get; init; } = new();
    public Dictionary<EntProtoId<HiveBoonDefinitionComponent>, NetEntity> Active { get; init; } = new();
    public bool KingAnnounced { get; init; }
}
