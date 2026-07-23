using Content.Server.Administration.Logs;
using Content.Server._CMU14.ZLevels.Core;
using Content.Server.Destructible;
using Content.Server.Effects;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared.Camera;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;

namespace Content.Server.Projectiles;

public sealed partial class ProjectileSystem : SharedProjectileSystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private ColorFlashEffectSystem _color = default!;
    [Dependency] private DamageableSystem _damageableSystem = default!;
    [Dependency] private DestructibleSystem _destructibleSystem = default!;
    [Dependency] private GunSystem _guns = default!;
    [Dependency] private SharedCameraRecoilSystem _sharedCameraRecoil = default!;
    [Dependency] private SharedTransformSystem _zTransform = default!;
    [Dependency] private CMUZLevelsSystem _zLevels = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ProjectileComponent, StartCollideEvent>(OnStartCollide);
    }

    private void OnStartCollide(EntityUid uid, ProjectileComponent component, ref StartCollideEvent args)
    {
        // This is so entities that shouldn't get a collision are ignored.
        if (args.OurFixtureId != ProjectileFixture || !args.OtherFixture.Hard
            || component.ProjectileSpent || component is { Weapon: null, OnlyCollideWhenShot: true })
            return;

        ProjectileCollide((uid, component, args.OurBody), args.OtherEntity);
    }

    protected override bool ProjectileCollideCore(
        Entity<ProjectileComponent, PhysicsComponent> projectile,
        EntityUid target,
        bool predicted)
    {
        var (uid, component, physics) = projectile;
        if (component.ProjectileSpent)
        {
            TakeOverRejectedPrediction(uid);
            return false;
        }

        // ProjectileHitEvent handlers may reparent persistent projectiles (for
        // example, embeddables). Keep a grid/map-relative impact position that
        // remains resolvable even if the hit target is deleted by the damage.
        var impactCoordinates = GetNetCoordinates(Transform(uid).Coordinates);

        // it's here so this check is only done once before possible hit
        var attemptEv = new ProjectileReflectAttemptEvent(uid, component, false);
        RaiseLocalEvent(target, ref attemptEv);
        if (attemptEv.Cancelled)
        {
            SetShooter(uid, component, target);
            TakeOverRejectedPrediction(uid);
            return false;
        }

        var ev = new ProjectileHitEvent(component.Damage * _damageableSystem.UniversalProjectileDamageModifier, target, component.Shooter);
        RaiseLocalEvent(uid, ref ev);
        if (ev.Handled)
        {
            TakeOverRejectedPrediction(uid);
            return false;
        }

        var otherName = ToPrettyString(target);
        var damageRequired = _destructibleSystem.DestroyedAt(target);
        if (TryComp<DamageableComponent>(target, out var damageableComponent))
        {
            damageRequired -= _damageableSystem.GetTotalDamage((target, damageableComponent));
            damageRequired = FixedPoint2.Max(damageRequired, FixedPoint2.Zero);
        }
        var damageChanged = _damageableSystem.TryChangeDamage(
            (target, damageableComponent),
            ev.Damage,
            out var damage,
            component.IgnoreResistances,
            origin: component.Shooter,
            tool: uid);
        var deleted = Deleted(target);
        if (damageChanged && Exists(component.Shooter))
        {
            _adminLogger.Add(LogType.BulletHit,
                LogImpact.Medium,
                $"Projectile {ToPrettyString(uid):projectile} shot by {ToPrettyString(component.Shooter!.Value):user} hit {otherName:target} and dealt {damage:damage} damage");

            component.ProjectileSpent = !TryPenetrate((uid, component), damage, damageRequired);
        }
        else
        {
            component.ProjectileSpent = true;
        }

        ICommonSession? predictingSession = null;
        TryComp(projectile, out PredictedProjectileServerComponent? predictedProjectile);
        var activePrediction = predictedProjectile is { Hit: false, RejectionSent: false };
        if (activePrediction && predictedProjectile?.Shooter is { } shooter)
            predictingSession = shooter;

        // Correlated projectiles send feedback directly to the predicting client,
        // which deduplicates it against the immediate local result.
        if (predictingSession == null)
            RaiseRmcProjectileDamageDealt(target, component.Shooter, damage);

        RaiseRmcAfterProjectileHit((uid, component), target);
        Dirty(uid, component);

        if (!predicted &&
            !component.ProjectileSpent &&
            activePrediction &&
            predictedProjectile != null)
        {
            predictedProjectile.AuthoritativeHits.Add(GetNetEntity(target));
        }

        if (activePrediction &&
            predictedProjectile != null &&
            predictingSession is { } shooterSession)
        {
            FixedPoint2? damageTotal = damageableComponent == null
                ? null
                : _damageableSystem.GetTotalDamage((target, damageableComponent));
            var (impactSound, varyPitch) = _guns.GetImpactSound(
                target,
                damage,
                component.SoundHit,
                component.ForceSound);
            RaiseNetworkEvent(
                new PredictedProjectileImpactFeedbackEvent(
                    predictedProjectile.ClientId,
                    GetNetEntity(target),
                    impactCoordinates,
                    damage,
                    damageTotal,
                    impactSound,
                    varyPitch,
                    damageChanged && damage?.AnyPositive() == true,
                    component.DeleteOnCollide,
                    component.ProjectileSpent),
                shooterSession);
        }

        if (!deleted)
        {
            var feedbackCoordinates = _zTransform.ToMapCoordinates(Transform(target).Coordinates);
            var feedbackFilter = _zLevels.AddZLevelViewers(
                Filter.Pvs(target, entityManager: EntityManager),
                feedbackCoordinates);
            if (predictingSession != null)
                feedbackFilter.RemovePlayer(predictingSession);

            if (damageChanged && damage?.AnyPositive() == true)
                _color.RaiseEffect(Color.Red, new List<EntityUid> { target }, feedbackFilter);

            _guns.PlayImpactSound(
                target,
                damage,
                component.SoundHit,
                component.ForceSound,
                predictingSession);

            if (!physics.LinearVelocity.IsLengthZero())
                _sharedCameraRecoil.KickCamera(target, physics.LinearVelocity.Normalized());
        }

        if (component.DeleteOnCollide && component.ProjectileSpent)
        {
            if (predicted)
                PreservePredictedProjectileHit(projectile, target);
            else
                QueueDel(uid);
        }

        PlayImpactEffect((uid, component), target);

        if (activePrediction && predictedProjectile != null)
            predictedProjectile.Hit = component.ProjectileSpent;

        return true;
    }

    /// <summary>
    /// A reflection or handled collision cannot be reconciled with the client's
    /// locally simulated projectile. Retire that copy immediately and let the
    /// authoritative projectile take over, regardless of which collision path
    /// (physics, point-blank, or a validated report) discovered the divergence.
    /// </summary>
    private void TakeOverRejectedPrediction(EntityUid projectile)
    {
        if (!TryComp(projectile, out PredictedProjectileServerComponent? predicted) ||
            predicted.Hit ||
            predicted.Shooter is not { } shooter)
        {
            return;
        }

        predicted.Hit = true;
        if (predicted.RejectionSent)
            return;

        predicted.RejectionSent = true;
        RaiseNetworkEvent(new PredictedProjectileHitRejectedEvent(predicted.ClientId), shooter);
    }

    protected override void PlayImpactEffect(Entity<ProjectileComponent> projectile, EntityUid target)
    {
        if (projectile.Comp.ImpactEffect is not { } impactEffect ||
            !TryComp(projectile, out TransformComponent? xform))
        {
            return;
        }

        NetEntity? shooter = null;
        int? predictedProjectile = null;
        if (TryComp(projectile, out PredictedProjectileServerComponent? predicted) &&
            !predicted.Hit &&
            !predicted.RejectionSent &&
            predicted.ClientEnt is { } clientEntity)
        {
            shooter = GetNetEntity(clientEntity);
            predictedProjectile = predicted.ClientId;
        }

        var impactFilter = _zLevels.AddZLevelViewers(
            Filter.Pvs(xform.Coordinates, entityMan: EntityManager),
            _zTransform.ToMapCoordinates(xform.Coordinates));

        RaiseNetworkEvent(
            new ImpactEffectEvent(
                impactEffect,
                GetNetCoordinates(xform.Coordinates),
                shooter,
                predictedProjectile,
                GetNetEntity(target)),
            impactFilter);
    }

    private bool TryPenetrate(Entity<ProjectileComponent> projectile, DamageSpecifier damage, FixedPoint2 damageRequired)
    {
        // If penetration is to be considered, we need to do some checks to see if the projectile should stop.
        if (projectile.Comp.PenetrationThreshold == 0)
            return false;

        // If a damage type is required, stop the bullet if the hit entity doesn't have that type.
        if (projectile.Comp.PenetrationDamageTypeRequirement != null)
        {
            foreach (var requiredDamageType in projectile.Comp.PenetrationDamageTypeRequirement)
            {
                if (damage.DamageDict.Keys.Contains(requiredDamageType))
                    continue;

                return false;
            }
        }

        // If the object won't be destroyed, it "tanks" the penetration hit.
        if (damage.GetTotal() < damageRequired)
        {
            return false;
        }

        if (!projectile.Comp.ProjectileSpent)
        {
            projectile.Comp.PenetrationAmount += damageRequired;
            // The projectile has dealt enough damage to be spent.
            if (projectile.Comp.PenetrationAmount >= projectile.Comp.PenetrationThreshold)
            {
                return false;
            }
        }

        return true;
    }
}
