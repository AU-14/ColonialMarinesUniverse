using Content.Shared.Vehicle.Components;
using Content.Shared.Vehicle;
using Content.Shared._RMC14.Vehicle;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.CMU14.ZLevels.Vehicles;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.CMU14.ZLevels;

[TestFixture]
public sealed class CMUZMovementQueryTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: CMUTestGroundQueryBody
  components:
  - type: Physics
    bodyType: Dynamic
  - type: CMUZPhysics
    bounciness: 0

- type: entity
  id: CMUTestGroundQueryVehicle
  parent: CMUTestGroundQueryBody
  components:
  - type: CMUVehicleZTraversal
  - type: Fixtures
    fixtures:
      body:
        shape:
          !type:PhysShapeAabb
          bounds: '-1,-2,1,2'
        hard: true
        layer: 0
        mask: 0
";

    private static readonly FieldInfo DistanceFloors = typeof(CMUSharedZLevelsSystem)
        .GetField("_profileZDistanceFloors", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private readonly List<EntityUid> _cleanup = new();
    private CMUZLevelsSystem _z = default!;
    private SharedTransformSystem _transform = default!;
    private Entity<MapGridComponent> _map;
    private EntityUid _lower;
    private EntityUid _upper;
    private EntityUid _body;
    private CMUZPhysicsComponent _bodyZ = default!;
    private bool? _originalProfiler;

    [Test]
    public async Task ChangedHeightRequeriesGroundAfterSnap()
    {
        await Server.WaitAssertion(() =>
        {
            CreateScenario(false);
            AddSupport(0.4f);
            var before = Floors;
            Move(1.05f);
            Assert.That(Floors - before, Is.GreaterThanOrEqualTo(2),
                "A snap changes height, so activation cannot reuse the old distance.");
            AssertSettled(_map.Owner, 0.4f);
        });
    }

    [Test]
    public async Task NextMoveSeesRemovedFloorAndFallsToLowerLevel()
    {
        await Server.WaitAssertion(() =>
        {
            CreateScenario(false);
            Move(1.05f);
            AssertSettled(_map.Owner, 0f);
            Server.System<SharedMapSystem>().SetTile(_map, Vector2i.Zero, Tile.Empty);
            Move(0.95f);
            Assert.That(SEntMan.HasComponent<CMUZFallingComponent>(_body), Is.True,
                "The previous move's supported result must not survive a terrain edit.");
            Assert.That(SComp<TransformComponent>(_body).MapUid, Is.EqualTo(_map.Owner));
        });
        await Pair.RunTicksSync(80);
        await Server.WaitAssertion(() => AssertSettled(_lower, 0f));
    }

    [Test]
    public async Task StickyBoundarySnapQueriesTheDestinationLevel()
    {
        await Server.WaitAssertion(() =>
        {
            CreateScenario(false);
            AddSupport(1f);
            Move(1.05f);
            Assert.That(SComp<TransformComponent>(_body).MapUid, Is.EqualTo(_upper));
            Assert.That(_bodyZ.LocalPosition, Is.Zero.Within(0.0001f));
            Assert.That(_bodyZ.Velocity, Is.Zero);
            Assert.That(_z.DistanceToGround((_body, _bodyZ), out _), Is.Zero);
        });
        // Moving between maps can wake the body before its height is normalized. The final
        // ground check queues removal of that falling component at the normal lifecycle boundary.
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() => AssertSettled(_upper, 0f));
    }

    [Test]
    public async Task HumveeFallsWithTurretAndCanMoveAfterLanding()
    {
        await Server.WaitAssertion(() =>
        {
            CreateScenario(true);
            SEntMan.DeleteEntity(_body);
            _body = SEntMan.SpawnEntity("VehicleHumvee", new EntityCoordinates(_map.Owner, 0.5f, 0.5f));
            _cleanup.Add(_body);
            _bodyZ = SComp<CMUZPhysicsComponent>(_body);
            var maps = Server.System<SharedMapSystem>();
            for (var x = -3; x <= 3; x++)
            for (var y = -3; y <= 3; y++)
                maps.SetTile(_map, new Vector2i(x, y), Tile.Empty);
            _z.WakeZPhysics((_body, _bodyZ));
        });
        await Pair.RunTicksSync(100);
        await Server.WaitAssertion(() =>
        {
            AssertSettled(_lower, 0f);
            var mover = SComp<GridVehicleMoverComponent>(_body);
            Assert.That(mover.SyncedGrid, Is.EqualTo(_lower));
            var transform = SComp<TransformComponent>(_body);
            var position = _transform.GetWorldPosition(transform);
            Assert.That(Server.System<GridVehicleMoverSystem>().TryShoveVehicle(_body, _body, Vector2.UnitX), Is.True,
                "A landed vehicle must accept movement on its new floor.");
            Assert.That(_transform.GetWorldPosition(_body).X, Is.GreaterThan(position.X));
            var visuals = SEntMan.EntityQueryEnumerator<CMUZVisualFollowerComponent, TransformComponent>();
            var visualCount = 0;
            while (visuals.MoveNext(out _, out var follower, out var visualTransform))
            {
                if (follower.Target == _body)
                {
                    visualCount++;
                    Assert.That(visualTransform.MapUid, Is.EqualTo(_lower), "Turret visuals must follow the hull across floors.");
                }
            }
            Assert.That(visualCount, Is.GreaterThan(0), "The armed humvee must exercise its separate turret visuals.");
        });
    }

    private int Floors => (int) DistanceFloors.GetValue(_z)!;

    [Test]
    public async Task VehicleSupportTracksGeometryAndSamplingEditsAtAnEdge()
    {
        await Server.WaitAssertion(() =>
        {
            CreateScenario(true);
            var traversal = SComp<CMUVehicleZTraversalComponent>(_body);
            var fixtures = SComp<FixturesComponent>(_body);
            var samples = new List<Vector2>();
            var fixtureSystem = Server.System<FixtureSystem>();
            Move(5.8f);
            CheckSupport();

            fixtureSystem.DestroyFixture(_body, "body");
            var shape = new Robust.Shared.Physics.Collision.Shapes.PolygonShape();
            shape.SetAsBox(2.8f, .6f);
            Assert.That(fixtureSystem.TryCreateFixture(_body, shape, "body"), Is.True);
            CheckSupport();
            traversal.SupportSampleSpacing = .83f;
            CheckSupport();
            traversal.SupportSampleInset = .4f;
            CheckSupport();
            _transform.SetWorldRotation(_body, new Angle(.7));
            CheckSupport();
            return;

            void CheckSupport()
            {
                Assert.That(CMUVehicleSupportFootprint.TryGetFixtureLocalAabb(fixtures, out var bounds), Is.True);
                CMUVehicleSupportFootprint.GenerateWorldSamples(bounds, traversal.SupportSampleSpacing,
                    traversal.SupportSampleInset, _transform.GetWorldPosition(_body),
                    _transform.GetWorldRotation(_body), samples);
                var falling = SEntMan.HasComponent<CMUZFallingComponent>(_body);
                var supportedDistances = new List<float>();
                foreach (var sample in samples)
                {
                    var distance = _z.DistanceToGroundAtWorldPosition((_body, _bodyZ), sample, out var sticky);
                    if (CMUVehicleSupportFootprint.IsSampleSupported(distance, sticky,
                            traversal.MaxSupportStepHeight, traversal.SupportSnapDistance, falling))
                        supportedDistances.Add(distance);
                }
                Assert.That(supportedDistances.Count, Is.InRange(1, samples.Count - 1),
                    "The fixture must straddle the edge so a stale sample layout changes support.");
                // Exercise both sides of the rejection boundary, including equality (which remains supported).
                var boundary = 1f - supportedDistances.Count / (float) samples.Count;
                foreach (var threshold in new[] { MathF.Max(0, boundary - .01f), boundary })
                {
                    traversal.EdgeTipUnsupportedFraction = threshold;
                    SEntMan.RemoveComponent<CMUZFallingComponent>(_body);
                    var expected = boundary > threshold ? 1f : supportedDistances.Average();
                    var actual = _z.DistanceToGround((_body, _bodyZ), out var sticky);
                    Assert.That(actual, Is.EqualTo(expected).Within(.0001f),
                        "A full geometric scan and the cached/short-circuit query must agree after each edit.");
                    Assert.That(sticky, Is.False);
                }
            }
        });
    }

    private void CreateScenario(bool vehicle)
    {
        _originalProfiler = Server.CfgMan.GetCVar(Robust.Shared.CVars.ProfEnabled);
        Server.CfgMan.SetCVar(Robust.Shared.CVars.ProfEnabled, true);
        var maps = Server.System<SharedMapSystem>();
        var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
        var layers = new Dictionary<EntityUid, int>();
        for (var depth = -1; depth <= 1; depth++)
        {
            var map = maps.CreateMap(runMapInit: true);
            _cleanup.Add(map);
            var grid = SEntMan.EnsureComponent<MapGridComponent>(map);
            for (var x = -4; x <= 5; x++)
            for (var y = -4; y <= 4; y++)
                maps.SetTile(map, grid, new Vector2i(x, y), floor);
            layers.Add(map, depth);
            if (depth == -1) _lower = map;
            else if (depth == 1) _upper = map;
            else _map = (map, grid);
        }
        _z = Server.System<CMUZLevelsSystem>();
        _transform = Server.System<SharedTransformSystem>();
        var network = _z.CreateZNetwork();
        _cleanup.Add(network.Owner);
        Assert.That(_z.TryAddMapsIntoZNetwork(network, layers), Is.True);
        CreateBody(vehicle);
    }

    private void CreateBody(bool vehicle)
    {
        _body = SEntMan.SpawnEntity(vehicle ? "CMUTestGroundQueryVehicle" : "CMUTestGroundQueryBody",
            new EntityCoordinates(_map.Owner, 0.95f, 0.5f));
        _cleanup.Add(_body);
        _bodyZ = SComp<CMUZPhysicsComponent>(_body);
        Server.System<SharedPhysicsSystem>().SetBodyStatus(_body, SComp<PhysicsComponent>(_body), BodyStatus.OnGround);
    }

    private void AddSupport(float height)
    {
        var uid = SEntMan.SpawnEntity(null, new EntityCoordinates(_map.Owner, 1.5f, 0.5f));
        var support = SEntMan.AddComponent<CMUZLevelHighGroundComponent>(uid);
        support.HeightCurve = [height, height];
        support.Stick = true;
        Assert.That(_transform.AnchorEntity((uid, SComp<TransformComponent>(uid)), _map, new Vector2i(1, 0)), Is.True);
    }

    private void Move(float x) => _transform.SetCoordinates(_body, new EntityCoordinates(_map.Owner, x, 0.5f));

    private void AssertSettled(EntityUid map, float height)
    {
        Assert.Multiple(() =>
        {
            Assert.That(SComp<TransformComponent>(_body).MapUid, Is.EqualTo(map));
            Assert.That(_bodyZ.LocalPosition, Is.EqualTo(height).Within(0.0001f));
            Assert.That(_bodyZ.Velocity, Is.Zero);
            Assert.That(SEntMan.HasComponent<CMUZFallingComponent>(_body), Is.False);
            Assert.That(SComp<PhysicsComponent>(_body).BodyStatus, Is.EqualTo(BodyStatus.OnGround));
        });
    }

    [TearDown]
    public async Task CleanupScenario()
    {
        if (_originalProfiler is { } enabled)
            await Server.WaitPost(() => Server.CfgMan.SetCVar(Robust.Shared.CVars.ProfEnabled, enabled));
        for (var i = _cleanup.Count - 1; i >= 0; i--)
            await Pair.DeleteEntityTreeLeafFirst(_cleanup[i]);
    }
}
