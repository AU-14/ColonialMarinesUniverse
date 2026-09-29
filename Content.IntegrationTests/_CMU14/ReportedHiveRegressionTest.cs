using System.Linq;
using System.Numerics;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.GameTicking;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids.Construction.Events;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Maturing;
using Content.Shared.Actions.Components;
using Content.Shared.Destructible;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14;

[TestFixture]
public sealed class ReportedHiveRegressionTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: CMXenoDrone
          id: CMUTestReportedResinDrone
          components:
          - type: XenoConstruction
            buildChoice: WallXenoResin
        """;

    [TestCase("RMCGrassa1")]
    [TestCase("RMCGrassTallDesert")]
    [TestCase("RMCFloraTree01")]
    [TestCase("CMChair")]
    [TestCase("CMTable")]
    public async Task ResinClearsObstructionsOnlyAfterSuccessfulPlacement(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        EntityUid obstruction = default, neighbor = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var construction = entities.System<SharedXenoConstructionSystem>();
            var hives = entities.System<SharedXenoHiveSystem>();
            var hive = entities.SpawnEntity("CMXenoHive", map.GridCoords);
            var drone = entities.SpawnEntity("CMUTestReportedResinDrone", map.GridCoords);
            hives.SetHive(drone, hive);
            var target = map.GridCoords.Offset(new Vector2(1, 0));
            entities.System<SharedMapSystem>().SetTile(map.Grid, map.Grid.Comp, target,
                new Tile(pair.Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId));
            obstruction = entities.SpawnEntity(prototype, target);
            neighbor = entities.SpawnEntity(prototype, target.Offset(new Vector2(1, 0)));
            var action = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var actionComp = entities.AddComponent<ActionComponent>(action);
            var ev = new XenoSecreteStructureActionEvent { Performer = drone, Target = target, Action = (action, actionComp) };
            entities.EventBus.RaiseLocalEvent(drone, ev);
            Assert.That(entities.Deleted(obstruction), Is.False, "Failed construction must leave scenery intact.");
            Assert.That(construction.GetHiveInstantBuildsRemaining(hive), Is.EqualTo(200));
            hives.SetSameHive(drone, entities.SpawnEntity("XenoWeeds", target));
            ev = new XenoSecreteStructureActionEvent { Performer = drone, Target = target, Action = (action, actionComp) };
            entities.EventBus.RaiseLocalEvent(drone, ev);
            Assert.That(entities.EntityQuery<MetaDataComponent>().Count(meta => meta.EntityPrototype?.ID == "WallXenoResin"), Is.EqualTo(1));
            Assert.That(construction.GetHiveInstantBuildsRemaining(hive), Is.EqualTo(199));
        });
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(pair.Server.EntMan.Deleted(obstruction), Is.True);
            Assert.That(pair.Server.EntMan.Deleted(neighbor), Is.False, "Clearing a build tile must preserve nearby scenery.");
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task DestroyedCoreHonorsSetupGracePeriod(bool duringSetup)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var hive = entities.SpawnEntity("CMXenoHive", map.GridCoords);
            var hiveComp = entities.GetComponent<HiveComponent>(hive);
            Assert.That(hiveComp.PreSetupCutoff, Is.EqualTo(TimeSpan.FromMinutes(15)));
            var roundTime = entities.System<GameTicker>().RoundDuration();
            #pragma warning disable RA0002 // Place the round on either side of the cutoff without simulating fifteen minutes.
            hiveComp.PreSetupCutoff = roundTime + TimeSpan.FromSeconds(duringSetup ? 1 : -1);
            var timing = pair.Server.ResolveDependency<IGameTiming>();
            hiveComp.NewCoreAt = timing.CurTime + TimeSpan.FromHours(1);
            #pragma warning restore RA0002
            var core = entities.SpawnEntity(null, map.GridCoords);
            entities.AddComponent<HiveCoreComponent>(core);
            entities.System<SharedXenoHiveSystem>().SetHive(core, hive);
            entities.System<SharedDestructibleSystem>().DestroyEntity(core);
            Assert.That(hiveComp.NewCoreAt, Is.EqualTo(duringSetup ? timing.CurTime : timing.CurTime + hiveComp.NewCoreCooldown));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HijackingFromShipClearsAllPlanetLevelsAndPreservesPassengers()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var surface = await pair.CreateTestMap();
        var lower = await pair.CreateTestMap();
        var ship = await pair.CreateTestMap();
        EntityUid core = default, stranded = default, passenger = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var z = entities.System<CMUZLevelsSystem>();
            var surfaceMap = entities.GetComponent<TransformComponent>(surface.Grid).MapUid!.Value;
            var lowerMap = entities.GetComponent<TransformComponent>(lower.Grid).MapUid!.Value;
            var network = z.CreateZNetwork();
            Assert.That(z.TryAddMapsIntoZNetwork(network, new() { [surfaceMap] = 0, [lowerMap] = -1 }), Is.True);
            entities.EnsureComponent<RMCPlanetComponent>(surfaceMap);
            core = entities.SpawnEntity(null, lower.GridCoords);
            entities.AddComponent<HiveConstructionLimitedComponent>(core);
            stranded = entities.SpawnEntity("CMXenoDrone", lower.GridCoords);
            var container = entities.SpawnEntity(null, ship.GridCoords);
            passenger = entities.SpawnEntity("CMXenoDrone", new EntityCoordinates(container, Vector2.Zero));
            var queen = entities.SpawnEntity("CMXenoQueen", ship.GridCoords);
            var ev = new DropshipHijackStartEvent(ship.Grid);
            entities.EventBus.RaiseEvent(EventSource.Local, ref ev);
            Assert.That(entities.GetComponent<XenoMaturingComponent>(queen).MatureAt, Is.EqualTo(TimeSpan.Zero));
        });
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(pair.Server.EntMan.Deleted(core), Is.True);
            Assert.That(pair.Server.EntMan.Deleted(stranded), Is.True);
            Assert.That(pair.Server.EntMan.Deleted(passenger), Is.False);
        });
        await pair.CleanReturnAsync();
    }
}
