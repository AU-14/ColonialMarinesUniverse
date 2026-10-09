using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Weapons;

public sealed class CMUPredictedProjectileStateSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<PredictedProjectileServerComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<PredictedProjectileServerComponent, ComponentHandleState>(OnHandleState);
    }

    private void OnGetState(Entity<PredictedProjectileServerComponent> ent, ref ComponentGetState args)
    {
        TryGetNetEntity(ent.Comp.ClientEnt, out var client);
        args.State = new CMUPredictedProjectileState(ent.Comp.ClientId, client);
    }

    private void OnHandleState(Entity<PredictedProjectileServerComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not CMUPredictedProjectileState state)
            return;

        ent.Comp.ClientId = state.ClientId;
        ent.Comp.ClientEnt = EnsureEntity<PredictedProjectileServerComponent>(state.ClientEnt, ent);
    }
}

[Serializable, NetSerializable]
public sealed class CMUPredictedProjectileState(int clientId, NetEntity? clientEnt) : ComponentState
{
    public int ClientId { get; } = clientId;
    public NetEntity? ClientEnt { get; } = clientEnt;
}
