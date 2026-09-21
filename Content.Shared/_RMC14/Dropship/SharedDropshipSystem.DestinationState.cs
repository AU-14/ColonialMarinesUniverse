using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Dropship;

public abstract partial class SharedDropshipSystem
{
    [SubscribeLocalEvent]
    private void OnDropshipDestinationComponentGetState(Entity<DropshipDestinationComponent> ent, ref ComponentGetState args)
    {
        // References can outlive their entities. Build a safe snapshot without mutating gameplay state.
        TryGetNetEntity(ent.Comp.Ship, out var netShip);
        TryGetNetEntity(ent.Comp.ArrivalSoundEntity, out var netArrivalSoundEntity);
        args.State = new DropshipDestinationComponentState
        {
            Ship = netShip,
            AutoRecall = ent.Comp.AutoRecall,
            CanBePrimary = ent.Comp.CanBePrimary,
            LightSearchRadius = ent.Comp.LightSearchRadius,
            ArrivalSoundEntity = netArrivalSoundEntity,
            FactionController = ent.Comp.FactionController,
        };
    }

    [SubscribeLocalEvent]
    private void OnDropshipDestinationComponentHandleState(Entity<DropshipDestinationComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not DropshipDestinationComponentState state)
            return;

        ent.Comp.Ship = EnsureEntity<DropshipDestinationComponent>(state.Ship, ent);
        ent.Comp.AutoRecall = state.AutoRecall;
        ent.Comp.CanBePrimary = state.CanBePrimary;
        ent.Comp.LightSearchRadius = state.LightSearchRadius;
        ent.Comp.ArrivalSoundEntity = EnsureEntity<DropshipDestinationComponent>(state.ArrivalSoundEntity, ent);
        ent.Comp.FactionController = state.FactionController;
    }
}

[Serializable, NetSerializable]
public sealed class DropshipDestinationComponentState : ComponentState
{
    public NetEntity? Ship { get; init; }
    public bool AutoRecall { get; init; }
    public bool CanBePrimary { get; init; }
    public int LightSearchRadius { get; init; }
    public NetEntity? ArrivalSoundEntity { get; init; }
    public string FactionController { get; init; } = string.Empty;
}
