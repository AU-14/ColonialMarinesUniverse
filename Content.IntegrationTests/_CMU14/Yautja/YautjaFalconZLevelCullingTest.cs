using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._CMU14.ZLevels.Core;
using Content.Client._CMU14.ZLevels.Culling;
using Content.Server._CMU14.ZLevels.Core;
using Content.Shared._CMU14.Yautja;
using Content.Shared._CMU14.ZLevels;
using Content.Shared._CMU14.ZLevels.Core;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Maps;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaFalconZLevelCullingTest
{
    [Test]
    public async Task EyeMapResolverRejectsNullspaceAndUnrelatedZNetworks()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var viewerMap = await pair.CreateTestMap();
        var relatedEyeMap = await pair.CreateTestMap();
        var unrelatedEyeMap = await pair.CreateTestMap();
        EntityUid network = default;
        EntityUid unrelatedNetwork = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var zLevels = server.EntMan.System<CMUZLevelsSystem>();
                var zNetwork = zLevels.CreateZNetwork();
                network = zNetwork.Owner;

                Assert.That(zLevels.TryAddMapsIntoZNetwork(zNetwork, new Dictionary<EntityUid, int>
                {
                    [viewerMap.MapUid] = 0,
                    [relatedEyeMap.MapUid] = 1,
                }), Is.True);

                var unrelatedZNetwork = zLevels.CreateZNetwork();
                unrelatedNetwork = unrelatedZNetwork.Owner;
                Assert.That(zLevels.TryAddMapsIntoZNetwork(unrelatedZNetwork, new Dictionary<EntityUid, int>
                {
                    [unrelatedEyeMap.MapUid] = 0,
                }), Is.True);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var zLevels = client.EntMan.System<CMUClientZLevelsSystem>();
                var clientViewerMap = pair.ToClientUid(viewerMap.MapUid);
                var clientRelatedEyeMap = pair.ToClientUid(relatedEyeMap.MapUid);

                Assert.That(zLevels.TryGetEyeMapInViewerZNetwork(
                    clientViewerMap,
                    relatedEyeMap.MapId,
                    out var resolvedEyeMap), Is.True);
                Assert.That(resolvedEyeMap, Is.EqualTo(clientRelatedEyeMap));
                Assert.That(zLevels.TryGetEyeMapInViewerZNetwork(
                    clientViewerMap,
                    MapId.Nullspace,
                    out _), Is.False);
                Assert.That(zLevels.TryGetEyeMapInViewerZNetwork(
                    clientViewerMap,
                    unrelatedEyeMap.MapId,
                    out _), Is.False);
            });
        }
        finally
        {
            await server.WaitAssertion(() => DeleteAll(server.EntMan, network, unrelatedNetwork));
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FalconRemainsVisibleWhenControlledEyeMovesToLowerZLevel()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var lowerMap = await pair.CreateTestMap();
        var upperMap = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid bracer = default;
        EntityUid falcon = default;
        EntityUid drone = default;
        EntityUid network = default;
        EntityUid? previousAttached = null;
        Vector2i openingTile = default;
        bool? previousRenderEnabled = null;
        bool? previousCullingEnabled = null;
        IViewportControl previousMainViewport = null!;
        TestViewportControl testViewport = null!;

        try
        {
            await client.WaitAssertion(() =>
            {
                var config = client.ResolveDependency<IConfigurationManager>();
                previousRenderEnabled = config.GetCVar(CMUZLevelsCVars.RenderEnabled);
                previousCullingEnabled = config.GetCVar(CMUZLevelsCVars.CullOccludedDynamicSprites);
                config.SetCVar(CMUZLevelsCVars.RenderEnabled, true, true);
                config.SetCVar(CMUZLevelsCVars.CullOccludedDynamicSprites, false, true);
            });

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var transform = entMan.System<SharedTransformSystem>();
                var zLevels = entMan.System<CMUZLevelsSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                var zNetwork = zLevels.CreateZNetwork();
                network = zNetwork.Owner;

                Assert.That(zLevels.TryAddMapsIntoZNetwork(zNetwork, new Dictionary<EntityUid, int>
                {
                    [lowerMap.MapUid] = 0,
                    [upperMap.MapUid] = 1,
                }), Is.True);

                hunter = SpawnControllingHunter(
                    entMan,
                    inventory,
                    upperMap.GridCoords,
                    entity => server.PlayerMan.SetAttachedEntity(session, entity),
                    out bracer,
                    out falcon,
                    out drone);

                transform.SetCoordinates(drone, lowerMap.GridCoords);
                Assert.That(entMan.GetComponent<TransformComponent>(hunter).MapUid, Is.EqualTo(upperMap.MapUid));
                Assert.That(entMan.GetComponent<TransformComponent>(drone).MapUid, Is.EqualTo(lowerMap.MapUid));
            });

            await pair.ReallyBeIdle(10);

            Box2 activeViewBounds = default;
            await client.WaitAssertion(() =>
            {
                var eyeManager = client.ResolveDependency<IEyeManager>();
                Assert.That(eyeManager.CurrentEye.Position.MapId, Is.EqualTo(lowerMap.MapId));

                previousMainViewport = eyeManager.MainViewport;
                testViewport = new TestViewportControl(eyeManager.CurrentEye);
                testViewport.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(640, 480)));
                eyeManager.MainViewport = testViewport;

                activeViewBounds = eyeManager.GetWorldViewbounds().CalcBoundingBox();
                Assert.That(activeViewBounds.Size.X, Is.GreaterThan(0));
                Assert.That(activeViewBounds.Size.Y, Is.GreaterThan(0));
            });

            await server.WaitAssertion(() =>
            {
                var map = server.EntMan.System<SharedMapSystem>();
                var tileDefinitions = server.ResolveDependency<ITileDefinitionManager>();
                var plating = new Tile(tileDefinitions["Plating"].TileId);
                var lattice = new Tile(tileDefinitions["Lattice"].TileId);
                var searchBottomLeft = activeViewBounds.BottomLeft.Floored() - new Vector2i(2, 2);
                var searchTopRight = activeViewBounds.TopRight.Ceiled() + new Vector2i(2, 2);
                var leftOpeningX = activeViewBounds.BottomLeft.Floored().X + 1;
                var rightOpeningX = activeViewBounds.TopRight.Ceiled().X - 2;
                var openingX = Math.Abs(leftOpeningX) > Math.Abs(rightOpeningX)
                    ? leftOpeningX
                    : rightOpeningX;
                var openingY = (int) MathF.Floor((activeViewBounds.BottomLeft.Y + activeViewBounds.TopRight.Y) * 0.5f);
                openingTile = new Vector2i(openingX, openingY);

                var tiles = new List<(Vector2i GridIndices, Tile Tile)>();
                for (var x = searchBottomLeft.X; x <= searchTopRight.X; x++)
                {
                    for (var y = searchBottomLeft.Y; y <= searchTopRight.Y; y++)
                    {
                        var indices = new Vector2i(x, y);
                        tiles.Add((indices, indices == openingTile ? lattice : plating));
                    }
                }

                map.SetTiles(upperMap.Grid.Owner, upperMap.Grid.Comp, tiles);
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var entMan = client.EntMan;
                var config = client.ResolveDependency<IConfigurationManager>();
                var eyeManager = client.ResolveDependency<IEyeManager>();
                var mapManager = client.ResolveDependency<IMapManager>();
                var tileDefinitions = client.ResolveDependency<ITileDefinitionManager>();
                var map = entMan.System<SharedMapSystem>();
                var transform = entMan.System<SharedTransformSystem>();
                var spriteSystem = entMan.System<SpriteSystem>();
                var zLevels = entMan.System<CMUClientZLevelsSystem>();
                var culling = entMan.System<CMUZLevelSpriteCullingSystem>();
                var clientDrone = pair.ToClientUid(drone);
                var clientHunter = pair.ToClientUid(hunter);
                var clientUpperGrid = pair.ToClientUid(upperMap.Grid.Owner);
                var droneSprite = entMan.GetComponent<SpriteComponent>(clientDrone);
                var droneXform = entMan.GetComponent<TransformComponent>(clientDrone);
                var hunterXform = entMan.GetComponent<TransformComponent>(clientHunter);
                var upperGrid = entMan.GetComponent<MapGridComponent>(clientUpperGrid);
                var viewBounds = eyeManager.GetWorldViewbounds().CalcBoundingBox();

                Assert.Multiple(() =>
                {
                    Assert.That(eyeManager.CurrentEye.Position.MapId, Is.EqualTo(lowerMap.MapId));
                    Assert.That(hunterXform.MapID, Is.EqualTo(upperMap.MapId));
                    Assert.That(droneXform.MapID, Is.EqualTo(lowerMap.MapId));
                    Assert.That(droneXform.Anchored, Is.False);
                    Assert.That(config.GetCVar(CMUZLevelsCVars.Enabled), Is.True);
                    Assert.That(config.GetCVar(CMUZLevelsCVars.MaxRenderDepth), Is.GreaterThanOrEqualTo(1));
                    Assert.That(droneSprite.Visible, Is.True, "Culling is disabled during setup, so the live Falcon sprite must start visible.");
                });

                Assert.That(map.TryGetTileRef(clientUpperGrid, upperGrid, openingTile, out var openingTileRef), Is.True,
                    $"The synchronized upper grid must contain the opening tile {openingTile}; view={viewBounds}.");
                Assert.That(CMUZLevelOpeningCache.IsOpeningTile(openingTileRef.Tile, tileDefinitions), Is.True,
                    $"The synchronized upper tile {openingTileRef.Tile} must classify as an opening.");

                var intersectingGrids = new List<Entity<MapGridComponent>>();
                mapManager.FindGridsIntersecting(
                    upperMap.MapId,
                    viewBounds,
                    ref intersectingGrids,
                    approx: true,
                    includeMap: true);
                Assert.That(intersectingGrids.Any(grid => grid.Owner == clientUpperGrid), Is.True,
                    $"The synchronized upper grid must intersect the live view {viewBounds}; grid bounds={upperGrid.LocalAABB}.");

                var expectedOpening = new Box2(
                    openingTile.X,
                    openingTile.Y,
                    openingTile.X + 1,
                    openingTile.Y + 1);
                Assert.That(viewBounds.Intersects(expectedOpening), Is.True,
                    $"The opening {expectedOpening} must be inside the live view {viewBounds}.");

                // The test constructs the upper grid after the client Z-level systems have started.
                // Force the production cache to observe the final synchronized tile state.
                zLevels.OpeningCache.Clear();
                var openingBounds = new List<Box2>();
                var openingGrids = new List<Entity<MapGridComponent>>();
                Assert.That(zLevels.OpeningCache.TryFindOpeningBounds(
                    upperMap.MapId,
                    viewBounds,
                    openingBounds,
                    out _,
                    int.MaxValue,
                    true,
                    openingGrids,
                    mapManager,
                    map,
                    transform,
                    tileDefinitions), Is.True);

                var droneWorldPosition = transform.GetWorldPosition(clientDrone);
                var localBounds = spriteSystem.GetLocalBounds((clientDrone, droneSprite));
                var radius = localBounds.Size.Length() * 0.5f + droneSprite.Offset.Length() + 0.25f;
                var droneWorldBounds = new Box2(
                    droneWorldPosition.X - radius,
                    droneWorldPosition.Y - radius,
                    droneWorldPosition.X + radius,
                    droneWorldPosition.Y + radius);

                Assert.Multiple(() =>
                {
                    Assert.That(openingBounds.Any(bounds => bounds.Intersects(expectedOpening)), Is.True,
                        "The upper/player map must contain an opening inside the active Falcon view.");
                    Assert.That(openingBounds.All(bounds => !bounds.Intersects(droneWorldBounds)), Is.True,
                        "The lower-map Falcon must be outside every upper-map opening so pre-fix culling reaches HideSprite.");
                });

                config.SetCVar(CMUZLevelsCVars.CullOccludedDynamicSprites, true, true);
                culling.FrameUpdate(0.016f);

                Assert.That(droneSprite.Visible, Is.True,
                    "A Falcon occupying the active Eye map must not be hidden as an occluded sprite below the controller.");
            });
        }
        finally
        {
            await client.WaitAssertion(() =>
            {
                var config = client.ResolveDependency<IConfigurationManager>();
                var eyeManager = client.ResolveDependency<IEyeManager>();
                config.SetCVar(CMUZLevelsCVars.CullOccludedDynamicSprites, false, true);
                client.EntMan.System<CMUZLevelSpriteCullingSystem>().FrameUpdate(0.016f);

                if (previousRenderEnabled is { } renderEnabled)
                    config.SetCVar(CMUZLevelsCVars.RenderEnabled, renderEnabled, true);
                if (previousCullingEnabled is { } cullingEnabled)
                    config.SetCVar(CMUZLevelsCVars.CullOccludedDynamicSprites, cullingEnabled, true);

                if (previousMainViewport != null)
                    eyeManager.MainViewport = previousMainViewport;
            });

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.SingleOrDefault();
                if (session != null)
                    server.PlayerMan.SetAttachedEntity(session, previousAttached);

                DeleteAll(entMan, hunter, bracer, falcon, drone, network);
            });
        }

        await pair.CleanReturnAsync();
    }

    private static EntityUid SpawnControllingHunter(
        IEntityManager entMan,
        InventorySystem inventory,
        EntityCoordinates coordinates,
        Action<EntityUid> attachController,
        out EntityUid bracer,
        out EntityUid falcon,
        out EntityUid drone)
    {
        var hunter = entMan.SpawnEntity("CMMobHuman", coordinates);
        bracer = entMan.SpawnEntity("CMUYautjaBracer", coordinates);
        falcon = entMan.SpawnEntity("CMUYautjaFalconDrone", coordinates);

        entMan.EnsureComponent<YautjaComponent>(hunter);
        attachController(hunter);
        Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

        entMan.EventBus.RaiseLocalEvent(falcon, new UseInHandEvent(hunter));
        drone = entMan.GetComponent<YautjaFalconControllerComponent>(hunter).Drone;
        Assert.That(entMan.HasComponent<YautjaFalconDroneDeployedComponent>(drone), Is.True);

        return hunter;
    }

    private static void DeleteAll(IEntityManager entMan, params EntityUid[] entities)
    {
        foreach (var uid in entities)
        {
            if (uid != default && !entMan.Deleted(uid))
                entMan.DeleteEntity(uid);
        }
    }

#nullable enable
    private sealed class TestViewportControl(IEye eye) : Control, IViewportControl
    {
        public override IClydeWindow? Window => null;

        public MapCoordinates ScreenToMap(Vector2 coords)
        {
            var point = coords - PixelSize / 2f;
            point *= new Vector2(1, -1) / EyeManager.PixelsPerMeter;
            eye.GetViewMatrixInv(out var viewMatrixInv, Vector2.One);
            return new MapCoordinates(Vector2.Transform(point, viewMatrixInv), eye.Position.MapId);
        }

        public MapCoordinates PixelToMap(Vector2 point)
        {
            return ScreenToMap(point);
        }

        public Vector2 WorldToScreen(Vector2 map)
        {
            eye.GetViewMatrix(out var viewMatrix, Vector2.One);
            var point = Vector2.Transform(map, viewMatrix);
            point *= new Vector2(1, -1) * EyeManager.PixelsPerMeter;
            return point + PixelSize / 2f;
        }

        public Matrix3x2 GetWorldToScreenMatrix()
        {
            return Matrix3x2.Identity;
        }

        public Matrix3x2 GetLocalToScreenMatrix()
        {
            return Matrix3x2.Identity;
        }
    }
#nullable restore
}
