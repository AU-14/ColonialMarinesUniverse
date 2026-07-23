using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Content.Client._RMC14.Interaction;
using Content.Client._RMC14.Dialog;
using Content.Client.Clickable;
using Content.Server.Maps;
using Content.Server.Power.Components;
using Content.Shared._RMC14.Dialog;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Doors;
using Content.Shared._RMC14.Rules;
using Content.Shared.Interaction;
using Content.Shared.VendingMachines;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
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
                Assert.That(ContainsLine(resources, JungleMoonPath, "destinationId: jungle_moon"), Is.True);

                Assert.That(desert.GetValueOrDefault("CMUYautjaHuntPreySpawn"), Is.GreaterThanOrEqualTo(1));
                Assert.That(ContainsLine(resources, DesertMoonPath, "destinationId: desert_moon"), Is.True);

                Assert.That(caves.GetValueOrDefault("CMUYautjaHuntDestinationDesertMoon"), Is.GreaterThanOrEqualTo(1));
                Assert.That(caves.GetValueOrDefault("CMUYautjaHuntPreySpawn"), Is.GreaterThanOrEqualTo(1));
                Assert.That(caves.GetValueOrDefault("CMUYautjaYoungbloodDestinationDesertMoon"), Is.GreaterThanOrEqualTo(1));
                Assert.That(ContainsLine(resources, DesertMoonCavesPath, "destinationId: desert_moon"), Is.True);
            });
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
