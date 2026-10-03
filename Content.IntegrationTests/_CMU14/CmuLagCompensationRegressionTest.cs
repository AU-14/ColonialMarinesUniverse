#nullable enable annotations

using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Utility;
using Content.Server._RMC14.Weapons.Ranged.Prediction;
using Content.Server.Movement.Components;
using Content.Server.Movement.Systems;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Movement;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Robust.Client.Timing;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14;

[TestFixture]
public sealed class CmuLagCompensationRegressionTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUTestLagCompensationBody
          components:
          - type: LagCompensation
          - type: Physics
            bodyType: Static
          - type: Fixtures
            fixtures:
              body:
                shape:
                  !type:PhysShapeCircle
                    radius: 0.1
                layer:
                - MobLayer
                mask:
                - MobMask
        """;

    [Test]
    public async Task PredictedHitRejectsDifferentMapsAndInvalidViewStamps()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        await pair.RunTicksSync(60);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            var otherMap = maps.CreateMap(out var otherMapId);
            var projectile = entities.SpawnEntity("CMUTestLagCompensationBody", map.GridCoords);
            var transform = entities.System<SharedTransformSystem>();
            var position = transform.GetMapCoordinates(projectile).Position;
            var target = entities.SpawnEntity("CMUTestLagCompensationBody", new MapCoordinates(position, otherMapId));
            var lag = entities.System<SharedRMCLagCompensationSystem>();
            var timing = pair.Server.ResolveDependency<IGameTiming>();
            var body = entities.GetComponent<PhysicsComponent>(projectile);

            Assert.That(lag.ValidatePredictedHit(target, (projectile, body), null, timing.CurTick, 0), Is.False,
                "Matching XY on another map must not authorize damage or a grab.");
            transform.SetCoordinates(target, map.GridCoords);
            Assert.That(lag.ValidatePredictedHit(target, (projectile, body), null, timing.CurTick, 0), Is.True,
                "The same valid bodies must collide after moving onto the same map.");
            Assert.Multiple(() =>
            {
                Assert.That(lag.ValidatePredictedHit(target, (projectile, body), null, timing.CurTick + 1, 0), Is.False);
                Assert.That(lag.ValidatePredictedHit(target, (projectile, body), null, new GameTick(1), 0), Is.False);
                Assert.That(lag.ValidatePredictedHit(target, (projectile, body), null, timing.CurTick, lag.GetSubsteps() + 1), Is.False);
                Assert.That(lag.ValidatePredictedHit(target, (projectile, body), null, timing.CurTick, -lag.GetSubsteps() - 1), Is.False);
            });
            entities.DeleteEntity(otherMap);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(4)]
    public async Task ExecutionSubstepsDoNotChangeViewedSnapshotOrLeakSessionContext(int substeps)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var sessions = await pair.Server.AddDummySessions(1);
        await pair.RunTicksSync(15);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var timing = pair.Server.ResolveDependency<IGameTiming>();
            pair.Server.ResolveDependency<IConfigurationManager>().SetCVar(CVars.TargetMinimumTickrate, (int) timing.TickRate * substeps);
            var lag = entities.System<SharedRMCLagCompensationSystem>();
            Assert.That(lag.GetSubsteps(), Is.EqualTo(substeps));
            var projectile = entities.SpawnEntity("CMUTestLagCompensationBody", map.GridCoords);
            var target = entities.SpawnEntity("CMUTestLagCompensationBody", map.GridCoords.Offset(new Vector2(8, 0)));
            var body = entities.GetComponent<PhysicsComponent>(projectile);
            var history = entities.GetComponent<LagCompensationComponent>(target);
            var viewTick = timing.CurTick - 5;
            var viewTime = timing.CurTime - 5 * timing.TickPeriod;
            history.Positions.Clear();
            history.Positions.Enqueue((viewTime, map.GridCoords, Angle.Zero));
            history.Positions.Enqueue((viewTime + timing.TickPeriod, map.GridCoords.Offset(new Vector2(4, 0)), Angle.Zero));
            history.Positions.Enqueue((timing.CurTime, map.GridCoords.Offset(new Vector2(8, 0)), Angle.Zero));
            lag.SetLastRealTick(sessions[0].UserId, timing.CurTick);

            foreach (var substep in new[] { -lag.GetSubsteps(), -1, 0, 1, lag.GetSubsteps() })
            {
                Assert.That(lag.ValidatePredictedHit(target, (projectile, body), sessions[0], viewTick, substep), Is.True,
                    $"Execution substep {substep} must leave the ordinary remote target at its viewed snapshot.");
                Assert.That(lag.GetLastRealTick(sessions[0].UserId), Is.EqualTo(timing.CurTick),
                    "A hit's temporary view must not leak into subsequent interactions.");
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SparseHistoryUsesPredecessorAndFinalEqualTimestampPose()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var timing = pair.Server.ResolveDependency<IGameTiming>();
            var lag = entities.System<LagCompensationSystem>();
            var target = entities.SpawnEntity("CMUTestLagCompensationBody", map.GridCoords.Offset(new Vector2(10, 0)));
            var current = entities.GetComponent<TransformComponent>(target).Coordinates;
            var history = entities.GetComponent<LagCompensationComponent>(target);
            Assert.That(history.Positions, Is.Not.Empty, "A stationary newly added component needs an initial history sample.");
            var firstTime = timing.CurTime - TimeSpan.FromMilliseconds(200);
            var lastTime = timing.CurTime - TimeSpan.FromMilliseconds(100);
            var first = map.GridCoords;
            var last = map.GridCoords.Offset(new Vector2(2, 0));
            history.Positions.Clear();
            history.Positions.Enqueue((firstTime, first, Angle.Zero));
            history.Positions.Enqueue((lastTime, map.GridCoords.Offset(new Vector2(1, 0)), Angle.FromDegrees(10)));
            history.Positions.Enqueue((lastTime, last, Angle.FromDegrees(20)));

            Assert.Multiple(() =>
            {
                Assert.That(lag.GetCoordinatesAngleAtTime(target, firstTime).Coordinates, Is.EqualTo(first));
                Assert.That(lag.GetCoordinatesAngleAtTime(target, lastTime - TimeSpan.FromTicks(1)).Coordinates, Is.EqualTo(first),
                    "A future move cannot replace the pose of a stationary target.");
                Assert.That(lag.GetCoordinatesAngleAtTime(target, lastTime), Is.EqualTo((last, Angle.FromDegrees(20))),
                    "The last movement in the snapshot tick defines its final pose.");
                Assert.That(lag.GetCoordinatesAngleAtTime(target, timing.CurTime).Coordinates, Is.EqualTo(last));
                Assert.That(lag.GetCoordinatesAngleAtTime(target, firstTime - TimeSpan.FromTicks(1)).Coordinates, Is.EqualTo(current));
                Assert.That(lag.GetCoordinatesAngleAtTime(target, timing.CurTime - lag.BufferTime - TimeSpan.FromTicks(1)).Coordinates, Is.EqualTo(current));
                Assert.That(lag.GetCoordinatesAngleAtTime(target, timing.CurTime + TimeSpan.FromTicks(1)).Coordinates, Is.EqualTo(current));
                Assert.That(lag.TryGetHistoricalCoordinatesAngleAtTime(target, firstTime, out var actual, out _), Is.True);
                Assert.That(actual, Is.EqualTo(first));
                Assert.That(lag.TryGetHistoricalCoordinatesAngleAtTime(target, firstTime - TimeSpan.FromTicks(1), out _, out _), Is.False,
                    "Missing history must not authorize a gun's additional historical-position tolerance.");
                Assert.That(lag.TryGetHistoricalCoordinatesAngleAtTime(target, timing.CurTime - lag.BufferTime - TimeSpan.FromTicks(1), out _, out _), Is.False);
            });
            history.Positions.Clear();
            Assert.That(lag.GetCoordinatesAngleAtTime(target, lastTime).Coordinates, Is.EqualTo(current));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PruningRetainsStationaryAnchorAcrossTheWindowBoundary()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var timing = pair.Server.ResolveDependency<IGameTiming>();
            var lag = entities.System<LagCompensationSystem>();
            var target = entities.SpawnEntity("CMUTestLagCompensationBody", map.GridCoords.Offset(new Vector2(8, 0)));
            var history = entities.GetComponent<LagCompensationComponent>(target);
            var cutoff = timing.CurTime - lag.BufferTime;
            var anchor = map.GridCoords.Offset(new Vector2(1, 0));
            history.Positions.Clear();
            history.Positions.Enqueue((cutoff - TimeSpan.FromSeconds(2), map.GridCoords, Angle.Zero));
            history.Positions.Enqueue((cutoff - TimeSpan.FromSeconds(1), anchor, Angle.Zero));
            history.Positions.Enqueue((cutoff + TimeSpan.FromMilliseconds(100), map.GridCoords.Offset(new Vector2(3, 0)), Angle.Zero));
            lag.Update(0);
            Assert.Multiple(() =>
            {
                Assert.That(history.Positions.Count, Is.EqualTo(2));
                Assert.That(lag.GetCoordinatesAngleAtTime(target, cutoff).Coordinates, Is.EqualTo(anchor));
                Assert.That(lag.GetCoordinatesAngleAtTime(target, cutoff + TimeSpan.FromMilliseconds(50)).Coordinates, Is.EqualTo(anchor));
            });

            history.Positions.Clear();
            history.Positions.Enqueue((cutoff - TimeSpan.FromSeconds(1), anchor, Angle.Zero));
            lag.Update(0);
            Assert.That(lag.GetCoordinatesAngleAtTime(target, timing.CurTime).Coordinates, Is.EqualTo(anchor),
                "An unmoving target's sole sample remains useful even after its timestamp leaves the buffer.");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StandardGunUsesHistoricalPredecessorAndRequiresEvidenceForExtraTolerance()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var sessions = await pair.Server.AddDummySessions(1);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var config = pair.Server.ResolveDependency<IConfigurationManager>();
            config.SetCVar(RMCCVars.RMCGunPredictionCoordinateDeviation, 0.25f);
            config.SetCVar(RMCCVars.RMCGunPredictionLowestCoordinateDeviation, 3f);
            config.SetCVar(RMCCVars.RMCGunPredictionAabbEnlargement, 0f);
            var timing = pair.Server.ResolveDependency<IGameTiming>();
            var transform = entities.System<SharedTransformSystem>();
            var projectile = entities.SpawnEntity("CMUTestLagCompensationBody", map.GridCoords);
            var target = entities.SpawnEntity("CMUTestLagCompensationBody", map.GridCoords);
            var history = entities.GetComponent<LagCompensationComponent>(target);
            var prediction = new PredictedProjectileServerComponent { Shooter = sessions[0] };
            entities.AddComponent(projectile, prediction);
            Assert.That(sessions[0].Channel.Ping, Is.Zero, "The fixture requests the current snapshot without a latency offset.");
            Entity<PredictedProjectileServerComponent, PhysicsComponent> shot =
                (projectile, prediction, entities.GetComponent<PhysicsComponent>(projectile));
            Entity<LagCompensationComponent, FixturesComponent, PhysicsComponent, TransformComponent> victim =
                (target, history, entities.GetComponent<FixturesComponent>(target),
                    entities.GetComponent<PhysicsComponent>(target), entities.GetComponent<TransformComponent>(target));
            var collision = typeof(GunPredictionSystem).GetMethod("Collides", BindingFlags.Instance | BindingFlags.NonPublic)!;
            bool Collides(MapCoordinates? reported = null) =>
                (bool) collision.Invoke(entities.System<GunPredictionSystem>(), new object?[] { shot, victim, reported })!;

            history.Positions.Clear();
            history.Positions.Enqueue((timing.CurTime - timing.TickPeriod, map.GridCoords, Angle.Zero));
            history.Positions.Enqueue((timing.CurTime + timing.TickPeriod, map.GridCoords.Offset(new Vector2(8, 0)), Angle.Zero));
            Assert.That(Collides(), Is.True, "A future movement sample must not replace the last known position.");

            history.Positions.Clear();
            history.Positions.Enqueue((timing.CurTime, map.GridCoords.Offset(new Vector2(8, 0)), Angle.Zero));
            history.Positions.Enqueue((timing.CurTime, map.GridCoords, Angle.Zero));
            Assert.That(Collides(), Is.True, "The final movement at an equal timestamp describes the snapshot.");

            transform.SetCoordinates(projectile, map.GridCoords.Offset(new Vector2(2, 0)));
            var reported = transform.GetMapCoordinates(projectile);
            Assert.That(Collides(reported), Is.True, "An actual historical anchor enables the configured historical tolerance.");
            history.Positions.Clear();
            Assert.That(Collides(reported), Is.False, "Missing history must not turn the current-position fallback into extra hit allowance.");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StandaloneViewMessageKeepsTheReportedSnapshotTick()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        await pair.RunTicksSync(15);
        GameTick sentTick = default;
        await pair.Client.WaitPost(() =>
        {
            sentTick = pair.Client.ResolveDependency<IClientGameTiming>().LastRealTick;
            pair.Client.EntMan.System<SharedRMCLagCompensationSystem>().SendLastRealTick();
        });
        await pair.RunTicksSync(5);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(pair.Server.EntMan.System<SharedRMCLagCompensationSystem>().GetLastRealTick(pair.Player!.UserId),
                Is.EqualTo(sentTick), "The applied snapshot tick must not be decremented in transit.");
        });
        await pair.CleanReturnAsync();
    }
}
