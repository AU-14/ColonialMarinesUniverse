using Content.Shared._RMC14.Projectiles.Penetration;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared._RMC14.Xenonids.Damage;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Shared.Projectiles;

public abstract partial class SharedProjectileSystem
{
    [Dependency] private DamageableSystem _rmcDamageable = default!;
    [Dependency] private SharedContainerSystem _rmcContainers = default!;
    [Dependency] private INetManager _rmcNet = default!;
    [Dependency] private IGameTiming _rmcTiming = default!;

    private void InitializeRMCProjectile()
    {
        if (!_rmcNet.IsClient)
            return;

        SubscribeLocalEvent<ProjectileComponent, StartCollideEvent>(OnRMCStartCollide);
    }

    private void OnRMCStartCollide(Entity<ProjectileComponent> projectile, ref StartCollideEvent args)
    {
        // Contact reset replays collisions during state application. Deleting a predicted copy there can invalidate
        // another predicted body's reference to the same contact before the reset has finished iterating it.
        if ((_rmcTiming.ApplyingState && HasComp<PredictedProjectileClientComponent>(projectile)) ||
            args.OurFixtureId != ProjectileFixture ||
            !args.OtherFixture.Hard ||
            projectile.Comp.ProjectileSpent ||
            projectile.Comp is { Weapon: null, OnlyCollideWhenShot: true })
        {
            return;
        }

        ProjectileCollide((projectile.Owner, projectile.Comp, args.OurBody), args.OtherEntity);
    }

    private void PreventRmcContainerOwnerCollision(ProjectileComponent projectile, ref PreventCollideEvent args)
    {
        if (projectile.Weapon is not { } weapon ||
            !HasComp<GunIgnoreContainerOwnerCollisionComponent>(weapon))
        {
            return;
        }

        var current = weapon;
        while (_rmcContainers.TryGetContainingContainer((current, null), out var container))
        {
            if (args.OtherEntity == container.Owner)
            {
                args.Cancelled = true;
                return;
            }

            current = container.Owner;
        }
    }

    /// <summary>
    /// RMC prediction-compatible projectile collision entry point.
    /// </summary>
    public void ProjectileCollide(
        Entity<ProjectileComponent, PhysicsComponent> projectile,
        EntityUid target,
        bool predicted = false)
    {
        var (uid, component, _) = projectile;
        if (component.ProjectileSpent)
        {
            if (_rmcNet.IsServer && component.DeleteOnCollide)
                PredictedQueueDel(uid);

            return;
        }

        var reflect = new ProjectileReflectAttemptEvent(uid, component, false);
        RaiseLocalEvent(target, ref reflect);
        if (reflect.Cancelled)
        {
            SetShooter(uid, component, target);
            return;
        }

        var hit = new ProjectileHitEvent(
            component.Damage * _rmcDamageable.UniversalProjectileDamageModifier,
            target,
            component.Shooter);
        RaiseLocalEvent(uid, ref hit);
        if (hit.Handled)
            return;

        DamageSpecifier? damage = new DamageSpecifier(hit.Damage);
        if (_rmcNet.IsServer)
        {
            damage = _rmcDamageable.ChangeDamage(
                (target, (DamageableComponent?) null),
                hit.Damage,
                component.IgnoreResistances,
                origin: component.Shooter,
                tool: uid);
        }

        var damageEvent = new ProjectileDamageDealtEvent(component.Shooter, damage);
        RaiseLocalEvent(target, ref damageEvent);

        component.ProjectileSpent = true;
        Dirty(uid, component);

        var additionalHits = new AfterProjectileHitEvent(projectile, target);
        RaiseLocalEvent(uid, ref additionalHits);

        if (_rmcNet.IsServer || HasComp<PredictedProjectileClientComponent>(uid))
            PlayImpactEffect((uid, component));

        if (!predicted && component.DeleteOnCollide && component.ProjectileSpent)
        {
            PredictedQueueDel(uid);
            return;
        }

        if (!_rmcNet.IsServer || !predicted || !component.DeleteOnCollide)
            return;

        var predictedHit = EnsureComp<PredictedProjectileHitComponent>(uid);
        predictedHit.Origin = _transform.GetMoverCoordinates(uid);

        var targetCoordinates = _transform.GetMoverCoordinates(target);
        if (predictedHit.Origin.TryDistance(EntityManager, _transform, targetCoordinates, out var distance))
            predictedHit.Distance = distance;

        Dirty(uid, predictedHit);
    }

    protected abstract void PlayImpactEffect(Entity<ProjectileComponent> projectile);
}
