using Content.Client.Projectiles;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Client.GameObjects;
using Robust.Client.Physics;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Dynamics.Contacts;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Client._RMC14.Weapons.Ranged.Prediction;

public sealed partial class GunPredictionSystem : SharedGunPredictionSystem
{
    private static readonly TimeSpan PredictionCorrelationRetention = TimeSpan.FromSeconds(30);

    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private ProjectileSystem _projectile = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private EntityQuery<IgnorePredictionHideComponent> _ignorePredictionHideQuery;
    private EntityQuery<IgnorePredictionHitComponent> _ignorePredictionHitQuery;
    private EntityQuery<FixturesComponent> _fixturesQuery;
    private EntityQuery<SpriteComponent> _spriteQuery;

    private readonly Dictionary<int, EntityUid> _authoritativeProjectiles = new();
    private readonly Dictionary<int, TimeSpan> _completedPredictions = new();
    private readonly List<int> _expiredPredictions = new();
    private readonly Dictionary<int, TimeSpan> _pendingPersistentHandoffs = new();
    private readonly Dictionary<int, TimeSpan> _rejectedPredictions = new();

    public override void Initialize()
    {
        base.Initialize();

        _ignorePredictionHideQuery = GetEntityQuery<IgnorePredictionHideComponent>();
        _ignorePredictionHitQuery = GetEntityQuery<IgnorePredictionHitComponent>();
        _fixturesQuery = GetEntityQuery<FixturesComponent>();
        _spriteQuery = GetEntityQuery<SpriteComponent>();

        SubscribeLocalEvent<PhysicsUpdateBeforeSolveEvent>(OnBeforeSolve);
        SubscribeLocalEvent<PhysicsUpdateAfterSolveEvent>(OnAfterSolve);
        SubscribeLocalEvent<PredictedProjectileAuthorityReconcileEvent>(OnAuthorityReconcile);
        SubscribeNetworkEvent<PredictedProjectileCleanupEvent>(OnPredictedProjectileCleanup);
        SubscribeNetworkEvent<PredictedProjectileHitRejectedEvent>(OnPredictedProjectileHitRejected);
        SubscribeLocalEvent<PredictedProjectileClientComponent, ComponentStartup>(OnClientProjectileStartup);
        SubscribeLocalEvent<PredictedProjectileClientComponent, UpdateIsPredictedEvent>(OnClientProjectileUpdateIsPredicted);
        SubscribeLocalEvent<PredictedProjectileClientComponent, StartCollideEvent>(
            OnClientProjectileStartCollide,
            before: [typeof(ProjectileSystem)]);

        SubscribeLocalEvent<PredictedProjectileServerComponent, ComponentStartup>(OnServerProjectileStartup);
        SubscribeLocalEvent<PredictedProjectileServerComponent, AfterAutoHandleStateEvent>(OnServerProjectileAfterState);
        SubscribeLocalEvent<PredictedProjectileServerComponent, ComponentRemove>(OnServerProjectileRemove);

        UpdatesBefore.Add(typeof(TransformSystem));
    }

    private void OnBeforeSolve(ref PhysicsUpdateBeforeSolveEvent ev)
    {
        var query = EntityQueryEnumerator<PredictedProjectileClientComponent>();
        while (query.MoveNext(out var uid, out var predicted))
        {
            if (!IsClientSide(uid) ||
                TerminatingOrDeleted(uid) ||
                EntityManager.IsQueuedForDeletion(uid))
            {
                predicted.Coordinates = null;
                continue;
            }

            predicted.Coordinates = Transform(uid).Coordinates;
        }
    }

    private void OnAfterSolve(ref PhysicsUpdateAfterSolveEvent ev)
    {
        if (_timing.IsFirstTimePredicted)
            return;
        var query = EntityQueryEnumerator<PredictedProjectileClientComponent>();
        while (query.MoveNext(out var uid, out var predicted))
        {
            if (IsClientSide(uid) &&
                predicted.Coordinates is { } coordinates &&
                !TerminatingOrDeleted(uid) &&
                !EntityManager.IsQueuedForDeletion(uid) &&
                coordinates.EntityId != uid &&
                Exists(coordinates.EntityId) &&
                !TerminatingOrDeleted(coordinates.EntityId))
            {
                _transform.SetCoordinates(uid, coordinates);
            }

            predicted.Coordinates = null;
        }
    }

    private void OnClientProjectileUpdateIsPredicted(Entity<PredictedProjectileClientComponent> ent, ref UpdateIsPredictedEvent args)
    {
        args.IsPredicted = true;
    }

    private void OnClientProjectileStartup(Entity<PredictedProjectileClientComponent> ent, ref ComponentStartup args)
    {
        // Client entity IDs may eventually be reused. A new predicted projectile
        // must never inherit a completed/rejected correlation from an older shot.
        if (_authoritativeProjectiles.TryGetValue(ent.Owner.Id, out var oldAuthority) &&
            oldAuthority != ent.Owner)
        {
            _authoritativeProjectiles.Remove(ent.Owner.Id);
        }

        _completedPredictions.Remove(ent.Owner.Id);
        _pendingPersistentHandoffs.Remove(ent.Owner.Id);
        _rejectedPredictions.Remove(ent.Owner.Id);
        _projectile.RejectPredictedImpact(ent.Owner.Id);
    }

    private void OnClientProjectileStartCollide(Entity<PredictedProjectileClientComponent> ent, ref StartCollideEvent args)
    {
        if (!_timing.IsFirstTimePredicted ||
            _timing.ApplyingState ||
            ent.Comp.Hit ||
            args.OurFixtureId != SharedProjectileSystem.ProjectileFixture ||
            !args.OtherFixture.Hard)
        {
            return;
        }

        if (!TryComp(ent, out ProjectileComponent? projectile) ||
            !TryComp(ent, out PhysicsComponent? physics) ||
            projectile.ProjectileSpent ||
            projectile is { Weapon: null, OnlyCollideWhenShot: true } ||
            _ignorePredictionHitQuery.HasComp(args.OtherEntity) ||
            !IsSameMap(ent.Owner, args.OtherEntity))
        {
            return;
        }

        var netEnt = GetNetEntity(args.OtherEntity);
        if (ent.Comp.HitTargets.Contains(netEnt))
            return;

        var pos = _transform.GetMapCoordinates(args.OtherEntity);
        var hit = new List<(NetEntity, MapCoordinates)> { (netEnt, pos) };
        PredictHit(ent, projectile, physics, args.OtherEntity, hit);
    }

    private void OnServerProjectileStartup(Entity<PredictedProjectileServerComponent> ent, ref ComponentStartup args)
    {
        ReconcileServerProjectile(ent);
    }

    private void OnServerProjectileAfterState(
        Entity<PredictedProjectileServerComponent> ent,
        ref AfterAutoHandleStateEvent args)
    {
        ReconcileServerProjectile(ent);
    }

    private void ReconcileServerProjectile(Entity<PredictedProjectileServerComponent> ent)
    {
        if (_player.LocalEntity is not { } localPlayer || ent.Comp.ClientEnt != localPlayer)
            return;

        _authoritativeProjectiles[ent.Comp.ClientId] = ent;

        if (_pendingPersistentHandoffs.Remove(ent.Comp.ClientId))
        {
            _completedPredictions.Remove(ent.Comp.ClientId);
            _rejectedPredictions.Remove(ent.Comp.ClientId);
            RetirePredictedProjectileCopy(ent.Comp.ClientId, true);
            SetAuthoritativeProjectileVisible(ent, true);
            ReleaseAuthoritativeProjectile(ent.Comp.ClientId, ent.Owner);
            return;
        }

        if (_rejectedPredictions.Remove(ent.Comp.ClientId))
        {
            _completedPredictions.Remove(ent.Comp.ClientId);
            RetirePredictedProjectileCopy(ent.Comp.ClientId, true);
            SetAuthoritativeProjectileVisible(ent, true);
            ReleaseAuthoritativeProjectile(ent.Comp.ClientId, ent.Owner);
            return;
        }

        if (!GunPrediction)
        {
            RetirePredictedProjectileCopy(ent.Comp.ClientId, true);
            ReleaseAuthoritativeProjectile(ent.Comp.ClientId, ent.Owner);
            return;
        }

        // Loaded physical ammo is predicted on its existing network entity, so there is
        // no separate authoritative copy to hide when the correlation state arrives.
        if (ent.Owner.Id == ent.Comp.ClientId && HasComp<PredictedProjectileClientComponent>(ent))
        {
            ReleaseAuthoritativeProjectile(ent.Comp.ClientId, ent.Owner);
            return;
        }

        var livePredictedCopy = TryGetPredictedProjectileCopy(ent.Comp.ClientId, true, out _);
        if (!ShouldHideAuthoritativeProjectile(
                true,
                livePredictedCopy,
                _completedPredictions.ContainsKey(ent.Comp.ClientId),
                false))
            return;

        SetAuthoritativeProjectileVisible(ent, false);
    }

    private void OnServerProjectileRemove(Entity<PredictedProjectileServerComponent> ent, ref ComponentRemove args)
    {
        var localProjectile = _player.LocalEntity is { } localPlayer &&
                              ent.Comp.ClientEnt == localPlayer;
        var ownsCorrelation = _authoritativeProjectiles.TryGetValue(
                                  ent.Comp.ClientId,
                                  out var authoritative) &&
                              authoritative == ent.Owner;
        if (ownsCorrelation)
            _authoritativeProjectiles.Remove(ent.Comp.ClientId);

        if (localProjectile && ownsCorrelation)
        {
            _completedPredictions.Remove(ent.Comp.ClientId);
            _pendingPersistentHandoffs.Remove(ent.Comp.ClientId);
            _rejectedPredictions.Remove(ent.Comp.ClientId);
            RetirePredictedProjectileCopy(ent.Comp.ClientId, true);
        }

        SetAuthoritativeProjectileVisible(ent, true);
    }

    private void ReleaseAuthoritativeProjectile(int clientId, EntityUid authority)
    {
        if (_authoritativeProjectiles.TryGetValue(clientId, out var current) &&
            current == authority)
        {
            _authoritativeProjectiles.Remove(clientId);
        }
    }

    private void SetAuthoritativeProjectileVisible(EntityUid projectile, bool visible)
    {
        if (_ignorePredictionHideQuery.HasComp(projectile))
            return;

        if (_spriteQuery.TryComp(projectile, out var sprite))
            _sprite.SetVisible((projectile, sprite), visible);
    }

    public static bool IsMatchingPredictedProjectileCopy(
        bool serverProjectileBelongsToLocalPlayer,
        bool clientCopyExists,
        bool clientCopyIsClientSide,
        bool clientCopyIsPredicted)
    {
        return serverProjectileBelongsToLocalPlayer &&
               clientCopyExists &&
               clientCopyIsClientSide &&
               clientCopyIsPredicted;
    }

    public static bool ShouldHideAuthoritativeProjectile(
        bool serverProjectileBelongsToLocalPlayer,
        bool livePredictedCopy,
        bool completedPrediction,
        bool rejectedPrediction)
    {
        return serverProjectileBelongsToLocalPlayer &&
               !rejectedPrediction &&
               (livePredictedCopy || completedPrediction);
    }

    private void RetirePredictedProjectileCopy(int clientId, bool localProjectile)
    {
        if (!TryGetPredictedProjectileCopy(clientId, localProjectile, out var predicted))
            return;

        QueueDel(predicted);
    }

    private bool TryGetPredictedProjectileCopy(int clientId, bool localProjectile, out EntityUid predicted)
    {
        predicted = new EntityUid(clientId);
        var exists = Exists(predicted);
        var isClientSide = exists && IsClientSide(predicted);
        var isPredicted = exists && HasComp<PredictedProjectileClientComponent>(predicted);
        return IsMatchingPredictedProjectileCopy(localProjectile, exists, isClientSide, isPredicted);
    }

    private bool TryGetPredictedProjectileSimulation(int clientId, bool localProjectile, out EntityUid predicted)
    {
        predicted = new EntityUid(clientId);
        return localProjectile &&
               Exists(predicted) &&
               HasComp<PredictedProjectileClientComponent>(predicted);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        RemoveExpiredPredictions(_completedPredictions);
        RemoveExpiredPersistentHandoffs();
        RemoveExpiredPredictions(_rejectedPredictions);

        if (!_timing.IsFirstTimePredicted)
            return;

        // TODO gun prediction remove this once the client reliably detects collisions
        var projectiles = EntityQueryEnumerator<PredictedProjectileClientComponent, ProjectileComponent, PhysicsComponent>();
        while (projectiles.MoveNext(out var uid, out var predicted, out var projectile, out var physics))
        {
            if (predicted.Hit)
                continue;

            if (!_fixturesQuery.TryComp(uid, out var fixtures))
                continue;

            var hit = new List<(NetEntity, MapCoordinates)>();
            var hitEntities = new HashSet<NetEntity>();
            EntityUid? firstHit = null;
            var contacts = _physics.GetContacts((uid, fixtures));
            while (contacts.MoveNext(out var contact))
            {
                if (!TryGetValidProjectileContact(uid, contact, out var other) ||
                    _ignorePredictionHitQuery.HasComp(other) ||
                    !IsSameMap(uid, other))
                {
                    continue;
                }

                var netEnt = GetNetEntity(other);
                if (predicted.HitTargets.Contains(netEnt) || !hitEntities.Add(netEnt))
                    continue;

                var pos = _transform.GetMapCoordinates(other);
                hit.Add((netEnt, pos));
                firstHit ??= other;
            }

            if (firstHit is not { } firstHitEntity)
                continue;

            PredictHit((uid, predicted), projectile, physics, firstHitEntity, hit);
        }

        var predictedQuery = EntityQueryEnumerator<PredictedProjectileHitComponent, SpriteComponent, TransformComponent>();
        while (predictedQuery.MoveNext(out var hit, out var sprite, out var xform))
        {
            var origin = hit.Origin;
            var coordinates = xform.Coordinates;
            if (!origin.TryDistance(EntityManager, _transform, coordinates, out var distance) ||
                distance >= hit.Distance)
            {
                sprite.Visible = false;
            }
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        // TODO bullet prediction remove this when lerping doesnt make the client's entity slightly slower
        var projectiles = EntityQueryEnumerator<PredictedProjectileClientComponent, TransformComponent>();
        while (projectiles.MoveNext(out _, out var xform))
        {
            xform.ActivelyLerping = false;
        }
    }

    private void PredictHit(
        Entity<PredictedProjectileClientComponent> ent,
        ProjectileComponent projectile,
        PhysicsComponent physics,
        EntityUid firstHit,
        List<(NetEntity Id, MapCoordinates Coordinates)> hit)
    {
        if (ent.Comp.Hit)
            return;

        ent.Comp.Hit = true;
        if (IsClientSide(ent.Owner))
        {
            _completedPredictions[ent.Owner.Id] =
                _timing.CurTime + PredictionCorrelationRetention;
        }

        ReportPredictedHit(new PredictedProjectileHitEvent(ent.Owner.Id, hit));
        var waitsForAuthoritativePenetration = projectile.PenetrationThreshold > 0;
        if (waitsForAuthoritativePenetration)
        {
            ent.Comp.PendingPenetrationVelocity = physics.LinearVelocity;
            ent.Comp.PendingPenetrationBodyType = physics.BodyType;
        }

        if (_projectile.ProjectileCollide((ent, projectile, physics), firstHit))
            ent.Comp.HitTargets.Add(GetNetEntity(firstHit));

        // RMC penetration is deterministic on both sides and can re-arm
        // immediately. Vanilla penetration depends on server-only destruction
        // thresholds, so wait for authoritative feedback before another hit.
        if (!projectile.ProjectileSpent && waitsForAuthoritativePenetration)
        {
            // Do not let a gated projectile travel through later targets while
            // collision reporting is suppressed. Authoritative feedback restores
            // its saved body type and velocity when penetration is confirmed.
            _physics.SetLinearVelocity(ent, System.Numerics.Vector2.Zero, body: physics);
            _physics.SetBodyType(ent, BodyType.Static, body: physics);
        }

        if (!projectile.ProjectileSpent && !waitsForAuthoritativePenetration)
        {
            ent.Comp.Hit = false;
            _completedPredictions.Remove(ent.Owner.Id);
        }
    }

    public void ReportPredictedHit(PredictedProjectileHitEvent hit)
    {
        RaiseNetworkEvent(hit);
    }

    private void OnPredictedProjectileCleanup(PredictedProjectileCleanupEvent ev)
    {
        foreach (var projectile in ev.Projectiles)
        {
            _completedPredictions.Remove(projectile);
            _rejectedPredictions.Remove(projectile);
            _projectile.RejectPredictedImpact(projectile);
            RetirePredictedProjectileCopy(projectile, true);
        }
    }

    private void OnPredictedProjectileHitRejected(PredictedProjectileHitRejectedEvent ev)
    {
        _completedPredictions.Remove(ev.Projectile);
        _rejectedPredictions[ev.Projectile] =
            _timing.CurTime + PredictionCorrelationRetention;
        _projectile.RejectPredictedImpact(ev.Projectile);
        RetirePredictedProjectileCopy(ev.Projectile, true);

        if (_authoritativeProjectiles.TryGetValue(ev.Projectile, out var authoritative) &&
            Exists(authoritative))
        {
            SetAuthoritativeProjectileVisible(authoritative, true);
            ReleaseAuthoritativeProjectile(ev.Projectile, authoritative);
        }
    }

    private void OnAuthorityReconcile(ref PredictedProjectileAuthorityReconcileEvent ev)
    {
        if (!ev.ProjectileSpent)
        {
            _completedPredictions.Remove(ev.Projectile);
            _pendingPersistentHandoffs.Remove(ev.Projectile);
            _rejectedPredictions.Remove(ev.Projectile);
            // Physical ammunition predicts on its existing network entity, while
            // ordinary rounds use a separate client-side copy. Both need their
            // local simulation re-armed after authoritative penetration feedback.
            if (TryGetPredictedProjectileSimulation(ev.Projectile, true, out var predicted) &&
                !EntityManager.IsQueuedForDeletion(predicted) &&
                TryComp(predicted, out PredictedProjectileClientComponent? predictedComponent) &&
                TryComp(predicted, out ProjectileComponent? projectile) &&
                TryComp(predicted, out PhysicsComponent? physics))
            {
                // Authority-first penetration feedback means the server already
                // processed this target. Ignore the local copy's later contact so
                // it neither reports the duplicate nor waits for feedback that the
                // server intentionally will not send twice.
                predictedComponent.HitTargets.Add(ev.Target);
                predictedComponent.Hit = false;
                projectile.ProjectileSpent = false;

                if (predictedComponent.PendingPenetrationBodyType is { } bodyType)
                {
                    _physics.SetBodyType(predicted, bodyType, body: physics);
                    _physics.SetBodyStatus(predicted, physics, BodyStatus.InAir);
                }

                if (predictedComponent.PendingPenetrationVelocity is { } velocity)
                    _physics.SetLinearVelocity(predicted, velocity, body: physics);

                predictedComponent.PendingPenetrationBodyType = null;
                predictedComponent.PendingPenetrationVelocity = null;
                return;
            }

            // The client already retired a copy the server says survived. Hand
            // presentation back to the authority instead of hiding it forever.
            _rejectedPredictions[ev.Projectile] =
                _timing.CurTime + PredictionCorrelationRetention;
            RetirePredictedProjectileCopy(ev.Projectile, true);
            if (_authoritativeProjectiles.TryGetValue(ev.Projectile, out var survivingAuthority) &&
                Exists(survivingAuthority))
            {
                SetAuthoritativeProjectileVisible(survivingAuthority, true);
            }

            return;
        }

        if (ev.Persistent)
        {
            _completedPredictions.Remove(ev.Projectile);
            _rejectedPredictions.Remove(ev.Projectile);

            if (_authoritativeProjectiles.TryGetValue(ev.Projectile, out var authoritative) &&
                Exists(authoritative))
            {
                _pendingPersistentHandoffs.Remove(ev.Projectile);
                RetirePredictedProjectileCopy(ev.Projectile, true);
                SetAuthoritativeProjectileVisible(authoritative, true);
                ReleaseAuthoritativeProjectile(ev.Projectile, authoritative);
            }
            else
            {
                _pendingPersistentHandoffs[ev.Projectile] =
                    _timing.CurTime + PredictionCorrelationRetention;
            }

            return;
        }

        _pendingPersistentHandoffs.Remove(ev.Projectile);
        _rejectedPredictions.Remove(ev.Projectile);
        _completedPredictions[ev.Projectile] =
            _timing.CurTime + PredictionCorrelationRetention;

        if (_authoritativeProjectiles.TryGetValue(ev.Projectile, out var completedAuthority) &&
            Exists(completedAuthority))
        {
            SetAuthoritativeProjectileVisible(completedAuthority, false);
            ReleaseAuthoritativeProjectile(ev.Projectile, completedAuthority);
        }
    }

    private void RemoveExpiredPredictions(Dictionary<int, TimeSpan> predictions)
    {
        _expiredPredictions.Clear();
        foreach (var (projectile, expiry) in predictions)
        {
            if (expiry <= _timing.CurTime)
                _expiredPredictions.Add(projectile);
        }

        foreach (var projectile in _expiredPredictions)
        {
            predictions.Remove(projectile);
        }
    }

    private void RemoveExpiredPersistentHandoffs()
    {
        _expiredPredictions.Clear();
        foreach (var (projectile, expiry) in _pendingPersistentHandoffs)
        {
            if (expiry <= _timing.CurTime)
                _expiredPredictions.Add(projectile);
        }

        foreach (var projectile in _expiredPredictions)
        {
            _pendingPersistentHandoffs.Remove(projectile);
            RetirePredictedProjectileCopy(projectile, true);
        }
    }

    private static bool TryGetValidProjectileContact(
        EntityUid projectile,
        Contact contact,
        out EntityUid other)
    {
        string projectileFixtureId;
        Fixture? projectileFixture;
        Fixture? otherFixture;
        if (contact.EntityA == projectile)
        {
            projectileFixtureId = contact.FixtureAId;
            projectileFixture = contact.FixtureA;
            other = contact.EntityB;
            otherFixture = contact.FixtureB;
        }
        else if (contact.EntityB == projectile)
        {
            projectileFixtureId = contact.FixtureBId;
            projectileFixture = contact.FixtureB;
            other = contact.EntityA;
            otherFixture = contact.FixtureA;
        }
        else
        {
            other = default;
            return false;
        }

        return contact.Enabled &&
               projectileFixtureId == SharedProjectileSystem.ProjectileFixture &&
               projectileFixture != null &&
               otherFixture is { Hard: true } &&
               ((projectileFixture.CollisionMask & otherFixture.CollisionLayer) != 0 ||
                (otherFixture.CollisionMask & projectileFixture.CollisionLayer) != 0);
    }
}

[ByRefEvent]
public record struct PredictedProjectileAuthorityReconcileEvent(
    int Projectile,
    NetEntity Target,
    bool ProjectileSpent,
    bool Persistent);
