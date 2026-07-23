using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Server.Shuttles.Components;
using Content.Shared.CCVar;
using Content.Shared.Doors.Components;
using Content.Shared._RMC14.Evacuation;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Server.GameObjects;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class HunterShipDockingTest
{
    [Test]
    public async Task HunterShuttleUsesInvisibleLandingPadMarker()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;
            var marker = prototypes.Index<EntityPrototype>("CMUHunterShipMarkerDockingPort");
            var landingPad = prototypes.Index<EntityPrototype>("CMUHunterShipYautjaLandingPadA");

            Assert.Multiple(() =>
            {
                Assert.That(marker.TryGetComponent<DoorComponent>(out _, factory), Is.False,
                    "The landing-pad docking marker must not be a door.");
                Assert.That(marker.TryGetComponent<AirlockComponent>(out _, factory), Is.False,
                    "The landing-pad docking marker must not be an airlock.");
                Assert.That(landingPad.TryGetComponent<GridSpawnerComponent>(out var spawner, factory), Is.True);
                Assert.That(spawner!.Spawn, Is.EqualTo(new ResPath("/Maps/_CMU14/Shuttles/hunter_shuttle.yml")));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShuttleUsesFullHullLayout()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
        });

        var server = pair.Server;
        var entMan = server.EntMan;
        var ticker = entMan.System<GameTicker>();
        var prototypes = server.ResolveDependency<IPrototypeManager>();

        server.CfgMan.SetCVar(CCVars.GridFill, true);

        await server.WaitAssertion(() =>
        {
            var map = prototypes.Index<GameMapPrototype>("CMUYautjaHunterShip");
            var options = DeserializationOptions.Default with { InitializeMaps = true };
            Assert.DoesNotThrow(() => ticker.LoadGameMap(map, out _, options));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var shuttles = entMan.EntityQueryEnumerator<ShuttleComponent, MapGridComponent, MetaDataComponent>();
            EntityUid shuttle = default;
            MapGridComponent? grid = null;

            while (shuttles.MoveNext(out var uid, out _, out var shuttleGrid, out var metadata))
            {
                if (metadata.EntityName != "Hunter Shuttle")
                    continue;

                shuttle = uid;
                grid = shuttleGrid;
                break;
            }

            Assert.That(shuttle, Is.Not.EqualTo(EntityUid.Invalid), "The roundstart Hunter Shuttle must be loaded.");
            Assert.That(grid, Is.Not.Null);
            Assert.That(grid!.LocalAABB.Width, Is.EqualTo(7f), $"Loaded shuttle AABB: {grid.LocalAABB}");
            Assert.That(grid.LocalAABB.Height, Is.EqualTo(13f), $"Loaded shuttle AABB: {grid.LocalAABB}");

            var required = new[]
            {
                "CMUYautjaHunterShuttleConsole",
                "CMUYautjaHunterShuttleHealthMonitor",
                "CMUYautjaHunterShuttleSupplyTerminal",
                "CMUYautjaHunterShuttleAirlock",
                "CMUYautjaHunterShuttleInteriorAirlock",
                "CMUYautjaHunterShuttleLockdownButton",
                "CMUYautjaHunterShuttleLockdownShutter",
                "CMUYautjaStructureHunterShuttleStanLeftengine",
                "CMUYautjaStructureHunterShuttleStanRightengine",
            };

            foreach (var requiredPrototype in required)
            {
                Assert.That(CountGridPrototype(entMan, shuttle, requiredPrototype), Is.GreaterThan(0),
                    $"The original Hunter Shuttle port must contain {requiredPrototype}.");
            }

            var sourceVisuals = new[]
            {
                "CMUYautjaStructureHunterShuttleHunter4",
                "CMUYautjaStructureHunterShuttleHunter18",
                "CMUYautjaStructureHunterShuttleHunter20",
                "CMUYautjaStructureHunterShuttleHunter21",
                "CMUYautjaStructureHunterShuttleHunter22",
                "CMUYautjaStructureHunterShuttleHunter25",
                "CMUYautjaStructureHunterShuttleHunter26",
                "CMUYautjaStructureHunterShuttleHunter27",
                "CMUYautjaStructureHunterShuttleHunter28",
                "CMUYautjaStructureHunterShuttleLeftengine2",
                "CMUYautjaStructureHunterShuttleLeftengine3",
                "CMUYautjaStructureHunterShuttleLeftengine4",
                "CMUYautjaStructureHunterShuttleLeftengine5",
                "CMUYautjaStructureHunterShuttleLeftengine7",
                "CMUYautjaStructureHunterShuttleLeftengine8",
                "CMUYautjaStructureHunterShuttleLeftengine9",
                "CMUYautjaStructureHunterShuttleRightengine2",
                "CMUYautjaStructureHunterShuttleRightengine3",
                "CMUYautjaStructureHunterShuttleRightengine4",
                "CMUYautjaStructureHunterShuttleRightengine5",
                "CMUYautjaStructureHunterShuttleRightengine7",
                "CMUYautjaStructureHunterShuttleRightengine8",
                "CMUYautjaStructureHunterShuttleRightengine9",
                "CMUYautjaStructureHunterShuttleHunter2",
                "CMUYautjaStructureHunterShuttleHunter5",
                "CMUYautjaStructureHunterShuttleHunter13",
                "CMUYautjaStructureHunterShuttleHunter14",
                "CMUYautjaStructureHunterShuttleHunter17",
                "CMUYautjaStructureHunterShuttleHunter19",
                "CMUYautjaStructureHunterShuttleHunter23",
                "CMUYautjaStructureHunterShuttleHunter24",
                "CMUYautjaStructureHunterShuttleHunterw2",
                "CMUYautjaStructureHunterShuttleHunterw3",
                "CMUYautjaStructureHunterShuttleHunterw4",
                "CMUYautjaStructureHunterShuttleHunterw8",
                "CMUYautjaStructureHunterShuttleLeftengine1",
                "CMUYautjaStructureHunterShuttleLeftengine6",
                "CMUYautjaStructureHunterShuttleRightengine1",
                "CMUYautjaStructureHunterShuttleRightengine6",
            };

            foreach (var sourceVisual in sourceVisuals)
            {
                Assert.That(CountGridPrototype(entMan, shuttle, sourceVisual), Is.GreaterThan(0),
                    $"The source DMM visual {sourceVisual} must be represented on the loaded shuttle.");
            }

            Assert.That(
                GridPrototypeTransforms(entMan, shuttle, "CMUHunterShipWallTurfClosedWallHuntershipHunterBase"),
                Is.Empty,
                "CMSS13 shuttle_border cells are open decorative turfs, not full Hunter Ship walls.");

            Assert.That(
                GridPrototypeTransforms(entMan, shuttle, "CMUYautjaHunterShuttleDockingPort"),
                Is.Empty,
                "The source-port shuttle does not place a separate mobile docking marker inside the hull.");

            AssertGridPrototypeLayout(
                entMan,
                shuttle,
                "CMUYautjaHunterShuttleAirlock",
                (new Vector2(0.5f, 8.5f), Direction.South),
                (new Vector2(6.5f, 8.5f), Direction.South),
                (new Vector2(3.5f, 2.5f), Direction.North));

            AssertGridPrototypeLayout(
                entMan,
                shuttle,
                "CMUYautjaHunterShuttleInteriorAirlock",
                (new Vector2(3.5f, 5.5f), Direction.North));

            AssertGridPrototypeLayout(
                entMan,
                shuttle,
                "CMUYautjaHunterShuttleLockdownShutter",
                (new Vector2(0.5f, 8.5f), Direction.East),
                (new Vector2(6.5f, 8.5f), Direction.East),
                (new Vector2(3.5f, 2.5f), Direction.South));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShuttleUsesInvisibleSourceTilesAndCornerHullParts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var loader = entMan.System<MapLoaderSystem>();
            var tiles = server.ResolveDependency<ITileDefinitionManager>();

            Assert.That(loader.TryLoadGrid(map.MapId, new ResPath("/Maps/_CMU14/Shuttles/hunter_shuttle.yml"), out var grid), Is.True);
            Assert.That(grid, Is.Not.Null);
            var shuttle = grid!.Value.Owner;

            Assert.Multiple(() =>
            {
                AssertTileAt(entMan, tiles, shuttle, new Vector2i(1, 9), "CMShuttleTileInvisible",
                    "hunter_tile_2/north");
                AssertTileAt(entMan, tiles, shuttle, new Vector2i(2, 11), "CMShuttleTileInvisible",
                    "hunter_tile_2/west");
                AssertTileAt(entMan, tiles, shuttle, new Vector2i(2, 9), "CMShuttleTileInvisible",
                    "shuttle_border");
                AssertTileAt(entMan, tiles, shuttle, new Vector2i(3, 10), "CMShuttleTileInvisible",
                    "shuttle_border/north");
                AssertTileAt(entMan, tiles, shuttle, new Vector2i(4, 10), "CMShuttleTileInvisible",
                    "shuttle_border_corner/west");

                AssertSourceShuttlePart(entMan, shuttle, "CMUYautjaStructureHunterShuttleHunter4",
                    new Vector2(0.5f, 9.5f), "left hull above external door");
                AssertSourceShuttlePart(entMan, shuttle, "CMUYautjaStructureHunterShuttleHunter18",
                    new Vector2(0.5f, 10.5f), "left front corner");
                AssertSourceShuttlePart(entMan, shuttle, "CMUYautjaStructureHunterShuttleHunter20",
                    new Vector2(1.5f, 12.5f), "left front outer corner");
                AssertSourceShuttlePart(entMan, shuttle, "CMUYautjaStructureHunterShuttleHunter25",
                    new Vector2(6.5f, 10.5f), "right front corner");
                AssertSourceShuttlePart(entMan, shuttle, "CMUYautjaStructureHunterShuttleHunter26",
                    new Vector2(5.5f, 12.5f), "right front outer corner");
            });
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertGridPrototypeLayout(
        IEntityManager entMan,
        EntityUid grid,
        string prototypeId,
        params (Vector2 Position, Direction Direction)[] expected)
    {
        var actual = GridPrototypeTransforms(entMan, grid, prototypeId);
        Assert.That(actual, Is.EquivalentTo(expected), prototypeId);
    }

    private static IReadOnlyList<(Vector2 Position, Direction Direction)> GridPrototypeTransforms(
        IEntityManager entMan,
        EntityUid grid,
        string prototypeId)
    {
        var result = new List<(Vector2 Position, Direction Direction)>();
        var entities = entMan.EntityQueryEnumerator<TransformComponent, MetaDataComponent>();

        while (entities.MoveNext(out _, out var xform, out var metadata))
        {
            if (xform.GridUid != grid || metadata.EntityPrototype?.ID != prototypeId)
                continue;

            result.Add((xform.LocalPosition, xform.LocalRotation.GetCardinalDir()));
        }

        return result;
    }

    private static void AssertTileAt(
        IEntityManager entMan,
        ITileDefinitionManager tiles,
        EntityUid grid,
        Vector2i indices,
        string expectedPrototype,
        string sourcePath)
    {
        var map = entMan.System<SharedMapSystem>();
        var gridComp = entMan.GetComponent<MapGridComponent>(grid);
        var expected = tiles[expectedPrototype];
        var tile = map.GetTileRef(grid, gridComp, indices).Tile;

        Assert.That(tile.TypeId, Is.EqualTo(expected.TileId),
            $"Source /turf/open/predship/{sourcePath} must use {expectedPrototype} at {indices}.");
    }

    private static void AssertSourceShuttlePart(
        IEntityManager entMan,
        EntityUid grid,
        string prototype,
        Vector2 localPosition,
        string sourcePart)
    {
        var query = entMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out _, out var meta, out var xform))
        {
            if (xform.GridUid == grid &&
                meta.EntityPrototype?.ID == prototype &&
                (xform.LocalPosition - localPosition).Length() < 0.001f)
            {
                return;
            }
        }

        Assert.Fail($"Source Hunter Shuttle part {sourcePart} must use {prototype} at {localPosition}.");
    }

    private static int CountGridPrototype(IEntityManager entMan, EntityUid grid, string prototypeId)
    {
        var count = 0;
        var entities = entMan.EntityQueryEnumerator<TransformComponent, MetaDataComponent>();
        while (entities.MoveNext(out _, out var xform, out var metadata))
        {
            if (xform.GridUid == grid && metadata.EntityPrototype?.ID == prototypeId)
                count++;
        }

        return count;
    }

}
