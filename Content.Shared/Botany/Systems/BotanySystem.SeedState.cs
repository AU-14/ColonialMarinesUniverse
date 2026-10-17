using Content.Shared.Botany.Items.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Botany.Systems;

public sealed partial class BotanySystem
{
    [SubscribeLocalEvent]
    private void OnSeedComponentGetState(Entity<SeedComponent> ent, ref ComponentGetState args)
    {
        // References can outlive their entities. Build a safe snapshot without mutating gameplay state.
        TryGetNetEntity(ent.Comp.PlantData, out var netPlantData);
        args.State = new SeedComponentState
        {
            PlantProtoId = ent.Comp.PlantProtoId,
            PlantData = netPlantData,
            HealthOverride = ent.Comp.HealthOverride,
        };
    }

    [SubscribeLocalEvent]
    private void OnSeedComponentHandleState(Entity<SeedComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not SeedComponentState state)
            return;

        ent.Comp.PlantProtoId = state.PlantProtoId;
        ent.Comp.PlantData = EnsureEntity<SeedComponent>(state.PlantData, ent);
        ent.Comp.HealthOverride = state.HealthOverride;
    }
}

[Serializable, NetSerializable]
public sealed class SeedComponentState : ComponentState
{
    public EntProtoId PlantProtoId { get; init; }
    public NetEntity? PlantData { get; init; }
    public float? HealthOverride { get; init; }
}
