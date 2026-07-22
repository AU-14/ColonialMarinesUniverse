using Content.Shared._RMC14.Projectiles.Penetration;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared._RMC14.Xenonids.Damage;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;

namespace Content.Shared.Projectiles;

public abstract partial class SharedProjectileSystem
{
    [Dependency] private DamageableSystem _rmcDamageable = default!;
    [Dependency] private SharedColorFlashEffectSystem _rmcColor = default!;
    [Dependency] private SharedContainerSystem _rmcContainers = default!;
    [Dependency] private SharedGunSystem _rmcGuns = default!;
    [Dependency] private INetManager _rmcNet = default!;

    private void InitializeRMCProjectile()
    {
        if (!_rmcNet.IsClient)
            return;

        SubscribeLocalEvent<ProjectileComponent, StartCollideEvent>(OnRMCStartCollide);
    }

    private void OnRMCStartCollide(Entity<ProjectileComponent> projectile, ref StartCollideEvent args)
    {
        // Predicted projectiles are handled first by GunPredictionSystem. Letting
        // this generic client handler process an unspent penetrating projectile
        // again would duplicate its impact feedback and penetration bookkeeping.
        if (HasComp<PredictedProjectileClientComponent>(projectile) ||
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
    public bool ProjectileCollide(
        Entity<ProjectileComponent, PhysicsComponent> projectile,
        EntityUid target,
        bool predicted = false)
    {
        return ProjectileCollideCore(projectile, target, predicted);
    }

    /// <summary>
    /// Handles a projectile collision after the caller has validated the contact.
    /// The server overrides this so physics hits and accepted predicted hits share
    /// the same authoritative damage and feedback pipeline.
    /// </summary>
    protected virtual bool ProjectileCollideCore(
        Entity<ProjectileComponent, PhysicsComponent> projectile,
        EntityUid target,
        bool predicted)
    {
        var (uid, component, _) = projectile;
        if (component.ProjectileSpent)
        {
            if (_rmcNet.IsServer && component.DeleteOnCollide)
                PredictedQueueDel(uid);

            return false;
        }

        var reflect = new ProjectileReflectAttemptEvent(uid, component, false);
        RaiseLocalEvent(target, ref reflect);
        if (reflect.Cancelled)
        {
            SetShooter(uid, component, target);
            return false;
        }

        var hit = new ProjectileHitEvent(
            component.Damage * _rmcDamageable.UniversalProjectileDamageModifier,
            target,
            component.Shooter);
        RaiseLocalEvent(uid, ref hit);
        if (hit.Handled)
            return false;

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
        else if (!component.IgnoreResistances)
        {
            var modify = new DamageModifyEvent(hit.Damage, component.Shooter, uid);
            RaiseLocalEvent(target, modify);
            damage = modify.Damage;
        }

        var localPrediction = _rmcNet.IsClient &&
                              HasComp<PredictedProjectileClientComponent>(uid);
        var playFeedback = !localPrediction || BeginPredictedImpactFeedback(uid, target);
        if (playFeedback)
        {
            var damageEvent = new ProjectileDamageDealtEvent(component.Shooter, damage);
            RaiseLocalEvent(target, ref damageEvent);

            if (localPrediction && !Deleted(target))
            {
                if (HasComp<DamageableComponent>(target) && damage?.AnyPositive() == true)
                    _rmcColor.RaiseEffect(Color.Red, new List<EntityUid> { target }, Filter.Local());

                _rmcGuns.PlayImpactSound(target, damage, component.SoundHit, component.ForceSound);
            }
        }

        component.ProjectileSpent = !TryPredictPenetration(component, damage);
        Dirty(uid, component);

        var additionalHits = new AfterProjectileHitEvent(projectile, target);
        RaiseLocalEvent(uid, ref additionalHits);

        if (_rmcNet.IsServer || HasComp<PredictedProjectileClientComponent>(uid))
            PlayImpactEffect((uid, component), target);

        if (!predicted && component.DeleteOnCollide && component.ProjectileSpent)
        {
            PredictedQueueDel(uid);
            return true;
        }

        if (!_rmcNet.IsServer || !predicted || !component.DeleteOnCollide || !component.ProjectileSpent)
            return true;

        PreservePredictedProjectileHit(projectile, target);
        return true;
    }

    protected void PreservePredictedProjectileHit(
        Entity<ProjectileComponent, PhysicsComponent> projectile,
        EntityUid target)
    {
        var uid = projectile.Owner;
        var predictedHit = EnsureComp<PredictedProjectileHitComponent>(uid);
        predictedHit.Origin = _transform.GetMoverCoordinates(uid);

        var targetCoordinates = _transform.GetMoverCoordinates(target);
        if (predictedHit.Origin.TryDistance(EntityManager, _transform, targetCoordinates, out var distance))
            predictedHit.Distance = distance;

        Dirty(uid, predictedHit);
    }

    /// <summary>
    /// Correlates immediate shooter feedback with the matching authoritative
    /// result. Non-client implementations do not need correlation state.
    /// </summary>
    protected virtual bool BeginPredictedImpactFeedback(EntityUid projectile, EntityUid target)
    {
        return true;
    }

    private static bool TryPredictPenetration(ProjectileComponent projectile, DamageSpecifier? damage)
    {
        if (projectile.PenetrationThreshold == 0 || damage == null)
            return false;

        if (projectile.PenetrationDamageTypeRequirement != null)
        {
            foreach (var requiredDamageType in projectile.PenetrationDamageTypeRequirement)
            {
                if (!damage.DamageDict.Keys.Contains(requiredDamageType))
                    return false;
            }
        }

        // The destruction threshold is server-only, so the client cannot know
        // exactly how much penetration budget this target consumes. Continue the
        // local copy optimistically; authoritative impact feedback stops it when
        // the server's real penetration calculation is spent.
        return damage.AnyPositive();
    }

    protected abstract void PlayImpactEffect(Entity<ProjectileComponent> projectile, EntityUid target);
}
