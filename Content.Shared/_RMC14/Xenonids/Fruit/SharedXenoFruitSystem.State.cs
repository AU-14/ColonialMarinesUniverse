using Content.Shared._RMC14.Xenonids.Fruit.Components;
using Content.Shared._RMC14.Xenonids.Fruit.Events;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Xenonids.Fruit;

public sealed partial class SharedXenoFruitSystem
{
    [SubscribeLocalEvent]
    private void OnXenoFruitComponentGetState(Entity<XenoFruitComponent> ent, ref ComponentGetState args)
    {
        // References can outlive their entities. Build a safe snapshot without mutating gameplay state.
        TryGetNetEntity(ent.Comp.Hive, out var netHive);
        TryGetNetEntity(ent.Comp.Planter, out var netPlanter);
        args.State = new XenoFruitComponentState
        {
            State = ent.Comp.State,
            GrowAt = ent.Comp.GrowAt,
            GrowTime = ent.Comp.GrowTime,
            ItemState = ent.Comp.ItemState,
            GrowingState = ent.Comp.GrowingState,
            GrownState = ent.Comp.GrownState,
            EatenState = ent.Comp.EatenState,
            HarvestSound = ent.Comp.HarvestSound,
            Hive = netHive,
            Planter = netPlanter,
            HarvestDelay = ent.Comp.HarvestDelay,
            ConsumeDelay = ent.Comp.ConsumeDelay,
            CanConsumeAtFull = ent.Comp.CanConsumeAtFull,
            Popup = ent.Comp.Popup,
            Color = ent.Comp.Color,
            OutlineColor = ent.Comp.OutlineColor,
            SpentDespawnTime = ent.Comp.SpentDespawnTime,
        };
    }

    [SubscribeLocalEvent]
    private void OnXenoFruitComponentHandleState(Entity<XenoFruitComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not XenoFruitComponentState state)
            return;

        ent.Comp.State = state.State;
        ent.Comp.GrowAt = state.GrowAt;
        ent.Comp.GrowTime = state.GrowTime;
        ent.Comp.ItemState = state.ItemState;
        ent.Comp.GrowingState = state.GrowingState;
        ent.Comp.GrownState = state.GrownState;
        ent.Comp.EatenState = state.EatenState;
        ent.Comp.HarvestSound = state.HarvestSound;
        ent.Comp.Hive = EnsureEntity<XenoFruitComponent>(state.Hive, ent);
        ent.Comp.Planter = EnsureEntity<XenoFruitComponent>(state.Planter, ent);
        ent.Comp.HarvestDelay = state.HarvestDelay;
        ent.Comp.ConsumeDelay = state.ConsumeDelay;
        ent.Comp.CanConsumeAtFull = state.CanConsumeAtFull;
        ent.Comp.Popup = state.Popup;
        ent.Comp.Color = state.Color;
        ent.Comp.OutlineColor = state.OutlineColor;
        ent.Comp.SpentDespawnTime = state.SpentDespawnTime;

        var ev = new XenoFruitStateChangedEvent();
        RaiseLocalEvent(ent, ref ev);
    }
}

[Serializable, NetSerializable]
public sealed class XenoFruitComponentState : ComponentState
{
    public XenoFruitState State { get; init; }
    public TimeSpan? GrowAt { get; init; }
    public TimeSpan GrowTime { get; init; }
    public string ItemState { get; init; } = string.Empty;
    public string GrowingState { get; init; } = string.Empty;
    public string GrownState { get; init; } = string.Empty;
    public string EatenState { get; init; } = string.Empty;
    public SoundSpecifier HarvestSound { get; init; } = default!;
    public NetEntity? Hive { get; init; }
    public NetEntity? Planter { get; init; }
    public TimeSpan HarvestDelay { get; init; }
    public TimeSpan ConsumeDelay { get; init; }
    public bool CanConsumeAtFull { get; init; }
    public LocId Popup { get; init; }
    public Color? Color { get; init; }
    public Color OutlineColor { get; init; }
    public float SpentDespawnTime { get; init; }
}
