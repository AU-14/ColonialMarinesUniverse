#nullable enable
#pragma warning disable RA0002 // the test pumps fuel straight into the tracker instead of waiting minutes for real pumps
using System.Linq;
using Content.Server._RMC14.Evacuation;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Server.Atmos.Components;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Evacuation;
using Content.Shared._RMC14.Rules;
using Content.Shared.Chasm;
using Content.Shared.CMU14.Ship;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Physics.Systems;

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
        var vacatedBerths = new List<MapCoordinates>();
        var entrants = new List<EntityUid>();
        EntityUid planetMap = default;
        List<EntityUid> decks = new();
        MapCoordinates? vacantBerth = null;
        MapCoordinates? berthApproach = null;
        EntityUid berthVisitor = default;
        EntityUid berthControl = default;

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

                // CMU14: use a real part of the berth which is open space beneath the lifeboat.
                if (vacantBerth == null && xform.MapUid is { } berthMap
                    && entities.TryGetComponent<MapGridComponent>(berthMap, out var shipGrid))
                {
                    foreach (var tile in maps.GetAllTiles(grid, boatGrid))
                    {
                        var world = maps.GridTileToWorld(grid, boatGrid, tile.GridIndices);
                        var shipTile = maps.WorldToTile(berthMap, shipGrid, world.Position);
                        if (!maps.TryGetTileRef(berthMap, shipGrid, shipTile, out var below) || below.Tile.IsEmpty
                            || tiles[below.Tile.TypeId].ID == "Space")
                        {
                            vacantBerth = world;
                            break;
                        }
                    }

                    if (vacantBerth is { } berth)
                    {
                        var closest = float.MaxValue;
                        foreach (var tile in maps.GetAllTiles(berthMap, shipGrid))
                        {
                            if (((ContentTileDefinition) tiles[tile.Tile.TypeId]).MapAtmosphere
                                || maps.GetAnchoredEntities(berthMap, shipGrid, tile.GridIndices).Any())
                            {
                                continue;
                            }

                            var world = maps.GridTileToWorld(berthMap, shipGrid, tile.GridIndices);
                            var distance = (world.Position - berth.Position).LengthSquared();
                            if (distance >= closest)
                                continue;

                            closest = distance;
                            berthApproach = world;
                        }
                    }
                }

                TestContext.Out.WriteLine($"lifeboat {computer} grid {grid} on {xform.MapUid}; enabled {comp.Enabled}; fuel read {evacuation.GetEvacuationProgress(grid)}; trackers {entities.EntityQuery<EvacuationProgressComponent>().Count()}; passenger {passenger}");
                Assert.That(evacuation.IsEvacuationComplete(grid), Is.True,
                    "the lifeboat must read the tank the pumps filled, not an empty duplicate");
                lifeboats.Add((computer, grid, passenger));
                if (passenger is { } occupant)
                    vacatedBerths.Add(entities.System<SharedTransformSystem>().GetMapCoordinates(occupant));
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

        // one entrant at a time: a human and a runner sharing a tile bumped into each other on slow CI runners
        foreach (var prototype in new[] { "CMMobHuman", "CMXenoRunner" })
        {
            entrants.Clear();
            await server.WaitPost(() =>
            {
                foreach (var berth in vacatedBerths)
                {
                    var entrant = entities.SpawnEntity(prototype, berth.Offset(new Vector2(-1, 0)));
                    // Isolate berth collision damage from the exposed dock's vacuum.
                    entities.RemoveComponent<BarotraumaComponent>(entrant);
                    entrants.Add(entrant);
                    entities.System<SharedPhysicsSystem>().SetLinearVelocity(entrant, new Vector2(2, 0));
                }
            });

            await pair.RunTicksSync(10);
            await server.WaitAssertion(() =>
            {
                foreach (var entrant in entrants)
                {
                    Assert.That(entities.IsQueuedForDeletion(entrant) || !entities.EntityExists(entrant), Is.False,
                        $"Entering a vacated lifeboat berth must not gib or delete the {prototype} entrant.");
                    var damageable = entities.GetComponent<DamageableComponent>(entrant);
                    var damage = entities.System<DamageableSystem>().GetAllDamage((entrant, damageable));
                    var types = string.Join(", ", damage.DamageDict.Where(d => d.Value > FixedPoint2.Zero).Select(d => $"{d.Key} {d.Value}"));
                    Assert.That(damage.GetTotal(), Is.EqualTo(FixedPoint2.Zero),
                        $"A vacated berth must not damage the {prototype} entrant (took {types}).");
                    entities.DeleteEntity(entrant);
                }
            });
        }

        await server.WaitAssertion(() =>
        {
            Assert.That(vacantBerth, Is.Not.Null, "the lifeboat must leave an exposed berth to exercise this regression");
            Assert.That(berthApproach, Is.Not.Null, "the visitor needs a nearby real ship floor to leave");
            berthControl = entities.SpawnEntity("CMMobHuman", berthApproach!.Value);
            berthVisitor = entities.SpawnEntity("CMMobHuman", berthApproach!.Value);
            Assert.That(entities.System<CMUZeroGSpaceSystem>().IsOverZeroGSpace(berthVisitor), Is.False);
            entities.System<SharedTransformSystem>().SetMapCoordinates(berthVisitor, vacantBerth!.Value);
            Assert.That(entities.System<CMUZeroGSpaceSystem>().IsOverZeroGSpace(berthVisitor), Is.True);
        });

        await pair.RunSeconds(7);
        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entities.Deleted(berthVisitor), Is.False, "entering the vacated berth must not delete the visitor");
                Assert.That(entities.HasComponent<ChasmFallingComponent>(berthVisitor), Is.False,
                    "open space beside a departed lifeboat is zero-g, not a lethal bottomless chasm");
                Assert.That(entities.GetComponent<MobStateComponent>(berthVisitor).CurrentState, Is.Not.EqualTo(MobState.Dead),
                    "the vacated berth must remain a playable area");
                Assert.That(entities.GetComponent<MobStateComponent>(berthControl).CurrentState, Is.Not.EqualTo(MobState.Dead),
                    "the adjacent approach floor must remain safe after departure");
            });
        });

        await pair.CleanReturnAsync();
    }
}
