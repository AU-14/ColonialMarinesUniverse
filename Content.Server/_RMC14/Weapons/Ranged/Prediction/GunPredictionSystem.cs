using Content.Server._RMC14.Movement;
using Content.Server.Movement.Components;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared.GameTicking;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._RMC14.Weapons.Ranged.Prediction;

public sealed partial class GunPredictionSystem : SharedGunPredictionSystem
{
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedProjectileSystem _projectile = default!;
    [Dependency] private RMCLagCompensationSystem _rmcLagCompensation = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private TransformSystem _transform = default!;

    private readonly Dictionary<(Guid, int), EntityUid> _predicted = new();
    private readonly List<(PredictedProjectileHitEvent Event, ICommonSession Player)> _predictedHits = new();
    private bool _preventCollision;
    private bool _validatingPredictedHitCollision;
    private bool _logHits;
    private float _coordinateDeviation;
    private float _lowestCoordinateDeviation;
    private float _aabbEnlargement;

    private EntityQuery<FixturesComponent> _fixturesQuery;
    private EntityQuery<LagCompensationComponent> _lagCompensationQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<ProjectileComponent> _projectileQuery;
    private EntityQuery<PredictedProjectileServerComponent> _predictedProjectileServerQuery;
    private EntityQuery<TransformComponent> _transformQuery;

    public override void Initialize()
    {
        base.Initialize();

        _fixturesQuery = GetEntityQuery<FixturesComponent>();
        _lagCompensationQuery = GetEntityQuery<LagCompensationComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _projectileQuery = GetEntityQuery<ProjectileComponent>();
        _predictedProjectileServerQuery = GetEntityQuery<PredictedProjectileServerComponent>();
        _transformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeNetworkEvent<RequestShootEvent>(OnShootRequest);
        SubscribeNetworkEvent<PredictedProjectileHitEvent>(OnPredictedProjectileHit);

        SubscribeLocalEvent<PredictedProjectileServerComponent, MapInitEvent>(OnPredictedMapInit);
        SubscribeLocalEvent<PredictedProjectileServerComponent, ComponentRemove>(OnPredictedRemove);
        SubscribeLocalEvent<PredictedProjectileServerComponent, EntityTerminatingEvent>(OnPredictedRemove);
        SubscribeLocalEvent<PredictedProjectileServerComponent, PreventCollideEvent>(OnPredictedPreventCollide);

        Subs.CVar(_config, RMCCVars.RMCGunPredictionPreventCollision, v => _preventCollision = v, true);
        Subs.CVar(_config, RMCCVars.RMCGunPredictionLogHits, v => _logHits = v, true);
        Subs.CVar(_config, RMCCVars.RMCGunPredictionCoordinateDeviation, v => _coordinateDeviation = v, true);
        Subs.CVar(_config, RMCCVars.RMCGunPredictionLowestCoordinateDeviation, v => _lowestCoordinateDeviation = v, true);
        Subs.CVar(_config, RMCCVars.RMCGunPredictionAabbEnlargement, v => _aabbEnlargement = v, true);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _predicted.Clear();
    }

    private void OnShootRequest(RequestShootEvent ev, EntitySessionEventArgs args)
    {
        _rmcLagCompensation.SetLastRealTick(args.SenderSession.UserId, ev.LastRealTick);
    }

    private void OnPredictedMapInit(Entity<PredictedProjectileServerComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Shooter == null)
        {
            Log.Warning($"{nameof(PredictedProjectileServerComponent)} map initialized with a null shooter session!");
            return;
        }

        _predicted[(ent.Comp.Shooter.UserId, ent.Comp.ClientId)] = ent;
    }

    private void OnPredictedRemove<T>(Entity<PredictedProjectileServerComponent> ent, ref T args)
    {
        if (ent.Comp.Shooter == null)
            return;

        _predicted.Remove((ent.Comp.Shooter.UserId, ent.Comp.ClientId));
    }

    private void OnPredictedProjectileHit(PredictedProjectileHitEvent ev, EntitySessionEventArgs args)
    {
        _predictedHits.Add((ev, args.SenderSession));
    }

    private void OnPredictedPreventCollide(Entity<PredictedProjectileServerComponent> ent, ref PreventCollideEvent args)
    {
        if (!_preventCollision || _validatingPredictedHitCollision)
            return;

        if (args.Cancelled)
            return;

        var other = args.OtherEntity;
        if (!_lagCompensationQuery.TryComp(other, out var otherLagComp) ||
            !_fixturesQuery.TryComp(other, out var otherFixtures) ||
            !_transformQuery.TryComp(other, out var otherTransform))
        {
            return;
        }

        if (!_physicsQuery.TryComp(ent, out var entPhysics))
            return;

        if (!FixturesCanCollide(args.OurFixture, args.OtherFixture))
            return;

        if (!Collides(
                (ent, ent, entPhysics),
                (other, otherLagComp, otherFixtures, args.OtherBody, otherTransform),
                null,
                args.OurFixture,
                args.OtherFixture))
        {
            args.Cancelled = true;
        }
    }

    private bool Collides(
        Entity<PredictedProjectileServerComponent, PhysicsComponent> projectile,
        Entity<LagCompensationComponent, FixturesComponent, PhysicsComponent, TransformComponent> other,
        MapCoordinates? clientCoordinates,
        Fixture projectileFixture,
        Fixture otherFixture)
    {
        if (!FixturesCanCollide(projectileFixture, otherFixture))
            return false;

        var projectileCoordinates = _transform.GetMapCoordinates(projectile);
        if (projectileCoordinates.MapId == MapId.Nullspace ||
            other.Comp4.MapID != projectileCoordinates.MapId)
        {
            return false;
        }

        var projectilePosition = projectileCoordinates.Position;

        MapCoordinates? lowestCoordinate = null;
        var otherCoordinates = EntityCoordinates.Invalid;
        var ping = projectile.Comp1.Shooter?.Channel.Ping ?? 0;
        // Use 1.5 due to the trip buffer.
        var sentTime = _timing.CurTime - TimeSpan.FromMilliseconds(ping * 1.5);
        var pingTime = TimeSpan.FromMilliseconds(ping);

        foreach (var pos in other.Comp1.Positions)
        {
            otherCoordinates = pos.Item2;
            if (pos.Item1 >= sentTime)
                break;
            else if (lowestCoordinate == null && pos.Item1 >= sentTime - pingTime)
                lowestCoordinate = _transform.ToMapCoordinates(pos.Item2);
        }

        var otherMapCoordinates = otherCoordinates == default
            ? _transform.GetMapCoordinates(other)
            : _transform.ToMapCoordinates(otherCoordinates);

        if (otherMapCoordinates.MapId != projectileCoordinates.MapId)
            return false;

        if (clientCoordinates is { } reportedCoordinates)
        {
            if (reportedCoordinates.MapId != projectileCoordinates.MapId)
                return false;

            var nearLowestCoordinate = lowestCoordinate is { } lowest &&
                                       lowest.MapId == projectileCoordinates.MapId &&
                                       reportedCoordinates.InRange(lowest, _lowestCoordinateDeviation);
            if (reportedCoordinates.InRange(otherMapCoordinates, _coordinateDeviation) || nearLowestCoordinate)
                otherMapCoordinates = reportedCoordinates;
        }

        var transform = new Transform(otherMapCoordinates.Position, 0);
        var bounds = new Box2(transform.Position, transform.Position);

        for (var i = 0; i < otherFixture.Shape.ChildCount; i++)
        {
            var fixtureBounds = otherFixture.Shape.ComputeAABB(transform, i);
            bounds = bounds.Union(fixtureBounds);
        }

        bounds = bounds.Enlarged(_aabbEnlargement);
        if (bounds.Contains(projectilePosition))
            return true;

        var projectileVelocity = _physics.GetLinearVelocity(projectile, projectile.Comp2.LocalCenter);
        projectilePosition = projectileCoordinates.Position + projectileVelocity / _timing.TickRate / 1.5f;
        if (bounds.Contains(projectilePosition))
            return true;

        return false;
    }

    private bool PassesCollisionRules(
        EntityUid projectile,
        PhysicsComponent projectilePhysics,
        Fixture projectileFixture,
        EntityUid other,
        PhysicsComponent otherPhysics,
        Fixture otherFixture)
    {
        if (!projectilePhysics.CanCollide ||
            !otherPhysics.CanCollide ||
            !FixturesCanCollide(projectileFixture, otherFixture))
        {
            return false;
        }

        _validatingPredictedHitCollision = true;
        try
        {
            var prevent = new PreventCollideEvent(
                projectile,
                other,
                projectilePhysics,
                otherPhysics,
                projectileFixture,
                otherFixture);
            RaiseLocalEvent(projectile, ref prevent);
            if (prevent.Cancelled)
                return false;

            prevent = new PreventCollideEvent(
                other,
                projectile,
                otherPhysics,
                projectilePhysics,
                otherFixture,
                projectileFixture);
            RaiseLocalEvent(other, ref prevent);
            return !prevent.Cancelled;
        }
        finally
        {
            _validatingPredictedHitCollision = false;
        }
    }

    private void ProcessPredictedHit(PredictedProjectileHitEvent ev, ICommonSession player)
    {
        if (!_predicted.TryGetValue((player.UserId, ev.Projectile), out var projectile))
            return;

        if (!_predictedProjectileServerQuery.TryComp(projectile, out var predictedProjectile) ||
            predictedProjectile.Hit)
        {
            return;
        }

        if (predictedProjectile.Shooter?.UserId != player.UserId.UserId)
            return;

        if (!_projectileQuery.TryComp(projectile, out var projectileComp) ||
            !_physicsQuery.TryComp(projectile, out var projectilePhysics) ||
            !_fixturesQuery.TryComp(projectile, out var projectileFixtures) ||
            !projectileFixtures.Fixtures.TryGetValue(
                SharedProjectileSystem.ProjectileFixture,
                out var projectileFixture))
        {
            return;
        }

        if (projectileComp.ProjectileSpent ||
            projectileComp is { Weapon: null, OnlyCollideWhenShot: true })
        {
            return;
        }

        foreach (var (netEnt, clientPos) in ev.Hit)
        {
            if (GetEntity(netEnt) is not { Valid: true } hit)
                continue;

            if (!_lagCompensationQuery.TryComp(hit, out var otherLagComp) ||
                !_fixturesQuery.TryComp(hit, out var otherFixtures) ||
                !_physicsQuery.TryComp(hit, out var otherPhysics) ||
                !_transformQuery.TryComp(hit, out var otherTransform))
            {
                continue;
            }

            var validCollision = false;
            foreach (var otherFixture in otherFixtures.Fixtures.Values)
            {
                if (!Collides(
                        (projectile, predictedProjectile, projectilePhysics),
                        (hit, otherLagComp, otherFixtures, otherPhysics, otherTransform),
                        clientPos,
                        projectileFixture,
                        otherFixture) ||
                    !PassesCollisionRules(
                        projectile,
                        projectilePhysics,
                        projectileFixture,
                        hit,
                        otherPhysics,
                        otherFixture))
                {
                    continue;
                }

                validCollision = true;
                break;
            }

            if (!validCollision)
            {
                if (_logHits)
                    Log.Info("missed");

                continue;
            }

            if (_logHits)
                Log.Info("hit");

            predictedProjectile.Hit = true;
            _projectile.ProjectileCollide((projectile, projectileComp, projectilePhysics), hit, true);
            return;
        }
    }

    private static bool FixturesCanCollide(Fixture projectileFixture, Fixture otherFixture)
    {
        return otherFixture.Hard &&
               ((projectileFixture.CollisionMask & otherFixture.CollisionLayer) != 0 ||
                (otherFixture.CollisionMask & projectileFixture.CollisionLayer) != 0);
    }

    public override void Update(float frameTime)
    {
        try
        {
            foreach (var ev in _predictedHits)
            {
                ProcessPredictedHit(ev.Event, ev.Player);
            }
        }
        finally
        {
            _predictedHits.Clear();
        }

        var predicted = EntityQueryEnumerator<PredictedProjectileHitComponent, TransformComponent>();
        while (predicted.MoveNext(out var uid, out var hit, out var xform))
        {
            var origin = hit.Origin;
            var coordinates = xform.Coordinates;
            if (!origin.TryDistance(EntityManager, _transform, coordinates, out var distance) ||
                distance >= hit.Distance)
            {
                QueueDel(uid);
            }
        }
    }
}
