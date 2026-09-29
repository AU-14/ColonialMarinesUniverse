using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Xenonids.Weeds;

public abstract partial class SharedXenoWeedsSystem
{
    [SubscribeLocalEvent]
    private void OnXenoWeedsComponentGetState(Entity<XenoWeedsComponent> ent, ref ComponentGetState args)
    {
        // References can outlive their entities. Build a safe snapshot without mutating gameplay state.
        TryGetNetEntity(ent.Comp.Source, out var netSource);
        var netSpread = new List<NetEntity>();
        foreach (var uid in ent.Comp.Spread)
        {
            if (TryGetNetEntity(uid, out var net) && net != NetEntity.Invalid)
                netSpread.Add(net.Value);
        }

        var netLocalWeeded = new List<NetEntity>();
        foreach (var uid in ent.Comp.LocalWeeded)
        {
            if (TryGetNetEntity(uid, out var net) && net != NetEntity.Invalid)
                netLocalWeeded.Add(net.Value);
        }

        var netWeedboundStructures = new List<NetEntity>();
        foreach (var uid in ent.Comp.WeedboundStructures)
        {
            if (TryGetNetEntity(uid, out var net) && net != NetEntity.Invalid)
                netWeedboundStructures.Add(net.Value);
        }

        args.State = new XenoWeedsComponentState
        {
            HealOnStopSpreading = ent.Comp.HealOnStopSpreading,
            HasHealed = ent.Comp.HasHealed,
            IsSource = ent.Comp.IsSource,
            Source = netSource,
            Spread = netSpread,
            LocalWeeded = netLocalWeeded,
            MinRandomDelete = ent.Comp.MinRandomDelete,
            MaxRandomDelete = ent.Comp.MaxRandomDelete,
            SpreadsOnSemiWeedable = ent.Comp.SpreadsOnSemiWeedable,
            FruitGrowthMultiplier = ent.Comp.FruitGrowthMultiplier,
            Level = ent.Comp.Level,
            BlockOtherWeeds = ent.Comp.BlockOtherWeeds,
            WeedboundStructures = netWeedboundStructures,
        };
    }

    [SubscribeLocalEvent]
    private void OnXenoWeedsComponentHandleState(Entity<XenoWeedsComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not XenoWeedsComponentState state)
            return;

        ent.Comp.HealOnStopSpreading = state.HealOnStopSpreading;
        ent.Comp.HasHealed = state.HasHealed;
        ent.Comp.IsSource = state.IsSource;
        ent.Comp.Source = EnsureEntity<XenoWeedsComponent>(state.Source, ent);
        EnsureEntityList<XenoWeedsComponent>(state.Spread, ent, ent.Comp.Spread);
        EnsureEntityList<XenoWeedsComponent>(state.LocalWeeded, ent, ent.Comp.LocalWeeded);
        ent.Comp.MinRandomDelete = state.MinRandomDelete;
        ent.Comp.MaxRandomDelete = state.MaxRandomDelete;
        ent.Comp.SpreadsOnSemiWeedable = state.SpreadsOnSemiWeedable;
        ent.Comp.FruitGrowthMultiplier = state.FruitGrowthMultiplier;
        ent.Comp.Level = state.Level;
        ent.Comp.BlockOtherWeeds = state.BlockOtherWeeds;
        EnsureEntityList<XenoWeedsComponent>(state.WeedboundStructures, ent, ent.Comp.WeedboundStructures);
    }
}

[Serializable, NetSerializable]
public sealed class XenoWeedsComponentState : ComponentState
{
    public DamageSpecifier HealOnStopSpreading { get; init; } = new();
    public bool HasHealed { get; init; }
    public bool IsSource { get; init; }
    public NetEntity? Source { get; init; }
    public List<NetEntity> Spread { get; init; } = new();
    public List<NetEntity> LocalWeeded { get; init; } = new();
    public TimeSpan MinRandomDelete { get; init; }
    public TimeSpan MaxRandomDelete { get; init; }
    public bool SpreadsOnSemiWeedable { get; init; }
    public float FruitGrowthMultiplier { get; init; }
    public int Level { get; init; }
    public bool BlockOtherWeeds { get; init; }
    public List<NetEntity> WeedboundStructures { get; init; } = new();
}
