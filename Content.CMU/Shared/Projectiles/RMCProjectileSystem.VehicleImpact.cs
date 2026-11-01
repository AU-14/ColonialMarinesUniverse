using Content.Shared.CMU14.Explosion;
using Content.Shared.Projectiles;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Projectiles;

public sealed partial class RMCProjectileSystem
{
    private EntityUid CMUSpawnVehicleImpact(Entity<SpawnOnTerminateComponent> ent, EntityCoordinates coordinates)
    {
        ComponentRegistry? overrides = null;
        if (TryComp<ProjectileComponent>(ent, out var projectile) &&
            projectile.Shooter is { } shooter && HasComp<VehicleComponent>(shooter))
        {
            // TriggerOnSpawn can explode during MapInit, before SpawnAtPosition returns.
            overrides = new ComponentRegistry
            {
                [CompName.Get<CMUVehicleExplosionSourceComponent>(EntityManager.ComponentFactory)] =
                    new(new CMUVehicleExplosionSourceComponent { Vehicle = shooter }),
            };
        }

        return SpawnAtPosition(ent.Comp.Spawn, coordinates, overrides);
    }
}
