using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Content.Client._RMC14.Interaction;
using Content.Client._RMC14.Dialog;
using Content.Client.Clickable;
using Content.Server.Maps;
using Content.Server.Power.Components;
using Content.Server.Spawners.Components;
using Content.Shared.Access.Components;
using Content.Shared._RMC14.Dialog;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Doors;
using Content.Shared._RMC14.Rules;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.VendingMachines;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaHuntingGroundMapTest
{
    private static readonly ResPath JungleMoonPath = new("/Maps/_CMU14/HuntingGrounds/jungle_moon.yml");
    private static readonly ResPath DesertMoonPath = new("/Maps/_CMU14/HuntingGrounds/desert_moon.yml");
    private static readonly ResPath DesertMoonCavesPath = new("/Maps/_CMU14/HuntingGrounds/desert_moon_caves.yml");
    private static readonly ResPath HunterShipPath = new("/Maps/_CMU14/huntership.yml");
    private static readonly ResPath[] HunterShipZLevelPaths =
    [
        new("/Maps/_CMU14/huntership.yml"),
        new("/Maps/_CMU14/huntership_upper.yml"),
        new("/Maps/_CMU14/huntership_lower.yml"),
    ];

    private static readonly string[] HunterShipHuntConsolePrototypes =
    [
        "CMUHunterShipPlacedCMUHunterShipFlightConsoleOverwatchSouthOffset0x13",
        "CMUHunterShipPlacedCMUHunterShipHuntsmastersConsoleOverwatchSouthOffsetNeg1x13",
        "CMUHunterShipPlacedCMUHunterShipBloodingConsoleOverwatchSouthOffsetNeg1x13",
    ];

    private static readonly string[] HunterShipDoorButtonPrototypes =
    [
        "CMUHunterShipPlacedRMCPodDoorButtonDoorctrlSouthOffset0x23",
        "CMUHunterShipPlacedRMCPodDoorButtonDoorctrlSouthOffset5x23",
        "CMUHunterShipPlacedRMCPodDoorButtonDoorctrlSouthOffsetNeg4x23",
        "CMUHunterShipPlacedRMCPodDoorButtonDoorctrlSouthOffsetNeg9x23",
        "CMUHunterShipPlacedRMCPodDoorButtonDoorctrlSouthOffset23x0",
        "CMUHunterShipPlacedRMCPodDoorButtonDoorctrlSouthOffset24x0",
        "CMUHunterShipPlacedRMCPodDoorButtonDoorctrlSouthOffsetNeg20x0",
        "CMUHunterShipPlacedRMCPodDoorButtonDoorctrlSouthOffsetNeg24x0",
    ];

    [Test]
    public async Task HunterShipElderQuartersRequireElderOrAboveAccess()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;
            var expected = new HashSet<string>
            {
                "CMUAccessYautjaAncient",
                "CMUAccessYautjaElder",
                "CMUAccessYautjaLeader",
            };

            foreach (var prototypeId in new[]
                     {
                         "CMUHunterShipObjStructureMachineryDoorAirlockYautjaSecureElderDoorClosedEast",
                         "CMUHunterShipObjStructureMachineryDoorAirlockYautjaSecureElderDoorClosedNorth",
                     })
            {
                var prototype = prototypes.Index<EntityPrototype>(prototypeId);
                Assert.That(prototype.TryGetComponent<AccessReaderComponent>(out var reader, factory), Is.True, prototypeId);

                var actual = reader!.AccessLists
                    .SelectMany(access => access)
                    .Select(access => access.Id)
                    .ToHashSet();

                Assert.That(actual, Is.EquivalentTo(expected), prototypeId);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InRotationPlanetMapsHaveGroundRelayMarkers()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.EntMan.ComponentFactory;
            var resources = server.ResolveDependency<IResourceManager>();
            var checkedPaths = new HashSet<ResPath>();
            var errors = new List<string>();

            foreach (var planetPrototype in prototypes.EnumeratePrototypes<EntityPrototype>())
            {
                if (!planetPrototype.TryComp<RMCPlanetMapPrototypeComponent>(out var planet, componentFactory) ||
                    !planet!.InRotation ||
                    !checkedPaths.Add(prototypes.Index<GameMapPrototype>(planet.MapId).MapPath))
                {
                    continue;
                }

                var map = prototypes.Index<GameMapPrototype>(planet.MapId);
                var markerCount = CountMapPrototypes(resources, map.MapPath)
                    .GetValueOrDefault("CMUYautjaGroundRelayDestination");
                if (markerCount < 1 || !ContainsLine(resources, map.MapPath, "kind: Ground"))
                    errors.Add($"{planetPrototype.ID} -> {map.ID} -> {map.MapPath}");
            }

            Assert.That(errors, Is.Empty,
                "In-rotation planet maps missing a CMUYautjaGroundRelayDestination marker:\n" +
                string.Join('\n', errors));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InRotationPlanetGroundRelaysAreAwayFromHumanStructures()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var server = pair.Server;

        await pair.LoadPrototypes(new List<string>
        {
            """
            - type: entity
              id: CMFlash
              name: test-only missing map prototype shim
            - type: entity
              id: RMCGrenadeFlashBang
              name: test-only missing map prototype shim
            """,
        });

        var errors = new List<string>();

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = entMan.ComponentFactory;
            var loader = entMan.System<MapLoaderSystem>();
            var mapSystem = entMan.System<SharedMapSystem>();
            var transform = entMan.System<SharedTransformSystem>();
            var turf = entMan.System<TurfSystem>();
            var loadedMaps = new List<EntityUid>();

            try
            {
                var mapPaths = GetInRotationPlanetMapPaths(prototypes, componentFactory);
                Assert.That(mapPaths, Has.Count.EqualTo(17),
                    "Expected the 17 InRotation primary planet map paths to be checked.");

                foreach (var mapPath in mapPaths)
                {
                    if (!loader.TryLoadMap(mapPath, out var map, out var grids,
                            DeserializationOptions.Default with { InitializeMaps = true }) ||
                        map == null ||
                        grids == null)
                    {
                        errors.Add($"{mapPath}: failed to load map.");
                        continue;
                    }

                    loadedMaps.Add(map.Value.Owner);
                    var gridIds = grids.Select(grid => grid.Owner).ToHashSet();
                    var humanStructures = new List<LoadedHumanStructure>();
                    var relayMarkers = new List<LoadedGroundRelayMarker>();

                    var entityQuery = entMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
                    while (entityQuery.MoveNext(out var uid, out var meta, out var xform))
                    {
                        if (xform.GridUid is not { } gridUid ||
                            !gridIds.Contains(gridUid) ||
                            meta.EntityPrototype is not { } prototype)
                        {
                            continue;
                        }

                        if (prototype.ID == "CMUYautjaGroundRelayDestination" &&
                            entMan.TryGetComponent<YautjaRelayDestinationComponent>(uid, out var destination) &&
                            destination.Kind == YautjaRelayDestinationKind.Ground)
                        {
                            relayMarkers.Add(new LoadedGroundRelayMarker(
                                uid,
                                gridUid,
                                transform.GetWorldPosition(xform),
                                $"{destination.Id} ({destination.DisplayName})"));
                            continue;
                        }

                        if (!IsHumanInfrastructure(entMan, uid, meta, xform))
                            continue;

                        humanStructures.Add(new LoadedHumanStructure(
                            uid,
                            gridUid,
                            transform.GetWorldPosition(xform),
                            prototype.ID));
                    }

                    if (relayMarkers.Count == 0)
                    {
                        errors.Add($"{mapPath}: no CMUYautjaGroundRelayDestination markers were loaded.");
                        continue;
                    }

                    if (humanStructures.Count == 0)
                    {
                        errors.Add($"{mapPath}: no classified human infrastructure was found.");
                        continue;
                    }

                    foreach (var marker in relayMarkers)
                    {
                        var markerXform = entMan.GetComponent<TransformComponent>(marker.Uid);
                        if (markerXform.GridUid is not { } markerGrid ||
                            !entMan.TryGetComponent<MapGridComponent>(markerGrid, out var gridComp) ||
                            !mapSystem.TryGetTileRef(markerGrid, gridComp, markerXform.Coordinates, out var tileRef))
                        {
                            errors.Add($"{mapPath}: relay {marker.Label} at {marker.Position} is not on a valid grid tile.");
                            continue;
                        }

                        if (tileRef.Tile.IsEmpty ||
                            turf.IsTileBlocked(tileRef, CollisionGroup.MobMask))
                        {
                            errors.Add($"{mapPath}: relay {marker.Label} at {marker.Position} is not on an accessible open cell.");
                        }

                        var nearest = humanStructures
                            .Where(structure => structure.GridUid == marker.GridUid)
                            .Select(structure => new
                            {
                                Structure = structure,
                                Distance = Vector2.Distance(marker.Position, structure.Position),
                            })
                            .OrderBy(candidate => candidate.Distance)
                            .FirstOrDefault();

                        if (nearest == null)
                        {
                            errors.Add($"{mapPath}: relay {marker.Label} at {marker.Position} has no classified human infrastructure on its grid.");
                            continue;
                        }

                        if (nearest.Distance < 8f)
                        {
                            errors.Add(
                                $"{mapPath}: relay {marker.Label} at {marker.Position} is {nearest.Distance:0.##} tiles from " +
                                $"{nearest.Structure.Prototype} at {nearest.Structure.Position}; expected at least 8.");
                        }
                    }
                }
            }
            finally
            {
                foreach (var loadedMap in loadedMaps)
                {
                    if (!entMan.Deleted(loadedMap))
                        entMan.DeleteEntity(loadedMap);
                }
            }
        });

        Assert.That(errors, Is.Empty,
            "In-rotation ground relay markers must be open and at least 8 tiles from human infrastructure:\n" +
            string.Join('\n', errors));

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingGroundMapsLoadWithSourceLandmarkMarkers()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var loader = entMan.System<MapLoaderSystem>();
            var resources = server.ResolveDependency<IResourceManager>();

            Assert.That(LoadMap(loader, JungleMoonPath), Is.True);
            Assert.That(LoadMap(loader, DesertMoonPath), Is.True);
            Assert.That(LoadMap(loader, DesertMoonCavesPath), Is.True);

            var jungle = CountMapPrototypes(resources, JungleMoonPath);
            var desert = CountMapPrototypes(resources, DesertMoonPath);
            var caves = CountMapPrototypes(resources, DesertMoonCavesPath);

            Assert.Multiple(() =>
            {
                Assert.That(jungle.GetValueOrDefault("CMUYautjaHuntDestinationJungleMoon"), Is.GreaterThanOrEqualTo(1));
                Assert.That(jungle.GetValueOrDefault("CMUYautjaYoungbloodDestinationJungleMoon"), Is.GreaterThanOrEqualTo(1));
                Assert.That(jungle.GetValueOrDefault("CMUYautjaHuntPreySpawn"), Is.GreaterThanOrEqualTo(1));
                Assert.That(CountMapPrototypeEntities(resources, JungleMoonPath, "CMUYautjaHuntingGroundPreserveShutter"), Is.GreaterThanOrEqualTo(4));
                Assert.That(CountMapPrototypeEntities(resources, JungleMoonPath, "CMUYautjaHuntingGroundEscapeConsole"), Is.GreaterThanOrEqualTo(1));
                Assert.That(ContainsLine(resources, JungleMoonPath, "destinationId: jungle_moon"), Is.True);

                Assert.That(desert.GetValueOrDefault("CMUYautjaHuntPreySpawn"), Is.GreaterThanOrEqualTo(1));
                Assert.That(CountMapPrototypeEntities(resources, DesertMoonPath, "CMUYautjaHuntingGroundPreserveShutter"), Is.EqualTo(3));
                Assert.That(CountMapPrototypeEntities(resources, DesertMoonPath, "CMUYautjaHuntingGroundPreserveEdge"), Is.EqualTo(3));
                Assert.That(CountMapPrototypeEntities(resources, DesertMoonPath, "CMUYautjaHuntingGroundEscapeConsole"), Is.GreaterThanOrEqualTo(1));
                Assert.That(ContainsLine(resources, DesertMoonPath, "destinationId: desert_moon"), Is.True);

                Assert.That(caves.GetValueOrDefault("CMUYautjaHuntDestinationDesertMoon"), Is.GreaterThanOrEqualTo(1));
                Assert.That(caves.GetValueOrDefault("CMUYautjaHuntPreySpawn"), Is.GreaterThanOrEqualTo(1));
                Assert.That(caves.GetValueOrDefault("CMUYautjaYoungbloodDestinationDesertMoon"), Is.GreaterThanOrEqualTo(1));
                Assert.That(CountMapPrototypeEntities(resources, DesertMoonCavesPath, "CMUYautjaHuntingGroundPreserveShutter"), Is.EqualTo(4));
                Assert.That(CountMapPrototypeEntities(resources, DesertMoonCavesPath, "CMUYautjaHuntingGroundPreserveEdge"), Is.EqualTo(14));
                Assert.That(CountMapPrototypeEntities(resources, DesertMoonCavesPath, "CMUYautjaHuntingGroundEscapeConsole"), Is.GreaterThanOrEqualTo(1));
                Assert.That(ContainsLine(resources, DesertMoonCavesPath, "destinationId: desert_moon"), Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingGroundMapsKeepSourceGateGeometryAndYoungbloodDestinations()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var resources = server.ResolveDependency<IResourceManager>();

            Assert.Multiple(() =>
            {
                AssertGateLine(resources, JungleMoonPath, ["19.5,7.5", "20.5,7.5", "21.5,7.5", "22.5,7.5"], "23.5,7.5");
                AssertGateLine(resources, DesertMoonPath, ["23.5,90.5", "23.5,91.5", "23.5,92.5"], "24.5,93.5");
                AssertGateLine(resources, DesertMoonCavesPath, ["11.5,4.5", "12.5,4.5", "13.5,4.5", "14.5,4.5"], "10.5,7.5");
                Assert.That(CountMapPrototypeEntities(resources, JungleMoonPath, "CMUYautjaHuntingGroundPreserveEdge"), Is.EqualTo(8));
                Assert.That(CountMapPrototypeEntities(resources, DesertMoonPath, "CMUYautjaHuntingGroundPreserveEdge"), Is.EqualTo(3));
                Assert.That(CountMapPrototypeEntities(resources, DesertMoonCavesPath, "CMUYautjaHuntingGroundPreserveEdge"), Is.EqualTo(14));

                Assert.That(CountMapPrototypes(resources, JungleMoonPath).GetValueOrDefault("CMUYautjaYoungbloodSpawn"), Is.GreaterThanOrEqualTo(1));
                Assert.That(CountMapPrototypes(resources, DesertMoonCavesPath).GetValueOrDefault("CMUYautjaHuntDestinationDesertMoon"), Is.GreaterThanOrEqualTo(1));
                Assert.That(CountMapPrototypes(resources, DesertMoonCavesPath).GetValueOrDefault("CMUYautjaYoungbloodDestinationDesertMoon"), Is.GreaterThanOrEqualTo(1));
                Assert.That(CountMapPrototypes(resources, DesertMoonCavesPath).GetValueOrDefault("CMUYautjaYoungbloodSpawn"), Is.GreaterThanOrEqualTo(1));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ConfiguredDesertMoonUsesSourceSurfaceSpawnAndEscapeGate()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var server = pair.Server;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = entMan.ComponentFactory;
            var loader = entMan.System<MapLoaderSystem>();
            var mapSystem = entMan.System<SharedMapSystem>();
            var interaction = entMan.System<SharedInteractionSystem>();
            var turf = entMan.System<TurfSystem>();

            var console = prototypes.Index<EntityPrototype>(
                "CMUHunterShipPlacedCMUHunterShipFlightConsoleOverwatchSouthOffset0x13");
            Assert.That(console.TryGetComponent<YautjaHuntConsoleComponent>(out var consoleComp, factory), Is.True);
            var desert = consoleComp!.AvailableDestinations.Single(destination => destination.Id == "desert_moon");

            Assert.That(loader.TryLoadMap(
                new ResPath(desert.MapPath),
                out var map,
                out var grids,
                DeserializationOptions.Default with { InitializeMaps = true }), Is.True);
            Assert.That(map, Is.Not.Null);
            Assert.That(grids, Is.Not.Null);

            var gridIds = grids!.Select(grid => grid.Owner).ToHashSet();
            var landmarks = new Dictionary<string, List<LoadedHuntingGroundLandmark>>();
            var query = entMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var metadata, out var xform))
            {
                if (xform.GridUid is not { } gridUid ||
                    !gridIds.Contains(gridUid) ||
                    metadata.EntityPrototype is not { } prototype ||
                    prototype.ID is not (
                        "CMUYautjaHuntDestinationDesertMoon" or
                        "CMUYautjaYoungbloodDestinationDesertMoon" or
                        "CMUYautjaYoungbloodSpawn" or
                        "CMUYautjaHuntingGroundPreserveShutter" or
                        "CMUYautjaHuntingGroundPreserveEdge" or
                        "CMUYautjaHuntingGroundEscapeConsole"))
                {
                    continue;
                }

                if (!landmarks.TryGetValue(prototype.ID, out var entries))
                {
                    entries = new List<LoadedHuntingGroundLandmark>();
                    landmarks.Add(prototype.ID, entries);
                }

                entries.Add(new LoadedHuntingGroundLandmark(uid, xform.Coordinates.Position, xform.LocalRotation));
            }

            AssertLandmarks(
                landmarks,
                "CMUYautjaHuntDestinationDesertMoon",
                [new Vector2(85.5f, 29.5f)],
                Angle.Zero);
            AssertLandmarks(
                landmarks,
                "CMUYautjaYoungbloodDestinationDesertMoon",
                [new Vector2(76.5f, 39.5f)],
                Angle.Zero);
            AssertLandmarks(
                landmarks,
                "CMUYautjaYoungbloodSpawn",
                [new Vector2(76.5f, 39.5f)],
                Angle.Zero);

            foreach (var prototypeId in new[]
                     {
                         "CMUYautjaHuntDestinationDesertMoon",
                         "CMUYautjaYoungbloodDestinationDesertMoon",
                         "CMUYautjaYoungbloodSpawn",
                     })
            {
                foreach (var landmark in landmarks[prototypeId])
                {
                    Assert.That(
                        CanReachOpenTileAtLeast(entMan, mapSystem, turf, landmark.Uid, 8),
                        Is.True,
                        $"{prototypeId} at {landmark.Position} must have a walkable exit from its source room");
                }
            }

            AssertLandmarks(
                landmarks,
                "CMUYautjaHuntingGroundPreserveShutter",
                [new Vector2(11.5f, 4.5f), new Vector2(12.5f, 4.5f), new Vector2(13.5f, 4.5f), new Vector2(14.5f, 4.5f)],
                Angle.Zero);
            AssertLandmarks(
                landmarks,
                "CMUYautjaHuntingGroundPreserveEdge",
                [
                    new Vector2(11.5f, 1.5f), new Vector2(12.5f, 1.5f), new Vector2(13.5f, 1.5f), new Vector2(14.5f, 1.5f),
                    new Vector2(11.5f, 2.5f), new Vector2(12.5f, 2.5f), new Vector2(13.5f, 2.5f), new Vector2(14.5f, 2.5f),
                    new Vector2(4.5f, 92.5f), new Vector2(5.5f, 92.5f), new Vector2(6.5f, 92.5f),
                    new Vector2(4.5f, 93.5f), new Vector2(5.5f, 93.5f), new Vector2(6.5f, 93.5f),
                ],
                Angle.Zero);
            AssertLandmarks(
                landmarks,
                "CMUYautjaHuntingGroundEscapeConsole",
                [new Vector2(10.5f, 7.5f)],
                Angle.Zero);

            var escapeConsole = landmarks["CMUYautjaHuntingGroundEscapeConsole"].Single();
            var consoleTransform = entMan.GetComponent<TransformComponent>(escapeConsole.Uid);
            Assert.That(consoleTransform.GridUid, Is.Not.Null);

            var hunter = entMan.SpawnEntity(
                "CMMobHuman",
                new EntityCoordinates(consoleTransform.GridUid!.Value, new Vector2(11.5f, 7.5f)));
            try
            {
                Assert.That(
                    interaction.InRangeUnobstructed(hunter, escapeConsole.Uid),
                    Is.True,
                    "The source-adjacent open tile must be able to interact with the desert hunting-ground escape console");
            }
            finally
            {
                entMan.DeleteEntity(hunter);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DesertMoonSurfaceLandmarksUseAccessibleTilesAndSourceRotation()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var server = pair.Server;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var loader = entMan.System<MapLoaderSystem>();
            var mapSystem = entMan.System<SharedMapSystem>();
            var transform = entMan.System<SharedTransformSystem>();
            var turf = entMan.System<TurfSystem>();
            var blockedLandmarks = new List<string>();
            Assert.That(loader.TryLoadMap(
                    DesertMoonCavesPath,
                    out var map,
                    out var grids,
                    DeserializationOptions.Default with { InitializeMaps = true }), Is.True);
            Assert.That(map, Is.Not.Null);
            Assert.That(grids, Is.Not.Null);

            var gridIds = grids!.Select(grid => grid.Owner).ToHashSet();
            var landmarks = new Dictionary<string, List<LoadedHuntingGroundLandmark>>();
            var query = entMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var metadata, out var xform))
            {
                if (xform.GridUid is not { } gridUid || !gridIds.Contains(gridUid) || metadata.EntityPrototype is not { } prototype)
                    continue;

                if (prototype.ID is not (
                        "CMUYautjaHuntDestinationDesertMoon" or
                        "CMUYautjaYoungbloodDestinationDesertMoon" or
                        "CMUYautjaHuntPreySpawn" or
                        "CMUYautjaYoungbloodSpawn" or
                        "CMUYautjaHuntingGroundPreserveShutter" or
                        "CMUYautjaHuntingGroundPreserveEdge" or
                        "CMUYautjaHuntingGroundEscapeConsole"))
                {
                    continue;
                }

                if (!landmarks.TryGetValue(prototype.ID, out var entries))
                {
                    entries = new List<LoadedHuntingGroundLandmark>();
                    landmarks.Add(prototype.ID, entries);
                }

                entries.Add(new LoadedHuntingGroundLandmark(
                    uid,
                    transform.GetWorldPosition(xform),
                    xform.LocalRotation));
            }

            foreach (var prototypeId in new[]
                         {
                             "CMUYautjaHuntDestinationDesertMoon",
                             "CMUYautjaYoungbloodDestinationDesertMoon",
                             "CMUYautjaHuntPreySpawn",
                             "CMUYautjaYoungbloodSpawn",
                         })
            {
                Assert.That(landmarks.GetValueOrDefault(prototypeId), Is.Not.Empty, prototypeId);
            }

            foreach (var landmark in landmarks["CMUYautjaHuntDestinationDesertMoon"]
                             .Concat(landmarks["CMUYautjaYoungbloodDestinationDesertMoon"])
                             .Concat(landmarks["CMUYautjaHuntPreySpawn"])
                             .Concat(landmarks["CMUYautjaYoungbloodSpawn"]))
            {
                var xform = entMan.GetComponent<TransformComponent>(landmark.Uid);
                    if (!xform.GridUid.HasValue ||
                        !entMan.TryGetComponent(xform.GridUid.Value, out MapGridComponent? grid) ||
                        !mapSystem.TryGetTileRef(xform.GridUid.Value, grid!, xform.Coordinates, out var tileRef) ||
                        tileRef.Tile.IsEmpty ||
                        turf.IsTileBlocked(tileRef, CollisionGroup.MobMask))
                    {
                        var alternatives = new List<Vector2>();
                        if (xform.GridUid.HasValue && entMan.TryGetComponent(xform.GridUid.Value, out grid))
                        {
                            for (var radius = 1; radius <= 4 && alternatives.Count == 0; radius++)
                            {
                                for (var dx = -radius; dx <= radius; dx++)
                                {
                                    for (var dy = -radius; dy <= radius; dy++)
                                    {
                                        if (Math.Abs(dx) != radius && Math.Abs(dy) != radius)
                                            continue;

                                        var candidateCoordinates = xform.Coordinates.Offset(new Vector2(dx, dy));
                                        if (!mapSystem.TryGetTileRef(xform.GridUid.Value, grid, candidateCoordinates, out var candidateTile) ||
                                            candidateTile.Tile.IsEmpty ||
                                            turf.IsTileBlocked(candidateTile, CollisionGroup.MobMask))
                                        {
                                            continue;
                                        }

                                        alternatives.Add(candidateCoordinates.Position);
                                    }
                                }
                            }
                        }

                        blockedLandmarks.Add(
                            $"{landmark.Uid} at {landmark.Position}; nearest accessible: {string.Join(", ", alternatives)}");
                    }
                }

                Assert.That(blockedLandmarks, Is.Empty, "Teleport and role spawn landmarks must be on accessible tiles.");

            AssertLandmarks(
                    landmarks,
                    "CMUYautjaHuntingGroundPreserveShutter",
                    [new Vector2(11.5f, 4.5f), new Vector2(12.5f, 4.5f), new Vector2(13.5f, 4.5f), new Vector2(14.5f, 4.5f)],
                    Angle.Zero);
            AssertLandmarks(
                    landmarks,
                    "CMUYautjaHuntingGroundPreserveEdge",
                    [
                        new Vector2(11.5f, 1.5f), new Vector2(12.5f, 1.5f), new Vector2(13.5f, 1.5f), new Vector2(14.5f, 1.5f),
                        new Vector2(11.5f, 2.5f), new Vector2(12.5f, 2.5f), new Vector2(13.5f, 2.5f), new Vector2(14.5f, 2.5f),
                        new Vector2(4.5f, 92.5f), new Vector2(5.5f, 92.5f), new Vector2(6.5f, 92.5f),
                        new Vector2(4.5f, 93.5f), new Vector2(5.5f, 93.5f), new Vector2(6.5f, 93.5f),
                    ],
                    Angle.Zero);
            AssertLandmarks(
                    landmarks,
                    "CMUYautjaHuntingGroundEscapeConsole",
                    [new Vector2(10.5f, 7.5f)],
                    Angle.Zero);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DesertMoonCaveLayerKeepsSourceEastGateGeometry()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var loader = entMan.System<MapLoaderSystem>();
            Assert.That(loader.TryLoadMap(
                DesertMoonPath,
                out var map,
                out var grids,
                DeserializationOptions.Default with { InitializeMaps = true }), Is.True);
            Assert.That(map, Is.Not.Null);
            Assert.That(grids, Is.Not.Null);

            var gridIds = grids!.Select(grid => grid.Owner).ToHashSet();
            var landmarks = new Dictionary<string, List<LoadedHuntingGroundLandmark>>();
            var query = entMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var metadata, out var xform))
            {
                if (xform.GridUid is not { } gridUid || !gridIds.Contains(gridUid) || metadata.EntityPrototype is not { } prototype)
                    continue;

                if (prototype.ID is not (
                    "CMUYautjaHuntingGroundPreserveShutter" or
                    "CMUYautjaHuntingGroundPreserveEdge" or
                    "CMUYautjaHuntingGroundEscapeConsole"))
                {
                    continue;
                }

                if (!landmarks.TryGetValue(prototype.ID, out var entries))
                {
                    entries = new List<LoadedHuntingGroundLandmark>();
                    landmarks.Add(prototype.ID, entries);
                }

                entries.Add(new LoadedHuntingGroundLandmark(uid, xform.Coordinates.Position, xform.LocalRotation));
            }

            AssertLandmarks(
                landmarks,
                "CMUYautjaHuntingGroundPreserveShutter",
                [new Vector2(23.5f, 90.5f), new Vector2(23.5f, 91.5f), new Vector2(23.5f, 92.5f)],
                new Angle(MathF.PI / 2));
            AssertLandmarks(
                landmarks,
                "CMUYautjaHuntingGroundPreserveEdge",
                [new Vector2(12.5f, 93.5f), new Vector2(12.5f, 94.5f), new Vector2(12.5f, 95.5f)],
                Angle.Zero);
            AssertLandmarks(
                landmarks,
                "CMUYautjaHuntingGroundEscapeConsole",
                [new Vector2(24.5f, 93.5f)],
                Angle.Zero);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DesertMoonShipTeleporterLandsOnOpenDestination()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var origin = await pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var loader = entMan.System<MapLoaderSystem>();
            var transform = entMan.System<SharedTransformSystem>();
            var mapSystem = entMan.System<SharedMapSystem>();
            var turf = entMan.System<TurfSystem>();

            Assert.That(loader.TryLoadMap(
                DesertMoonCavesPath,
                out var map,
                out var grids,
                DeserializationOptions.Default with { InitializeMaps = true }), Is.True);
            Assert.That(map, Is.Not.Null);
            Assert.That(grids, Is.Not.Null);

            var destination = entMan.EntityQuery<YautjaHuntTeleportDestinationComponent, TransformComponent>()
                .Where(destination =>
                    destination.Item1.Kind == YautjaHuntTeleporterKind.Ship &&
                    destination.Item1.Id == "desert_moon")
                .Select(destination => destination.Item2.Owner)
                .Single();
            var teleporter = entMan.SpawnEntity(null, origin.GridCoords);
            var hunter = entMan.SpawnEntity("CMMobHuman", origin.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                var teleporterComp = entMan.EnsureComponent<YautjaHuntTeleporterComponent>(teleporter);
                teleporterComp.Kind = YautjaHuntTeleporterKind.Ship;
                teleporterComp.DestinationId = "desert_moon";

                var ev = new StepTriggeredOnEvent(teleporter, hunter);
                entMan.EventBus.RaiseLocalEvent(teleporter, ref ev);

                Assert.That(entMan.TryGetComponent(teleporter, out DialogComponent? dialog), Is.True);
                Assert.That(dialog!.ConfirmEvent, Is.TypeOf<YautjaYoungbloodDeployConfirmedEvent>());
                entMan.EventBus.RaiseLocalEvent(teleporter, dialog.ConfirmEvent!, true);

                var actual = transform.GetMapCoordinates(hunter);
                var expected = transform.GetMapCoordinates(destination);
                Assert.That(actual.MapId, Is.EqualTo(expected.MapId));
                Assert.That(actual.Position, Is.EqualTo(expected.Position));

                var destinationXform = entMan.GetComponent<TransformComponent>(destination);
                Assert.That(destinationXform.GridUid, Is.Not.Null);
                Assert.That(entMan.TryGetComponent(destinationXform.GridUid!.Value, out MapGridComponent? grid), Is.True);
                Assert.That(mapSystem.TryGetTileRef(
                    destinationXform.GridUid.Value,
                    grid!,
                    destinationXform.Coordinates,
                    out var tileRef), Is.True);
                Assert.That(tileRef.Tile.IsEmpty, Is.False);
                Assert.That(turf.IsTileBlocked(tileRef, CollisionGroup.MobMask), Is.False);
            }
            finally
            {
                if (!entMan.Deleted(teleporter))
                    entMan.DeleteEntity(teleporter);
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipPlacedFlightConsolePrototypeHasConfiguredDestinations()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            Assert.That(prototypes.TryIndex<EntityPrototype>(
                "CMUHunterShipPlacedCMUHunterShipFlightConsoleOverwatchSouthOffset0x13",
                out var console), Is.True);
            Assert.That(console!.TryGetComponent<YautjaHuntConsoleComponent>(out var component, factory), Is.True);
            Assert.That(component!.Kind, Is.EqualTo(YautjaHuntConsoleKind.HuntingGroundSelection));
            Assert.That(component.AvailableDestinations.Select(destination => destination.Id),
                Does.Contain("jungle_moon"));
            Assert.That(component.AvailableDestinations.Select(destination => destination.Id),
                Does.Contain("desert_moon"));
            Assert.That(component.AvailableDestinations.Single(destination => destination.Id == "desert_moon").MapPath,
                Is.EqualTo("/Maps/_CMU14/HuntingGrounds/desert_moon_caves.yml"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HuntingGroundSelectionConsoleHasConfiguredLoadableDestinations()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;
            var resources = server.ResolveDependency<IResourceManager>();

            var selectionConsoles = prototypes.EnumeratePrototypes<EntityPrototype>()
                .Where(proto => proto.TryGetComponent<YautjaHuntConsoleComponent>(out var component, factory) &&
                                component.Kind == YautjaHuntConsoleKind.HuntingGroundSelection)
                .ToArray();

            Assert.That(selectionConsoles, Is.Not.Empty);

            foreach (var console in selectionConsoles)
            {
                Assert.That(console.TryGetComponent<YautjaHuntConsoleComponent>(out var component, factory), Is.True);
                var destinations = component!.AvailableDestinations;

                Assert.Multiple(() =>
                {
                    Assert.That(destinations, Has.Count.GreaterThanOrEqualTo(2), console.ID);
                    Assert.That(destinations.Select(destination => destination.Id), Does.Contain("jungle_moon"), console.ID);
                    Assert.That(destinations.Select(destination => destination.Id), Does.Contain("desert_moon"), console.ID);

                    foreach (var destination in destinations)
                    {
                        Assert.That(destination.DisplayName, Is.Not.Empty, $"{console.ID} destination display name");
                        Assert.That(destination.MapPath, Is.Not.Empty, $"{console.ID} destination map path");
                        Assert.That(resources.ContentFileExists(new ResPath(destination.MapPath)), Is.True, $"{console.ID} destination map exists");
                    }
                });
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipHuntConsolesAreClickableOnTheirVisiblePanel()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        foreach (var prototype in HunterShipHuntConsolePrototypes)
        {
            EntityUid serverConsole = default;
            await server.WaitPost(() =>
            {
                serverConsole = server.EntMan.SpawnEntity(prototype, map.GridCoords);
            });

            await pair.RunTicksSync(5);

            await client.WaitPost(() =>
            {
                var entMan = client.EntMan;
                var console = entMan.GetEntity(server.EntMan.GetNetEntity(serverConsole));
                var sprite = entMan.GetComponent<SpriteComponent>(console);
                var clickable = entMan.System<ClickableSystem>();
                var transform = entMan.System<SharedTransformSystem>();
                var eye = client.ResolveDependency<IEyeManager>().CurrentEye;
                eye.Rotation = 0;

                var position = transform.GetWorldPosition(console);
                var visiblePanel = position + new Vector2(0, 0.6f);

                Assert.That(
                    clickable.CheckClick((console, null, sprite, null), visiblePanel, eye, false, out _, out _, out _),
                    Is.True,
                    $"{prototype} should accept clicks on the visible shifted console panel.");
            });

            await server.WaitPost(() =>
            {
                server.EntMan.DeleteEntity(serverConsole);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipPlacedHuntConsolesAreTopClickedEntitiesOnRealShip()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Destructive = true });
        var server = pair.Server;
        var client = pair.Client;

        var serverConsoles = new Dictionary<string, EntityUid>();
        EntityUid hunter = default;
        EntityUid loadedMap = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loader = entMan.System<MapLoaderSystem>();

                Assert.That(loader.TryLoadMap(HunterShipPath, out var map, out var grids,
                    DeserializationOptions.Default with { InitializeMaps = true }), Is.True);

                loadedMap = map!.Value.Owner;
                var grid = grids!.Single().Owner;

                var remaining = HunterShipHuntConsolePrototypes.ToHashSet();
                var query = entMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
                while (query.MoveNext(out var uid, out var meta, out var xform))
                {
                    if (xform.GridUid != grid ||
                        meta.EntityPrototype?.ID is not { } prototype ||
                        !remaining.Remove(prototype))
                    {
                        continue;
                    }

                    serverConsoles[prototype] = uid;
                }

                Assert.That(serverConsoles.Keys, Is.EquivalentTo(HunterShipHuntConsolePrototypes));

                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                hunter = entMan.SpawnEntity("CMMobHuman", new EntityCoordinates(grid, new Vector2(45.5f, 19.5f)));
                entMan.EnsureComponent<YautjaComponent>(hunter);
                server.PlayerMan.SetAttachedEntity(session, hunter);
            });

            await pair.ReallyBeIdle(20);

            await client.WaitAssertion(() =>
            {
                var entMan = client.EntMan;
                var transform = entMan.System<SharedTransformSystem>();
                var spriteTree = entMan.System<SpriteTreeSystem>();
                var clickables = entMan.System<ClickableSystem>();
                var transparency = entMan.System<RMCClientInteractionSystem>();
                var clickQuery = entMan.GetEntityQuery<ClickableComponent>();
                var eye = client.ResolveDependency<IEyeManager>().CurrentEye;
                var player = client.ResolveDependency<Robust.Client.Player.IPlayerManager>().LocalEntity;
                eye.Rotation = 0;

                foreach (var prototype in HunterShipHuntConsolePrototypes)
                {
                    var console = entMan.GetEntity(server.EntMan.GetNetEntity(serverConsoles[prototype]));
                    var consoleMapCoordinates = transform.GetMapCoordinates(console);
                    var visiblePanelClick = new MapCoordinates(
                        consoleMapCoordinates.Position + new Vector2(0, 0.6f),
                        consoleMapCoordinates.MapId);

                    var entities = spriteTree.QueryAabb(
                        visiblePanelClick.MapId,
                        Box2.CenteredAround(visiblePanelClick.Position, new Vector2(1, 1)));

                    var found = new List<(EntityUid Uid, int Depth, uint RenderOrder, float Bottom)>();
                    foreach (var entity in entities)
                    {
                        if (transparency.IsInteractionTransparency(entity.Uid, player, eye))
                            continue;

                        if (clickQuery.TryGetComponent(entity.Uid, out var component) &&
                            clickables.CheckClick(
                                (entity.Uid, component, entity.Component, entity.Transform),
                                visiblePanelClick.Position,
                                eye,
                                excludeFaded: false,
                                out var drawDepth,
                                out var renderOrder,
                                out var bottom))
                        {
                            found.Add((entity.Uid, drawDepth, renderOrder, bottom));
                        }
                    }

                    found.Sort((x, y) =>
                    {
                        var cmp = y.Depth.CompareTo(x.Depth);
                        if (cmp != 0)
                            return cmp;

                        cmp = y.RenderOrder.CompareTo(x.RenderOrder);
                        if (cmp != 0)
                            return cmp;

                        cmp = -y.Bottom.CompareTo(x.Bottom);
                        if (cmp != 0)
                            return cmp;

                        return y.Uid.CompareTo(x.Uid);
                    });

                    var clicked = found.Select(entry => entry.Uid).Take(8).ToArray();

                    Assert.That(clicked, Does.Contain(console), $"{prototype} should be hittable from the real Hunter Ship map.");
                    Assert.That(clicked.FirstOrDefault(), Is.EqualTo(console), $"{prototype} should be the top entity under its visible panel.");
                }
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                if (hunter != default && !entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);

                if (loadedMap != default && !entMan.Deleted(loadedMap))
                    entMan.DeleteEntity(loadedMap);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipPlacedDoorButtonsArePoweredOnRealShip()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Destructive = true });
        var server = pair.Server;
        var buttons = new Dictionary<string, EntityUid>();
        var loadedMaps = new List<EntityUid>();

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loader = entMan.System<MapLoaderSystem>();

                foreach (var path in HunterShipZLevelPaths)
                {
                    Assert.That(loader.TryLoadMap(path, out var map, out var grids,
                        DeserializationOptions.Default with { InitializeMaps = true }), Is.True, path.ToString());

                    loadedMaps.Add(map!.Value.Owner);
                    var grid = grids!.Single().Owner;
                    var remaining = HunterShipDoorButtonPrototypes.ToHashSet();
                    var query = entMan.EntityQueryEnumerator<RMCDoorButtonComponent, TransformComponent, MetaDataComponent>();
                    while (query.MoveNext(out var uid, out _, out var xform, out var meta))
                    {
                        if (xform.GridUid == grid && meta.EntityPrototype?.ID is { } id && remaining.Remove(id))
                            buttons[id] = uid;
                    }
                }

                Assert.That(buttons.Keys, Is.EquivalentTo(HunterShipDoorButtonPrototypes));
            });

            await pair.ReallyBeIdle(20);

            await server.WaitAssertion(() =>
            {
                foreach (var (id, button) in buttons)
                {
                    var receiver = server.EntMan.GetComponent<ApcPowerReceiverComponent>(button);
                    Assert.That(receiver.NeedsPower, Is.False, id);
                    Assert.That(receiver.Powered, Is.True,
                        $"Hunter Ship button {id} must not show the generic unpowered message.");
                }
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                foreach (var loadedMap in loadedMaps)
                {
                    if (!server.EntMan.Deleted(loadedMap))
                        server.EntMan.DeleteEntity(loadedMap);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterShipPlacedHuntConsolesOpenThroughUserInteractionOnRealShip()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Destructive = true });
        var server = pair.Server;
        var client = pair.Client;

        var serverConsoles = new Dictionary<string, EntityUid>();
        EntityUid hunter = default;
        EntityUid loadedMap = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var loader = entMan.System<MapLoaderSystem>();

                Assert.That(loader.TryLoadMap(HunterShipPath, out var map, out var grids,
                    DeserializationOptions.Default with { InitializeMaps = true }), Is.True);

                loadedMap = map!.Value.Owner;
                var grid = grids!.Single().Owner;
                var remaining = HunterShipHuntConsolePrototypes.ToHashSet();
                var query = entMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
                while (query.MoveNext(out var uid, out var meta, out var xform))
                {
                    if (xform.GridUid != grid ||
                        meta.EntityPrototype?.ID is not { } prototype ||
                        !remaining.Remove(prototype))
                    {
                        continue;
                    }

                    serverConsoles[prototype] = uid;
                }

                Assert.That(serverConsoles.Keys, Is.EquivalentTo(HunterShipHuntConsolePrototypes));

                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                hunter = entMan.SpawnEntity("CMMobHuman", new EntityCoordinates(grid, new Vector2(45.5f, 19.5f)));
                entMan.EnsureComponent<YautjaComponent>(hunter);
                server.PlayerMan.SetAttachedEntity(session, hunter);
            });

            await pair.ReallyBeIdle(20);

            foreach (var prototype in HunterShipHuntConsolePrototypes)
            {
                await server.WaitPost(() =>
                {
                    var entMan = server.EntMan;
                    var interaction = entMan.System<SharedInteractionSystem>();
                    var transform = entMan.System<SharedTransformSystem>();
                    var ui = entMan.System<SharedUserInterfaceSystem>();

                    var console = serverConsoles[prototype];
                    var consoleCoords = transform.GetMoverCoordinates(console);
                    transform.SetCoordinates(hunter, consoleCoords.Offset(new Vector2(0, -1)));

                    Assert.That(ui.HasUi(console, DialogUiKey.Key), Is.True, prototype);

                    interaction.UserInteraction(hunter, consoleCoords, console);

                    Assert.That(ui.IsUiOpen(console, DialogUiKey.Key, hunter), Is.True, prototype);
                    Assert.That(entMan.TryGetComponent(console, out DialogComponent dialog), Is.True, prototype);
                    Assert.That(dialog!.Options, Is.Not.Empty, prototype);
                });

                await pair.ReallyBeIdle(10);

                await client.WaitAssertion(() =>
                {
                    var entMan = client.EntMan;
                    var console = entMan.GetEntity(server.EntMan.GetNetEntity(serverConsoles[prototype]));

                    Assert.That(entMan.TryGetComponent(console, out UserInterfaceComponent clientUi), Is.True, prototype);
                    Assert.That(clientUi!.ClientOpenInterfaces.TryGetValue(DialogUiKey.Key, out var bui), Is.True, prototype);
                    Assert.That(bui, Is.TypeOf<DialogBui>(), prototype);
                });

                await server.WaitPost(() =>
                {
                    var entMan = server.EntMan;
                    var ui = entMan.System<SharedUserInterfaceSystem>();
                    var console = serverConsoles[prototype];
                    ui.CloseUi(console, DialogUiKey.Key, hunter);
                    entMan.RemoveComponent<DialogComponent>(console);
                });

                await pair.ReallyBeIdle(10);
            }
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);

                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);

                if (loadedMap != default && !entMan.Deleted(loadedMap))
                    entMan.DeleteEntity(loadedMap);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YoungbloodRackPrototypeAndWrappersRemainSeparateFromAdultRacks()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            Assert.That(prototypes.HasIndex<EntityPrototype>("CMUYautjaYoungbloodLoadoutVendor"), Is.True);

            var youngWrapper = prototypes.Index<EntityPrototype>(
                "CMUHunterShipPlacedCMUYautjaYoungbloodLoadoutVendorPredVendorLeftSouthOffset0x16");
            var adultWrapper = prototypes.Index<EntityPrototype>(
                "CMUHunterShipPlacedCMUYautjaLoadoutVendorPredVendorCentreSouthVariant02Offset0x16");

            Assert.That(youngWrapper.Parents, Does.Contain("CMUYautjaYoungbloodLoadoutVendor"));
            Assert.That(adultWrapper.Parents, Does.Contain("CMUYautjaLoadoutVendor"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaGearRackSpritesExposeVendingLayersForClientMerge()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.EntMan.ComponentFactory;
            var entMan = client.EntMan;
            var sprites = entMan.System<SpriteSystem>();

            var rackPrototypes = prototypes.EnumeratePrototypes<EntityPrototype>()
                .Where(proto => !proto.Abstract &&
                                proto.TryGetComponent<YautjaGearRackComponent>(out _, factory) &&
                                proto.TryGetComponent<SpriteComponent>(out _, factory))
                .ToArray();

            Assert.That(rackPrototypes, Is.Not.Empty);

            foreach (var proto in rackPrototypes)
            {
                var rack = entMan.Spawn(proto.ID);
                try
                {
                    var sprite = entMan.GetComponent<SpriteComponent>(rack);
                    Assert.Multiple(() =>
                    {
                        Assert.That(sprites.LayerMapTryGet((rack, sprite), VendingMachineVisualLayers.Base, out _, false),
                            Is.True, $"{proto.ID} missing Base vending layer");
                        Assert.That(sprites.LayerMapTryGet((rack, sprite), VendingMachineVisualLayers.BaseUnshaded, out _, false),
                            Is.True, $"{proto.ID} missing BaseUnshaded vending layer");
                    });
                }
                finally
                {
                    entMan.DeleteEntity(rack);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    private static bool LoadMap(MapLoaderSystem loader, ResPath path)
    {
        return loader.TryLoadMap(path, out var map, out _) && map != null;
    }

    private static IReadOnlyList<ResPath> GetInRotationPlanetMapPaths(
        IPrototypeManager prototypes,
        IComponentFactory componentFactory)
    {
        var checkedPaths = new HashSet<ResPath>();
        var paths = new List<ResPath>();

        foreach (var planetPrototype in prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (!planetPrototype.TryComp<RMCPlanetMapPrototypeComponent>(out var planet, componentFactory) ||
                !planet!.InRotation)
            {
                continue;
            }

            var mapPath = prototypes.Index<GameMapPrototype>(planet.MapId).MapPath;
            if (checkedPaths.Add(mapPath))
                paths.Add(mapPath);
        }

        paths.Sort((left, right) => string.Compare(left.ToString(), right.ToString(), StringComparison.Ordinal));
        return paths;
    }

    private static bool IsHumanInfrastructure(
        IEntityManager entMan,
        EntityUid uid,
        MetaDataComponent meta,
        TransformComponent transform)
    {
        if (IsHumanSpawn(entMan, uid, meta))
            return true;

        if (!transform.Anchored ||
            meta.EntityPrototype is not { } prototype)
        {
            return false;
        }

        var prototypeText = $"{prototype.ID} {meta.EntityName}";
        return ContainsAny(prototypeText, HumanInfrastructureTerms) &&
               !ContainsAny(prototypeText, NonHumanInfrastructureTerms);
    }

    private static bool IsHumanSpawn(IEntityManager entMan, EntityUid uid, MetaDataComponent meta)
    {
        if (!entMan.TryGetComponent<SpawnPointComponent>(uid, out var spawn))
            return false;

        if (meta.EntityPrototype is { } prototype &&
            ContainsAny($"{prototype.ID} {meta.EntityName}", NonHumanInfrastructureTerms))
        {
            return false;
        }

        return spawn.Job != null ||
               spawn.SpawnType is SpawnPointType.Job or
                   SpawnPointType.LateJoin or
                   SpawnPointType.LateJoinGovfor or
                   SpawnPointType.LateJoinOpfor;
    }

    private static bool ContainsAny(string value, IReadOnlyList<string> terms)
    {
        foreach (var term in terms)
        {
            if (value.Contains(term, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static readonly string[] HumanInfrastructureTerms =
    [
        "airlock",
        "apc",
        "barricade",
        "bed",
        "button",
        "cabinet",
        "cable",
        "chair",
        "computer",
        "console",
        "crate",
        "desk",
        "door",
        "engine",
        "fence",
        "furniture",
        "generator",
        "girder",
        "lamp",
        "locker",
        "machine",
        "machinery",
        "pipe",
        "rack",
        "railing",
        "sandbag",
        "shelf",
        "shutter",
        "sign",
        "table",
        "terminal",
        "vendor",
        "vending",
        "wall",
        "window",
    ];

    private static readonly string[] NonHumanInfrastructureTerms =
    [
        "boulder",
        "bush",
        "cave",
        "crystal",
        "flora",
        "flower",
        "foliage",
        "grass",
        "hive",
        "moss",
        "mushroom",
        "plant",
        "resin",
        "rock",
        "root",
        "stalagmite",
        "tree",
        "vegetation",
        "vine",
        "weed",
        "xeno",
    ];

    private readonly record struct LoadedGroundRelayMarker(
        EntityUid Uid,
        EntityUid GridUid,
        Vector2 Position,
        string Label);

    private readonly record struct LoadedHuntingGroundLandmark(
        EntityUid Uid,
        Vector2 Position,
        Angle Rotation);

    private readonly record struct LoadedHumanStructure(
        EntityUid Uid,
        EntityUid GridUid,
        Vector2 Position,
        string Prototype);

    private static Dictionary<string, int> CountMapPrototypes(IResourceManager resources, ResPath mapPath)
    {
        using var file = resources.ContentFileRead(mapPath);
        using var reader = new StreamReader(file);
        var counts = new Dictionary<string, int>();

        while (reader.ReadLine() is { } line)
        {
            line = line.Trim();
            const string prefix = "- proto: ";
            if (!line.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            var proto = line[prefix.Length..];
            counts.TryGetValue(proto, out var existing);
            counts[proto] = existing + 1;
        }

        return counts;
    }

    private static int CountMapPrototypeEntities(IResourceManager resources, ResPath mapPath, string prototypeId)
    {
        using var file = resources.ContentFileRead(mapPath);
        using var reader = new StreamReader(file);
        var currentPrototype = string.Empty;
        var count = 0;

        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith("- proto: ", StringComparison.Ordinal))
            {
                currentPrototype = line[9..];
                continue;
            }

            if (line.StartsWith("- uid: ", StringComparison.Ordinal))
                currentPrototype = string.Empty;
            else if (currentPrototype == prototypeId && line.StartsWith("  - uid: ", StringComparison.Ordinal))
                count++;
        }

        return count;
    }

    private static void AssertGateLine(
        IResourceManager resources,
        ResPath mapPath,
        IReadOnlyCollection<string> shutterPositions,
        string consolePosition)
    {
        foreach (var position in shutterPositions)
        {
            Assert.That(CountMapPrototypeEntitiesAt(resources, mapPath, "CMUYautjaHuntingGroundPreserveShutter", position), Is.EqualTo(1),
                $"Expected one preserve shutter at {position} in {mapPath}");
            Assert.That(CountMapPrototypeEntitiesAt(resources, mapPath, "WallRock", position), Is.Zero,
                $"A preserve shutter must replace the source wall at {position} in {mapPath}");
        }

        Assert.That(CountMapPrototypeEntitiesAt(resources, mapPath, "CMUYautjaHuntingGroundEscapeConsole", consolePosition), Is.EqualTo(1),
            $"Expected one escape console at {consolePosition} in {mapPath}");
        Assert.That(CountMapPrototypeEntitiesAt(resources, mapPath, "WallRock", consolePosition), Is.Zero,
            $"An escape console must replace the source wall at {consolePosition} in {mapPath}");
    }

    private static int CountMapPrototypeEntitiesAt(
        IResourceManager resources,
        ResPath mapPath,
        string prototypeId,
        string position)
    {
        using var file = resources.ContentFileRead(mapPath);
        using var reader = new StreamReader(file);
        var currentPrototype = string.Empty;
        var count = 0;

        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith("- proto: ", StringComparison.Ordinal))
            {
                currentPrototype = line[9..];
                continue;
            }

            if (currentPrototype != prototypeId || !line.TrimStart().StartsWith("pos: ", StringComparison.Ordinal))
                continue;

            if (line.Trim()[5..] == position)
                count++;
        }

        return count;
    }

    private static void AssertLandmarks(
        Dictionary<string, List<LoadedHuntingGroundLandmark>> landmarks,
        string prototypeId,
        IReadOnlyCollection<Vector2> expectedPositions,
        Angle expectedRotation)
    {
        var actual = landmarks.GetValueOrDefault(prototypeId) ?? [];
        Assert.That(actual.Select(landmark => landmark.Position), Is.EquivalentTo(expectedPositions), prototypeId);
        Assert.That(actual, Has.All.Matches<LoadedHuntingGroundLandmark>(landmark =>
            Math.Abs((landmark.Rotation - expectedRotation).Theta) < 0.001f), prototypeId);
    }

    private static bool CanReachOpenTileAtLeast(
        IEntityManager entMan,
        SharedMapSystem mapSystem,
        TurfSystem turf,
        EntityUid landmark,
        int minimumDistance)
    {
        var xform = entMan.GetComponent<TransformComponent>(landmark);
        if (xform.GridUid is not { } gridUid ||
            !entMan.TryGetComponent(gridUid, out MapGridComponent? grid))
        {
            return false;
        }

        var origin = xform.Coordinates;
        var queue = new Queue<(EntityCoordinates Coordinates, int Distance)>();
        var visited = new HashSet<Vector2i>();
        queue.Enqueue((origin, 0));

        while (queue.TryDequeue(out var current))
        {
            var tileIndices = new Vector2i(
                (int) MathF.Floor(current.Coordinates.X),
                (int) MathF.Floor(current.Coordinates.Y));
            if (!visited.Add(tileIndices))
                continue;

            if (!mapSystem.TryGetTileRef(gridUid, grid, current.Coordinates, out var tileRef) ||
                tileRef.Tile.IsEmpty ||
                turf.IsTileBlocked(tileRef, CollisionGroup.MobMask))
            {
                continue;
            }

            if (current.Distance >= minimumDistance)
                return true;

            queue.Enqueue((current.Coordinates.Offset(new Vector2(1, 0)), current.Distance + 1));
            queue.Enqueue((current.Coordinates.Offset(new Vector2(-1, 0)), current.Distance + 1));
            queue.Enqueue((current.Coordinates.Offset(new Vector2(0, 1)), current.Distance + 1));
            queue.Enqueue((current.Coordinates.Offset(new Vector2(0, -1)), current.Distance + 1));
        }

        return false;
    }

    private static bool ContainsLine(IResourceManager resources, ResPath mapPath, string expected)
    {
        using var file = resources.ContentFileRead(mapPath);
        using var reader = new StreamReader(file);

        while (reader.ReadLine() is { } line)
        {
            if (line.Trim() == expected)
                return true;
        }

        return false;
    }

}
