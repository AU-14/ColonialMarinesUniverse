using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Medical.Treatment.Surgery;

// Surgery flow is instantiated on the server only; replication must also run on clients.
public sealed partial class CMUSurgeryInFlightStateSystem : EntitySystem
{
    [SubscribeLocalEvent]
    private void OnCMUSurgeryInFlightComponentGetState(Entity<CMUSurgeryInFlightComponent> ent, ref ComponentGetState args)
    {
        // References can outlive their entities. Build a safe snapshot without mutating gameplay state.
        TryGetNetEntity(ent.Comp.Surgeon, out var netSurgeon);
        args.State = new CMUSurgeryInFlightComponentState
        {
            LeafSurgeryId = ent.Comp.LeafSurgeryId,
            LeafSurgeryDisplayName = ent.Comp.LeafSurgeryDisplayName,
            Surgeon = netSurgeon ?? NetEntity.Invalid,
            SurgeonName = ent.Comp.SurgeonName,
            StartedAt = ent.Comp.StartedAt,
        };
    }

    [SubscribeLocalEvent]
    private void OnCMUSurgeryInFlightComponentHandleState(Entity<CMUSurgeryInFlightComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not CMUSurgeryInFlightComponentState state)
            return;

        ent.Comp.LeafSurgeryId = state.LeafSurgeryId;
        ent.Comp.LeafSurgeryDisplayName = state.LeafSurgeryDisplayName;
        ent.Comp.Surgeon = EnsureEntity<CMUSurgeryInFlightComponent>(state.Surgeon, ent);
        ent.Comp.SurgeonName = state.SurgeonName;
        ent.Comp.StartedAt = state.StartedAt;
    }
}

[Serializable, NetSerializable]
public sealed class CMUSurgeryInFlightComponentState : ComponentState
{
    public string LeafSurgeryId { get; init; } = string.Empty;
    public string LeafSurgeryDisplayName { get; init; } = string.Empty;
    public NetEntity Surgeon { get; init; }
    public string SurgeonName { get; init; } = string.Empty;
    public TimeSpan StartedAt { get; init; }
}
