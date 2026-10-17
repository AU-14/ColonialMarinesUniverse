using Content.Shared.CMU14.Xenomorphs.Swarm;
using Robust.Client.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Client.CMU14.Xenomorphs.Swarm;

public sealed partial class CMUSwarmClientCollisionSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUSwarmLesserComponent, UpdateIsPredictedEvent>(OnPredictionChanged);
    }

    private void OnPredictionChanged(Entity<CMUSwarmLesserComponent> ent, ref UpdateIsPredictedEvent args)
    {
        // Pulling by the local player enables prediction. Requeue the proxies so world
        // contacts are rebuilt using the new prediction state in the next physics step.
        if (TryComp<PhysicsComponent>(ent, out var body))
            _physics.RegenerateContacts((ent.Owner, body));
    }
}
