using Content.Shared.CMU14.Explosion;
using Content.Shared.Projectiles;
using Content.Shared.Vehicle.Components;

namespace Content.Server.Explosion.EntitySystems;

public sealed partial class ExplosionSystem
{
    private EntityUid? GetCMUProjectileVehicle(EntityUid? cause)
    {
        if (TryComp<CMUVehicleExplosionSourceComponent>(cause, out var impact))
            return impact.Vehicle;

        return TryComp<ProjectileComponent>(cause, out var projectile) &&
               HasComp<VehicleComponent>(projectile.Shooter)
            ? projectile.Shooter
            : null;
    }
}
