using Content.Shared.CMU14.Explosion;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.OnCollide;
using Content.Shared.Coordinates.Helpers;

namespace Content.Server.CMU14.Explosion;

public sealed class CMUSpawnOnExplosiveTriggerSystem : EntitySystem
{
    [Dependency] private SharedRMCFlammableSystem _flammable = default!;
    [Dependency] private SharedOnCollideSystem _onCollide = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUSpawnOnExplosiveTriggerComponent, CMExplosiveTriggeredEvent>(OnTriggered);
    }

    private void OnTriggered(Entity<CMUSpawnOnExplosiveTriggerComponent> ent, ref CMExplosiveTriggeredEvent args)
    {
        Spawn(ent.Comp.Spawn, _transform.GetMapCoordinates(ent));

        if (ent.Comp.Fire is { } fire)
        {
            var center = _transform.GetMoverCoordinates(ent).SnapToGrid(EntityManager);
            _flammable.SpawnFire(center, fire, _onCollide.SpawnChain(), 0, null, null, out _);
        }
    }
}
