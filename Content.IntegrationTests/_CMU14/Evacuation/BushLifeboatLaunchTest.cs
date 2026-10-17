#nullable enable
#pragma warning disable RA0002 // the test pumps fuel straight into the tracker instead of waiting minutes for real pumps
using System.Linq;
using Content.Server._RMC14.Evacuation;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Evacuation;
using Content.Shared._RMC14.Rules;
using Content.Shared.Maps;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Evacuation;

// on the real USS Bush a full lifeboat still came down on the planet after a hijack
[TestFixture]
public sealed class BushLifeboatLaunchTest
{
    private static readonly ProtoId<GameMapPrototype> USSBushReduxMap = "USSBushRedux";

    // evacuateFirst: the CO hits evacuate during the hijack's red alert, before the dropship crashes into deck 1
    [TestCase(false)]
    [TestCase(true)]
    public async Task BushLifeboatEscapesAfterAFullTank(bool evacuateFirst)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var lifeboats = new List<(EntityUid Computer, EntityUid Grid, EntityUid? Passenger)>();
        EntityUid planetMap = default;
        List<EntityUid> decks = new();

        await server.WaitAssertion(() =>
        {
            var maps = entities.System<SharedMapSystem>();
            var ticker = entities.System<GameTicker>();
            var zLevels = entities.System<CMUZLevelsSystem>();
            ticker.LoadGameMap(server.ProtoMan.Index(USSBushReduxMap),
                out var mapId, DeserializationOptions.Default with { InitializeMaps = true });
            var ship = maps.GetMap(mapId);
            decks = zLevels.GetAllNetworkMaps(ship);

            var tiles = server.ResolveDependency<ITileDefinitionManager>();
            var floor = new Tile(tiles["FloorSteel"].TileId);
            planetMap = maps.CreateMap(out _);
            var planetGrid = entities.EnsureComponent<MapGridComponent>(planetMap);
            for (var x = -60; x <= 60; x++)
            for (var y = -60; y <= 60; y++)
                maps.SetTile(planetMap, planetGrid, new Vector2i(x, y), floor);
            entities.EnsureComponent<RMCPlanetComponent>(planetMap);

            var evacuation = entities.System<EvacuationSystem>();
            Assert.That(zLevels.TryGetZNetwork(ship, out var network), Is.True);
            var landingDeck = evacuateFirst ? network!.Value.Comp.ZLevels[1]!.Value : ship;
            if (evacuateFirst)
                evacuation.ToggleEvacuation(null, null, ship);

            var hijack = new DropshipHijackLandedEvent(landingDeck, VictimFaction: "govfor");
            entities.EventBus.RaiseEvent(EventSource.Local, ref hijack);

            // the pumps only ever fill the tracker the crash marked
            var trackers = entities.EntityQueryEnumerator<EvacuationProgressComponent>();
            while (trackers.MoveNext(out var progress))
            {
                if (progress.DropShipCrashed)
                    progress.Progress = progress.Required;
            }

            if (!evacuateFirst)
                evacuation.ToggleEvacuation(null, null, ship);

            var query = entities.AllEntityQueryEnumerator<LifeboatComputerComponent, TransformComponent>();
            while (query.MoveNext(out var computer, out var comp, out var xform))
            {
                if (!zLevels.IsSameZNetwork(xform.MapUid, ship) || xform.GridUid is not { } grid)
                    continue;

                var boatGrid = entities.GetComponent<MapGridComponent>(grid);
                var open = maps.GetAllTiles(grid, boatGrid)
                    .FirstOrDefault(t => !maps.GetAnchoredEntities(grid, boatGrid, t.GridIndices).Any());
                EntityUid? passenger = open.GridUid.IsValid()
                    ? entities.SpawnEntity("CMMobHuman", maps.GridTileToLocal(grid, boatGrid, open.GridIndices))
                    : null;

                TestContext.Out.WriteLine($"lifeboat {computer} grid {grid} on {xform.MapUid}; enabled {comp.Enabled}; fuel read {evacuation.GetEvacuationProgress(grid)}; trackers {entities.EntityQuery<EvacuationProgressComponent>().Count()}; passenger {passenger}");
                Assert.That(evacuation.IsEvacuationComplete(grid), Is.True,
                    "the lifeboat must read the tank the pumps filled, not an empty duplicate");
                lifeboats.Add((computer, grid, passenger));
            }

            Assert.That(lifeboats, Is.Not.Empty, "the Bush should have a lifeboat");
            foreach (var (computer, _, passenger) in lifeboats)
            {
                var launch = new LifeboatComputerLaunchBuiMsg { UiKey = LifeboatComputerUi.Key, Actor = passenger ?? computer };
                entities.EventBus.RaiseLocalEvent(computer, launch);
            }
        });

        await pair.RunSeconds(20);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var (_, grid, passenger) in lifeboats)
                {
                    var boatMap = entities.GetComponent<TransformComponent>(grid).MapUid;
                    var passengerXform = passenger is { } p ? entities.GetComponent<TransformComponent>(p) : null;
                    TestContext.Out.WriteLine($"lifeboat grid {grid} now on {boatMap} (planet {planetMap}, decks {string.Join(",", decks)}); passenger on {passengerXform?.MapUid} parent {passengerXform?.ParentUid}");
                    Assert.That(boatMap, Is.Not.EqualTo(planetMap), "a full lifeboat shouldn't crash-land");
                    Assert.That(decks, Does.Not.Contain(boatMap), "the lifeboat should have left the ship");
                    if (passengerXform != null)
                        Assert.That(passengerXform.MapUid, Is.Not.EqualTo(planetMap), "passengers shouldn't fall out onto the planet");
                }
            });
        });

        await pair.CleanReturnAsync();
    }
}
