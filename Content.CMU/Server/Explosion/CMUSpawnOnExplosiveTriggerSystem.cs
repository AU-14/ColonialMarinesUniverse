using Content.Shared.CMU14.Explosion;
using Content.Shared._RMC14.Explosion;

namespace Content.Server.CMU14.Explosion;

public sealed class CMUSpawnOnExplosiveTriggerSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUSpawnOnExplosiveTriggerComponent, CMExplosiveTriggeredEvent>(OnTriggered);
    }

    private void OnTriggered(Entity<CMUSpawnOnExplosiveTriggerComponent> ent, ref CMExplosiveTriggeredEvent args)
    {
        Spawn(ent.Comp.Spawn, _transform.GetMapCoordinates(ent));
    }
}
