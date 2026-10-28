using Content.Shared._RMC14.Projectiles.Penetration;
using Robust.Shared.Map;

// ReSharper disable CheckNamespace
namespace Content.Shared._RMC14.Projectiles;

public sealed partial class RMCProjectileSystem
{
    // falloff, max range and penetration all grab their origin on map init, which is wrong for
    // shots that spawn somewhere other than where they were fired from. accuracy is left alone,
    // it also raycasts walls from its origin and a moved one ends up behind a wall
    public void SetShotOrigin(EntityUid projectile, EntityCoordinates origin)
    {
        if (TryComp(projectile, out RMCProjectileDamageFalloffComponent? falloff))
        {
            falloff.ShotFrom = origin;
            Dirty(projectile, falloff);
        }

        if (TryComp(projectile, out ProjectileMaxRangeComponent? maxRange))
        {
            maxRange.Origin = origin;
            Dirty(projectile, maxRange);
        }

        if (TryComp(projectile, out RMCPenetratingProjectileComponent? penetrating))
        {
            penetrating.ShotFrom = origin;
            Dirty(projectile, penetrating);
        }
    }
}
