using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Smoke;
using Content.Shared._RMC14.Xenonids.Bombard;
using Content.Shared._RMC14.Xenonids.Construction.Tunnel;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.ZLevels;

[TestFixture]
public sealed class CMUXenoZLevelTest : GameTest
{
    private EntityUid _lower, _upper, _sky, _unrelated, _network, _xeno;
    private SharedMapSystem _maps = null!;

    [SetUp]
    public async Task SetupMaps()
    {
        await Server.WaitAssertion(() =>
        {
            _maps = SEntMan.System<SharedMapSystem>();
            _lower = _maps.CreateMap(runMapInit: true);
            _upper = _maps.CreateMap(runMapInit: true);
            _unrelated = _maps.CreateMap(runMapInit: true);
            _sky = _maps.CreateMap(runMapInit: true);
            var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            foreach (var map in new[] { _lower, _upper, _unrelated })
            {
                var grid = SEntMan.EnsureComponent<MapGridComponent>(map);
                for (var x = -2; x <= 12; x++)
                for (var y = -2; y <= 2; y++)
                    _maps.SetTile(map, grid, new Vector2i(x, y), floor);
            }
            var z = SEntMan.System<CMUZLevelsSystem>();
            var network = z.CreateZNetwork();
            _network = network;
            Assert.That(z.TryAddMapsIntoZNetwork(network, new() { [_lower] = 0, [_upper] = 1, [_sky] = 2 }), Is.True);
        });
    }

    [TearDown]
    public async Task CleanupMaps()
    {
        await Server.WaitPost(() => { if (_xeno.IsValid()) SEntMan.DeleteEntity(_xeno); });
        foreach (var map in new[] { _sky, _upper, _lower, _unrelated, _network })
            await Pair.DeleteEntityTreeLeafFirst(map);
        _xeno = default;
    }

    [TestCase("same")]
    [TestCase("linked")]
    [TestCase("unrelated")]
    [TestCase("otherHive")]
    [TestCase("leave")]
    public async Task TunnelTravelCompletesOnlyToAnAvailableHiveDestination(string scenario)
    {
        EntityUid source = default, destination = default;
        SharedContainerSystem containers = null!;
        await Server.WaitAssertion(() =>
        {
            containers = SEntMan.System<SharedContainerSystem>();
            var hive = SEntMan.SpawnEntity("CMXenoHive", new EntityCoordinates(_lower, Vector2.Zero));
            var destinationHive = scenario == "otherHive"
                ? SEntMan.SpawnEntity("CMXenoHive", new EntityCoordinates(_upper, Vector2.Zero)) : hive;
            var destinationMap = scenario switch { "same" => _lower, "unrelated" => _unrelated, _ => _upper };
            var tunnels = SEntMan.System<XenoTunnelSystem>();
            Assert.That(tunnels.TryPlaceTunnel(hive, "source", new EntityCoordinates(_lower, new Vector2(0.5f)), out var first), Is.True);
            Assert.That(tunnels.TryPlaceTunnel(destinationHive, "destination", new EntityCoordinates(destinationMap, new Vector2(4.5f, 0.5f)), out var second), Is.True);
            source = first!.Value;
            destination = second!.Value;
            _xeno = SEntMan.SpawnEntity("CMXenoDrone", new EntityCoordinates(_lower, new Vector2(0.5f)));
            SEntMan.System<SharedXenoHiveSystem>().SetHive(_xeno, hive);
            var container = containers.EnsureContainer<Container>(source, XenoTunnelComponent.ContainedMobsContainerId);
            Assert.That(containers.Insert(_xeno, container), Is.True);
            var message = new TraverseXenoTunnelMessage(SEntMan.GetNetEntity(destination))
            {
                Actor = _xeno, Entity = SEntMan.GetNetEntity(source), UiKey = SelectDestinationTunnelUI.Key,
            };
            SEntMan.EventBus.RaiseLocalEvent(source, message);
            if (scenario == "leave")
                SEntMan.System<SharedTransformSystem>().SetCoordinates(_xeno, new EntityCoordinates(_lower, new Vector2(1.5f)));
        });
        await Pair.RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(containers.ContainsEntity(destination, _xeno), Is.EqualTo(scenario is "same" or "linked"));
            if (scenario is "unrelated" or "otherHive")
                Assert.That(containers.ContainsEntity(source, _xeno), Is.True);
        });
    }

#pragma warning disable RA0002 // Exercise the actual action with controlled aim and plasma.
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task BombardRetainsItsFullLandingRange(bool lookUp, bool floorAboveSource)
    {
        await Server.WaitAssertion(() =>
        {
            if (lookUp)
            {
                var grid = SComp<MapGridComponent>(_upper);
                for (var x = -2; x <= 12; x++)
                for (var y = -2; y <= 2; y++)
                    if (!floorAboveSource || x != 0 || y != 0)
                        _maps.SetTile(_upper, grid, new Vector2i(x, y), Tile.Empty);
            }
            _xeno = SEntMan.SpawnEntity("RMCXenoBoiler", new EntityCoordinates(_lower, new Vector2(0.5f)));
            SEntMan.EnsureComponent<CMUZLevelViewerComponent>(_xeno).LookUp = lookUp;
            SComp<XenoPlasmaComponent>(_xeno).Plasma = 1000;
            var bombard = SComp<XenoBombardComponent>(_xeno);
            var action = SEntMan.System<SharedRMCActionsSystem>().GetActionsWithEvent<XenoBombardActionEvent>(_xeno).Single();
            var ev = new XenoBombardActionEvent
            {
                Performer = _xeno, Action = action,
                Target = new EntityCoordinates(_lower, new Vector2(100.5f, 0.5f)),
            };
            SEntMan.EventBus.RaiseLocalEvent(_xeno, ev);
            Assert.That(ev.Handled, Is.True);
            var pending = (XenoBombardDoAfterEvent) SComp<DoAfterComponent>(_xeno).DoAfters.Values.Single().Args.Event;
            Assert.That(pending.Coordinates.Position, Is.EqualTo(new Vector2(0.5f + bombard.Range, 0.5f)));
            Assert.That(pending.Coordinates.MapId, Is.EqualTo(SComp<MapComponent>(lookUp ? _upper : _lower).MapId));
        });
    }
#pragma warning restore RA0002

    [TestCase("RMCSmokeAcid", 3, true)]
    [TestCase("RMCSmokeNeurotoxin", 4, true)]
    [TestCase("RMCSmokeAcid", 3, false)]
    [TestCase("RMCSmokeNeurotoxin", 4, false)]
    [TestCase("RMCSmokeAcid", 3, true, true)]
    [TestCase("RMCSmokeNeurotoxin", 4, true, true)]
    public async Task BombardGasSpreadsAcrossAirAndFloorsWithoutDuplicates(string prototype, int range, bool air, bool gridless = false)
    {
        var map = gridless ? _sky : _upper;
        var center = air ? new Vector2(32.5f, 0.5f) : new Vector2(4.5f, 0.5f);
        await Server.WaitPost(() =>
        {
            // Air is outside all existing tile chunks, not just a hole in a populated chunk.
            if (!air)
            {
                var grid = SComp<MapGridComponent>(_upper);
                var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
                for (var x = 0; x <= 9; x++)
                for (var y = -5; y <= 5; y++)
                    _maps.SetTile(_upper, grid, new Vector2i(x, y), floor);
            }
            SEntMan.SpawnEntity(prototype, new EntityCoordinates(map, center));
        });
        await Pair.RunSeconds(6);
        await Server.WaitAssertion(() =>
        {
            var clouds = SEntMan.EntityQuery<EvenSmokeComponent, TransformComponent>()
                .Where(e => e.Item2.MapUid == map).ToArray();
            var positions = clouds.Select(e => SEntMan.System<SharedTransformSystem>().GetMapCoordinates(e.Item1.Owner).Position).ToArray();
            Assert.That(positions.Length, Is.EqualTo(1 + 2 * range * (range + 1)));
            Assert.That(positions.Distinct().Count(), Is.EqualTo(positions.Length), "Converging gas fronts must not create duplicate clouds.");
            Assert.That(positions.All(p => Math.Abs(p.X - center.X) + Math.Abs(p.Y - center.Y) <= range + 0.01f), Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task GasDoesNotPassThroughAirtightWallsOrSpreadIntoUnlinkedSpace(bool gridless)
    {
        await Server.WaitPost(() =>
        {
            var map = gridless ? _sky : _upper;
            var grid = gridless ? _maps.CreateGridEntity(SComp<MapComponent>(_sky).MapId)
                : new Entity<MapGridComponent>(_upper, SComp<MapGridComponent>(_upper));
            var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            for (var y = -4; y <= 4; y++)
            {
                _maps.SetTile(grid, new Vector2i(1, y), floor);
                SEntMan.SpawnEntity("WallSolid", new EntityCoordinates(grid, new Vector2(1.5f, y + 0.5f)));
            }
            SEntMan.SpawnEntity("RMCSmokeAcid", new EntityCoordinates(map, new Vector2(0.5f)));
            SEntMan.SpawnEntity("RMCSmokeAcid", new EntityCoordinates(_unrelated, new Vector2(32.5f)));
        });
        await Pair.RunSeconds(5);
        await Server.WaitAssertion(() =>
        {
            var transform = SEntMan.System<SharedTransformSystem>();
            var map = gridless ? _sky : _upper;
            var clouds = SEntMan.EntityQuery<EvenSmokeComponent, TransformComponent>().ToArray();
            Assert.That(clouds.Where(e => e.Item2.MapUid == map).All(e => transform.GetWorldPosition(e.Item2).X < 1), Is.True);
            Assert.That(clouds.Count(e => e.Item2.MapUid == _unrelated), Is.EqualTo(1));
        });
    }
}
