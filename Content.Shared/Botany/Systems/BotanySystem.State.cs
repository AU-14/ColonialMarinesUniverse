using Content.Shared.Botany.Items.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Botany.Systems;

public sealed partial class BotanySystem
{
    [SubscribeLocalEvent]
    private void OnProduceGetState(Entity<ProduceComponent> ent, ref ComponentGetState args)
    {
        // These references can outlive their entities; a missing entity is an invalid network reference.
        TryGetNetEntity(ent.Comp.PlantData, out var plantData);

        args.State = new ProduceComponentState
        {
            PlantProtoId = ent.Comp.PlantProtoId,
            PlantData = plantData,
        };
    }

    [SubscribeLocalEvent]
    private void OnProduceHandleState(Entity<ProduceComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not ProduceComponentState state)
            return;

        ent.Comp.PlantProtoId = state.PlantProtoId;
        ent.Comp.PlantData = EnsureEntity<ProduceComponent>(state.PlantData, ent);
    }
}

[Serializable, NetSerializable]
public sealed class ProduceComponentState : ComponentState
{
    public EntProtoId? PlantProtoId { get; init; }
    public NetEntity? PlantData { get; init; }
}
