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

    public override void Initialize()
    {
        base.Initialize();

        _ignorePredictionHideQuery = GetEntityQuery<IgnorePredictionHideComponent>();
        _ignorePredictionHitQuery = GetEntityQuery<IgnorePredictionHitComponent>();
        _fixturesQuery = GetEntityQuery<FixturesComponent>();
        _spriteQuery = GetEntityQuery<SpriteComponent>();

        SubscribeLocalEvent<PhysicsUpdateBeforeSolveEvent>(OnBeforeSolve);
        SubscribeLocalEvent<PhysicsUpdateAfterSolveEvent>(OnAfterSolve);
        SubscribeNetworkEvent<PredictedProjectileCleanupEvent>(OnPredictedProjectileCleanup);
        SubscribeLocalEvent<PredictedProjectileClientComponent, UpdateIsPredictedEvent>(OnClientProjectileUpdateIsPredicted);
        SubscribeLocalEvent<PredictedProjectileClientComponent, StartCollideEvent>(
            OnClientProjectileStartCollide,
            before: [typeof(ProjectileSystem)]);

        SubscribeLocalEvent<PredictedProjectileServerComponent, ComponentStartup>(OnServerProjectileStartup);
        SubscribeLocalEvent<PredictedProjectileServerComponent, ComponentRemove>(OnServerProjectileRemove);

        UpdatesBefore.Add(typeof(TransformSystem));
    }

    private void OnBeforeSolve(ref PhysicsUpdateBeforeSolveEvent ev)
    {
        var query = EntityQueryEnumerator<PredictedProjectileClientComponent>();
        while (query.MoveNext(out var uid, out var predicted))
        {
            if (TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid))
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
            if (predicted.Coordinates is { } coordinates &&
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

    private void OnClientProjectileStartCollide(Entity<PredictedProjectileClientComponent> ent, ref StartCollideEvent args)
    {
        if (_timing.ApplyingState ||
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
        var pos = _transform.GetMapCoordinates(args.OtherEntity);
        var hit = new HashSet<(NetEntity, MapCoordinates)> { (netEnt, pos) };
        PredictHit(ent, projectile, physics, args.OtherEntity, hit);
    }

    private void OnServerProjectileStartup(Entity<PredictedProjectileServerComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.ClientEnt != _player.LocalEntity)
            return;

        if (!GunPrediction)
        {
            RetirePredictedProjectileCopy(ent.Comp.ClientId, true);
            return;
        }

        // Loaded physical ammo is predicted on its existing network entity, so there is
        // no separate authoritative copy to hide when the correlation state arrives.
        if (ent.Owner.Id == ent.Comp.ClientId && HasComp<PredictedProjectileClientComponent>(ent))
            return;

        if (!TryGetPredictedProjectileCopy(ent.Comp.ClientId, true, out _))
            return;

        if (_ignorePredictionHideQuery.HasComp(ent))
            return;

        if (_spriteQuery.TryComp(ent, out var sprite))
            _sprite.SetVisible((ent, sprite), false);
    }

    private void OnServerProjectileRemove(Entity<PredictedProjectileServerComponent> ent, ref ComponentRemove args)
    {
        var localProjectile = ent.Comp.ClientEnt == _player.LocalEntity;
        RetirePredictedProjectileCopy(ent.Comp.ClientId, localProjectile);

        if (_ignorePredictionHideQuery.HasComp(ent))
            return;

        if (_spriteQuery.TryComp(ent, out var sprite))
            _sprite.SetVisible((ent, sprite), true);
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

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

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

            var hit = new HashSet<(NetEntity, MapCoordinates)>();
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
        HashSet<(NetEntity Id, MapCoordinates Coordinates)> hit)
    {
        if (ent.Comp.Hit)
            return;

        ent.Comp.Hit = true;
        ReportPredictedHit(new PredictedProjectileHitEvent(ent.Owner.Id, hit));
        _projectile.ProjectileCollide((ent, projectile, physics), firstHit);
    }

    public void ReportPredictedHit(PredictedProjectileHitEvent hit)
    {
        RaiseNetworkEvent(hit);
    }

    private void OnPredictedProjectileCleanup(PredictedProjectileCleanupEvent ev)
    {
        foreach (var projectile in ev.Projectiles)
        {
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
