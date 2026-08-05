using Content.Shared._RMC14.Projectiles.Penetration;
using Content.Shared._RMC14.Xenonids.Damage;
using Content.Shared.Damage;
using Content.Shared.Projectiles;

namespace Content.Server.Projectiles;

public sealed partial class ProjectileSystem
{
    private void RaiseRmcProjectileDamageDealt(
        EntityUid target,
        EntityUid? shooter,
        DamageSpecifier? damage)
    {
        var ev = new ProjectileDamageDealtEvent(shooter, damage);
        RaiseLocalEvent(target, ref ev);
    }

    private void RaiseRmcAfterProjectileHit(Entity<ProjectileComponent> projectile, EntityUid target)
    {
        var ev = new AfterProjectileHitEvent(projectile, target);
        RaiseLocalEvent(projectile, ref ev);
    }
}
