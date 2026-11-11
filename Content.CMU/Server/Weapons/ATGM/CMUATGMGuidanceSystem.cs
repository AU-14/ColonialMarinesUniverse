using System.Numerics;
using Content.Shared._RMC14.Emplacements;
using Content.Shared.CMU14.Weapons.ATGM;
using Content.Shared.Examine;
using Content.Shared.Projectiles;
using Content.Shared.Trigger.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Weapons.ATGM;

/// <summary>
///     Server-authoritative flight of wire-guided A.T.G.M. missiles: attaches guidance on launch, steers with a
///     limited turn rate and off-boresight cone, and detonates on the target or on timeout. There is deliberately
///     no missile line-of-sight check; the missile is wire guided.
/// </summary>
/// <remarks>
///     <para>
///     The gunner's click only sets the launch heading. With a spotter lock the missile homes on the locked
///     entity and the spotter's cursor bends the path (<see cref="CMUGuidedMissileMath.ComputeAimPoint"/>);
///     without one it flies straight until the spotter's first cursor update, then to the cursor.
///     </para>
///     <para>
///     If the guider stops holding the finder the wire is cut and the missile flies straight, lock or not.
///     If the locked entity is deleted, changes map, or the guider loses sight of it for too long, the missile
///     keeps flying to where the entity was last seen and detonates there.
///     </para>
/// </remarks>
public sealed partial class CMUATGMGuidanceSystem : EntitySystem
{
    private static readonly TimeSpan SightCheckInterval = TimeSpan.FromSeconds(0.5);

    /// <summary>
    ///     Weight of the newest per-tick sample in the locked target's velocity estimate.
    /// </summary>
    private const float VelocitySmoothing = 0.5f;

    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private CMUATGMSystem _atgm = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedProjectileSystem _projectile = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TriggerSystem _trigger = default!;
    [Dependency] private ViewSubscriberSystem _viewSubscriber = default!;

    private readonly List<EntityUid> _detonate = new();

    /// <summary>
    ///     Newest cursor per guider received since the last tick. Last write wins, so a burst of updates costs one
    ///     application per tick and the newest point is never dropped.
    /// </summary>
    private readonly Dictionary<EntityUid, EntityCoordinates> _pendingGuidance = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUATGMLauncherComponent, AmmoShotEvent>(OnAmmoShot);
        SubscribeLocalEvent<CMUGuidedMissileComponent, ComponentShutdown>(OnMissileShutdown);

        SubscribeNetworkEvent<CMUATGMGuidanceEvent>(OnGuidance);
    }

    private void OnAmmoShot(Entity<CMUATGMLauncherComponent> ent, ref AmmoShotEvent args)
    {
        if (args.FiredProjectiles.Count == 0)
            return;

        var now = _timing.CurTime;

        // The gunner's click only orients the launch; it is never a guidance target (FLOW-05).
        MapCoordinates? aim = null;
        if (TryComp(ent, out GunComponent? gun) && gun.ShootCoordinates is { } shootCoordinates)
            aim = _transform.ToMapCoordinates(shootCoordinates);

        EntityUid? mount = TryComp(ent, out MountableWeaponComponent? mountable)
            ? GetEntity(mountable.MountedTo)
            : null;

        foreach (var projectile in args.FiredProjectiles)
        {
            if (TerminatingOrDeleted(projectile) || !TryComp(projectile, out PhysicsComponent? physics))
                continue;

            var position = _transform.GetMapCoordinates(projectile);
            var velocity = _physics.GetMapLinearVelocity(projectile, physics);
            Vector2 direction;
            if (velocity.LengthSquared() > 0.0001f)
                direction = velocity;
            else if (aim is { } aimPoint && aimPoint.MapId == position.MapId && aimPoint.Position != position.Position)
                direction = aimPoint.Position - position.Position;
            else
                direction = _transform.GetWorldRotation(ent).ToWorldVec();

            var missile = EnsureComp<CMUGuidedMissileComponent>(projectile);
            missile.Launcher = mount ?? ent.Owner;
            missile.WireOrigin = position;
            missile.Speed = ent.Comp.MissileSpeed;
            missile.TurnRateDegPerSec = ent.Comp.TurnRateDegPerSec;
            missile.MaxOffBoresightDeg = ent.Comp.MaxOffBoresightDeg;
            missile.ImpactRadius = ent.Comp.ImpactRadius;
            missile.MaxGuidanceDistance = ent.Comp.MaxGuidanceDistance;
            missile.MaxSteerOffset = ent.Comp.MaxSteerOffset;
            missile.SteerOffsetPerDistance = ent.Comp.SteerOffsetPerDistance;
            missile.TerminalDistance = ent.Comp.TerminalDistance;
            missile.FadeDistance = ent.Comp.FadeDistance;
            missile.CursorFreshness = ent.Comp.CursorFreshness;
            missile.MaxLead = ent.Comp.MaxLead;
            missile.TargetSightTimeout = ent.Comp.TargetSightTimeout;
            missile.LaunchHeading = direction.ToAngle();
            missile.Heading = missile.LaunchHeading;
            missile.ArmedAt = now + ent.Comp.ArmingDelay;
            missile.DetonateAt = now + ent.Comp.MaxFlightTime;
            missile.Target = MapCoordinates.Nullspace;
            missile.HasGuidance = false;

            if (TryComp(projectile, out ProjectileComponent? projectileComp) &&
                projectileComp.Shooter is { } gunner &&
                _atgm.TryGetSpotter(ent, gunner, out var spotter, out var finder, out _))
            {
                missile.Guider = spotter;
                missile.Finder = finder;
                AddViewer((projectile, missile), spotter);

                if (TryComp(finder, out CMUATGMFinderComponent? finderComp) &&
                    _atgm.TryGetLock((finder, finderComp), out var locked))
                {
                    missile.TargetEntity = locked;
                    missile.TrackedPosition = _transform.GetMapCoordinates(locked.Value);
                    missile.TrackedVelocity = Vector2.Zero;
                    missile.TrackingFrozen = false;
                    missile.TargetLastSeen = now;
                    missile.NextSightCheck = now + SightCheckInterval;
                }
            }

            Dirty(projectile, missile);
            ApplyFlight((projectile, missile, physics));
        }

        // The mount's ammo layer only refreshes on an appearance change; the shared mount system
        // derives ammo from a magazine slot this launcher does not have.
        if (mount != null)
            _appearance.SetData(mount.Value, WeaponMountComponentVisualLayers.MountedAmmo, false);
    }

    private void OnMissileShutdown(Entity<CMUGuidedMissileComponent> ent, ref ComponentShutdown args)
    {
        RemoveViewer(ent);
    }

    private void OnGuidance(CMUATGMGuidanceEvent ev, EntitySessionEventArgs args)
    {
        // Never trust an entity from the client: the guider is whoever the sending session controls.
        if (args.SenderSession.AttachedEntity is not { } guider)
            return;

        QueueGuidance(guider, GetCoordinates(ev.Coordinates));
    }

    /// <summary>
    ///     Queues a cursor update from <paramref name="guider"/>, replacing any not yet applied. Applied at the
    ///     start of the next update through <see cref="TryApplyGuidance"/>.
    /// </summary>
    public void QueueGuidance(EntityUid guider, EntityCoordinates coordinates)
    {
        _pendingGuidance[guider] = coordinates;
    }

    /// <summary>
    ///     Points every missile guided by <paramref name="guider"/> at <paramref name="coordinates"/>.
    ///     Rejects invalid, non-finite or cross-map points and clamps the distance from the guider. Every valid
    ///     update is applied; the latest one wins.
    /// </summary>
    /// <returns>Whether any missile accepted the update.</returns>
    public bool TryApplyGuidance(EntityUid guider, EntityCoordinates coordinates)
    {
        if (TerminatingOrDeleted(guider) || !coordinates.IsValid(EntityManager))
            return false;

        var point = _transform.ToMapCoordinates(coordinates);
        if (point.MapId == MapId.Nullspace ||
            !float.IsFinite(point.Position.X) ||
            !float.IsFinite(point.Position.Y))
        {
            return false;
        }

        var guiderPos = _transform.GetMapCoordinates(guider);
        if (guiderPos.MapId != point.MapId)
            return false;

        var now = _timing.CurTime;
        var applied = false;
        var query = EntityQueryEnumerator<CMUGuidedMissileComponent>();
        while (query.MoveNext(out var uid, out var missile))
        {
            if (missile.Guider != guider || _transform.GetMapId(uid) != point.MapId)
                continue;

            var offset = point.Position - guiderPos.Position;
            var distance = offset.Length();
            if (distance > missile.MaxGuidanceDistance && distance > 0)
                offset *= missile.MaxGuidanceDistance / distance;

            missile.Target = new MapCoordinates(guiderPos.Position + offset, point.MapId);
            missile.LastGuidanceUpdate = now;
            missile.HasGuidance = true;
            applied = true;
        }

        return applied;
    }

    public override void Update(float frameTime)
    {
        foreach (var (guider, coordinates) in _pendingGuidance)
        {
            TryApplyGuidance(guider, coordinates);
        }

        _pendingGuidance.Clear();

        var now = _timing.CurTime;
        _detonate.Clear();

        var query = EntityQueryEnumerator<CMUGuidedMissileComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var missile, out var physics))
        {
            // A collision this tick already fired the warhead; do not trigger it a second time,
            // but never leave a spent missile in flight with nothing left to remove it.
            if (HasDetonated(uid))
            {
                if (!TerminatingOrDeleted(uid) && !EntityManager.IsQueuedForDeletion(uid))
                    QueueDel(uid);

                continue;
            }

            if (now >= missile.DetonateAt)
            {
                _detonate.Add(uid);
                continue;
            }

            if (missile.Guider is { } guider &&
                (missile.Finder is not { } finder || !_atgm.CanGuide(guider, finder)))
            {
                LoseGuidance((uid, missile));
            }

            if (missile.Guider != null && Guide((uid, missile), frameTime, now))
            {
                _detonate.Add(uid);
                continue;
            }

            ApplyFlight((uid, missile, physics));
        }

        foreach (var uid in _detonate)
        {
            Detonate(uid);
        }

        _detonate.Clear();
    }

    /// <summary>
    ///     Steers a guided missile for one tick.
    /// </summary>
    /// <returns>Whether the missile's proximity fuse fired.</returns>
    private bool Guide(Entity<CMUGuidedMissileComponent> ent, float frameTime, TimeSpan now)
    {
        var missile = ent.Comp;
        var position = _transform.GetMapCoordinates(ent);
        var armed = now >= missile.ArmedAt;

        // A point fuse also covers one tick of travel so a fast missile cannot step over its aim point.
        var pointFuse = MathF.Max(missile.ImpactRadius, missile.Speed * frameTime);

        Vector2 aim;
        if (missile.TargetEntity != null)
        {
            UpdateTracking(ent, position.MapId, frameTime, now);
            if (missile.TrackedPosition.MapId != position.MapId)
                return false;

            var target = missile.TrackedPosition.Position;
            if (armed && IsInFuseRange(ent, position.Position, pointFuse))
                return true;

            var distance = Vector2.Distance(position.Position, target);
            var lead = missile.TrackingFrozen
                ? Vector2.Zero
                : CMUGuidedMissileMath.ComputeLead(missile.TrackedVelocity, distance, missile.Speed, missile.MaxLead);

            Vector2? cursor = null;
            if (missile.HasGuidance &&
                missile.Target.MapId == position.MapId &&
                now - missile.LastGuidanceUpdate <= missile.CursorFreshness)
            {
                cursor = missile.Target.Position;
            }

            aim = CMUGuidedMissileMath.ComputeAimPoint(position.Position,
                target + lead,
                cursor,
                missile.MaxSteerOffset,
                missile.SteerOffsetPerDistance,
                missile.TerminalDistance,
                missile.FadeDistance);
        }
        else
        {
            // No lock: fly straight until the spotter's first cursor update, then to the cursor.
            if (!missile.HasGuidance || missile.Target.MapId != position.MapId)
                return false;

            aim = missile.Target.Position;
            if (armed && (aim - position.Position).LengthSquared() <= pointFuse * pointFuse)
                return true;
        }

        var toAim = aim - position.Position;
        if (toAim.LengthSquared() > 0.0001f)
        {
            var heading = CMUGuidedMissileMath.StepHeading(missile.Heading.Theta,
                toAim.ToAngle().Theta,
                missile.LaunchHeading.Theta,
                MathHelper.DegreesToRadians(missile.TurnRateDegPerSec) * frameTime,
                MathHelper.DegreesToRadians(missile.MaxOffBoresightDeg));
            missile.Heading = new Angle(heading);
        }

        return false;
    }

    /// <summary>
    ///     Follows the locked entity's position, or freezes on its last known position once it is gone, has
    ///     left the missile's map, or the guider has not seen it for <see cref="CMUGuidedMissileComponent.TargetSightTimeout"/>.
    /// </summary>
    private void UpdateTracking(Entity<CMUGuidedMissileComponent> ent, MapId missileMap, float frameTime, TimeSpan now)
    {
        var missile = ent.Comp;
        if (missile.TrackingFrozen || missile.TargetEntity is not { } target)
            return;

        if (TerminatingOrDeleted(target) ||
            EntityManager.IsQueuedForDeletion(target) ||
            _transform.GetMapId(target) != missileMap)
        {
            FreezeTracking(missile);
            return;
        }

        var position = _transform.GetMapCoordinates(target);
        if (frameTime > 0 && missile.TrackedPosition.MapId == position.MapId)
        {
            var sample = (position.Position - missile.TrackedPosition.Position) / frameTime;
            missile.TrackedVelocity = Vector2.Lerp(missile.TrackedVelocity, sample, VelocitySmoothing);
        }

        missile.TrackedPosition = position;

        if (missile.Guider is not { } guider || now < missile.NextSightCheck)
            return;

        missile.NextSightCheck = now + SightCheckInterval;
        if (_examine.InRangeUnOccluded(guider, target, 0f, uid => uid == target))
            missile.TargetLastSeen = now;
        else if (now - missile.TargetLastSeen > missile.TargetSightTimeout)
            FreezeTracking(missile);
    }

    private static void FreezeTracking(CMUGuidedMissileComponent missile)
    {
        missile.TrackingFrozen = true;
        missile.TrackedVelocity = Vector2.Zero;
    }

    /// <summary>
    ///     Proximity fuse for a locked missile: within <see cref="CMUGuidedMissileComponent.ImpactRadius"/> of the
    ///     target's bounding box while tracking it, or within <paramref name="pointFuse"/> of its last known
    ///     position once tracking is frozen.
    /// </summary>
    private bool IsInFuseRange(Entity<CMUGuidedMissileComponent> ent, Vector2 position, float pointFuse)
    {
        var missile = ent.Comp;
        if (!missile.TrackingFrozen && missile.TargetEntity is { } target)
        {
            var box = _lookup.GetWorldAABB(target).Enlarged(missile.ImpactRadius);
            return box.Contains(position);
        }

        return (missile.TrackedPosition.Position - position).LengthSquared() <= pointFuse * pointFuse;
    }

    private void ApplyFlight(Entity<CMUGuidedMissileComponent, PhysicsComponent> ent)
    {
        var direction = ent.Comp1.Heading.ToVec();
        var targetVelocity = direction * ent.Comp1.Speed;
        var currentVelocity = _physics.GetMapLinearVelocity(ent, ent.Comp2);
        _physics.SetLinearVelocity(ent, ent.Comp2.LinearVelocity + targetVelocity - currentVelocity, body: ent.Comp2);

        var projectileAngle = CompOrNull<ProjectileComponent>(ent)?.Angle ?? Angle.Zero;
        var rotation = direction.ToWorldAngle() + projectileAngle;
        if (Math.Abs(Angle.ShortestDistance(rotation, _transform.GetWorldRotation(ent)).Theta) > 0.001)
            _transform.SetWorldRotationNoLerp(ent.Owner, rotation);
    }

    private bool HasDetonated(EntityUid uid)
    {
        return TerminatingOrDeleted(uid) ||
               EntityManager.IsQueuedForDeletion(uid) ||
               CompOrNull<ProjectileComponent>(uid)?.ProjectileSpent == true;
    }

    internal void Detonate(EntityUid uid)
    {
        if (HasDetonated(uid))
            return;

        var shooter = CompOrNull<ProjectileComponent>(uid)?.Shooter;

        // The proximity fuse can fire a few hundredths of a tile before the hull would have touched the
        // tracked target, which would skip the direct hit (armour piercing, vehicle multiplier, knockout).
        // Deliver that hit first so a fused detonation on a live target lands the full AP strike. A target that
        // has since hidden in a container (locker, body bag, devoured) is out of reach and only takes the blast.
        if (TryComp(uid, out CMUGuidedMissileComponent? missile) &&
            !missile.TrackingFrozen &&
            missile.TargetEntity is { } target &&
            !TerminatingOrDeleted(target) &&
            !EntityManager.IsQueuedForDeletion(target) &&
            !_container.IsEntityOrParentInContainer(target) &&
            _transform.GetMapId(target) == _transform.GetMapId(uid) &&
            TryComp(uid, out ProjectileComponent? projectile) &&
            TryComp(uid, out PhysicsComponent? physics) &&
            IsInFuseRange((uid, missile), _transform.GetWorldPosition(uid), missile.ImpactRadius))
        {
            _projectile.ProjectileCollide((uid, projectile, physics), target);
        }

        // Deletion is only queued below, so the hull lives into the next physics step. Mark it spent so a mob
        // it happens to overlap cannot still take a direct hit (and a gib) from a blast-only detonation.
        if (TryComp(uid, out ProjectileComponent? spent))
            spent.ProjectileSpent = true;

        RemComp<CMUGuidedMissileComponent>(uid);
        _trigger.Trigger(uid, shooter);

        // DeleteOnTrigger normally removes it; never leave a spent missile flying.
        if (!TerminatingOrDeleted(uid))
            QueueDel(uid);
    }

    private void LoseGuidance(Entity<CMUGuidedMissileComponent> ent)
    {
        ent.Comp.Guider = null;
        ent.Comp.Finder = null;
        RemoveViewer(ent);
        Dirty(ent);
    }

    private void AddViewer(Entity<CMUGuidedMissileComponent> ent, EntityUid guider)
    {
        if (!TryComp(guider, out ActorComponent? actor))
            return;

        _viewSubscriber.AddViewSubscriber(ent, actor.PlayerSession);
        ent.Comp.ViewSubscriber = actor.PlayerSession;
    }

    private void RemoveViewer(Entity<CMUGuidedMissileComponent> ent)
    {
        if (ent.Comp.ViewSubscriber is not { } session)
            return;

        ent.Comp.ViewSubscriber = null;
        _viewSubscriber.RemoveViewSubscriber(ent, session);
    }
}
