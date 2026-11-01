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

    [TestCase(false)]
    [TestCase(true)]
    public async Task UnchangedMovementReusesGroundQuery(bool vehicle)
    {
        await Server.WaitAssertion(() =>
        {
            CreateScenario(vehicle);
            var before = Floors;
            Assert.That(_z.DistanceToGround((_body, _bodyZ), out var sticky), Is.Zero);
            Assert.That(sticky, Is.False);
            var oneQuery = Floors - before;
            Assert.That(oneQuery, Is.GreaterThan(0));

            foreach (var x in new[] { 1.05f, 0.95f })
            {
                before = Floors;
                Move(x);
                Assert.That(Floors - before, Is.EqualTo(oneQuery),
                    "An unchanged snap and its activation must share one ground query, including the whole vehicle footprint.");
                AssertSettled(_map.Owner, 0f);
                Assert.That(_transform.GetWorldPosition(_body), Is.EqualTo(new Vector2(x, 0.5f)));
            }
        });
    }

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

    [Test, Explicit("Run by exact method filter to record Release movement and footprint costs.")]
    public async Task MeasureMovementAndFootprint()
    {
        var rows = new List<object>();
        await Server.WaitAssertion(() =>
        {
            CreateScenario(false);
            foreach (var vehicle in new[] { false, true })
            {
                if (vehicle)
                {
                    SEntMan.DeleteEntity(_body);
                    CreateBody(true);
                }
                Server.CfgMan.SetCVar(Robust.Shared.CVars.ProfEnabled, true);
                var floors = Floors;
                Move(1.05f);
                var floorsPerMove = Floors - floors;
                Server.CfgMan.SetCVar(Robust.Shared.CVars.ProfEnabled, false);
                var positions = new[]
                {
                    new EntityCoordinates(_map.Owner, 0.95f, 0.5f),
                    new EntityCoordinates(_map.Owner, 1.05f, 0.5f),
                };
                Measure(vehicle ? "vehicle-move" : "body-move", 4096, floorsPerMove,
                    i => _transform.SetCoordinates(_body, positions[i & 1]));
                AssertSettled(_map.Owner, 0f);
            }

            var samples = new List<Vector2>();
            Measure("vehicle-footprint", 4096, 0, _ => CMUVehicleSupportFootprint.GenerateWorldSamples(
                new Box2(-1f, -2f, 1f, 2f), 0.5f, 0.05f, new Vector2(0.95f, 0.5f),
                Angle.FromDegrees(37), samples));
            Assert.That(samples, Has.Count.EqualTo(45));

            void Measure(string name, int operations, int floorsPerMove, Action<int> action)
            {
                // Tiered compilation continues in the background. An iteration-only warmup
                // can finish sooner after optimization and put more Tier-0 work in that mode's
                // measured samples. Give each workload the same minimum elapsed warmup.
                var warmupStart = Stopwatch.GetTimestamp();
                var warmupOperations = 0;
                do
                {
                    for (var i = 0; i < operations; i++) action(i);
                    warmupOperations += operations;
                } while (Stopwatch.GetElapsedTime(warmupStart).TotalSeconds < 4);
                var warmupMilliseconds = Stopwatch.GetElapsedTime(warmupStart).TotalMilliseconds;
                var milliseconds = new double[9];
                var allocatedBytes = new long[9];
                for (var block = 0; block < milliseconds.Length; block++)
                {
                    var bytes = GC.GetAllocatedBytesForCurrentThread();
                    var start = Stopwatch.GetTimestamp();
                    for (var i = 0; i < operations; i++) action(i);
                    milliseconds[block] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    allocatedBytes[block] = GC.GetAllocatedBytesForCurrentThread() - bytes;
                }
                rows.Add(new { name, operations, floorsPerMove, warmupOperations, warmupMilliseconds,
                    milliseconds, allocatedBytes });
            }
        });

        var output = Environment.GetEnvironmentVariable("CMU_SIMULATION_MEASUREMENT") ??
                     Path.Combine(TestContext.CurrentContext.WorkDirectory, "simulation-ground-measurement.json");
        var engine = typeof(SharedPhysicsSystem).Assembly;
        File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            utc = DateTime.UtcNow,
            runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            processId = Environment.ProcessId,
            engineMvid = engine.ManifestModule.ModuleVersionId,
            engineDebugModes = engine.GetCustomAttribute<DebuggableAttribute>()?.DebuggingFlags.ToString(),
            contentMvid = typeof(CMUSharedZLevelsSystem).Assembly.ManifestModule.ModuleVersionId,
            serverMvid = typeof(CMUZLevelsSystem).Assembly.ManifestModule.ModuleVersionId,
            rows,
        }, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddTestAttachment(output);
    }

    private int Floors => (int) DistanceFloors.GetValue(_z)!;

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
