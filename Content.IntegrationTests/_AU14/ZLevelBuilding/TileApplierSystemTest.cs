using Content.Server._AU14.ZLevelBuilding;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._AU14.ZLevelBuilding;
using Content.Shared.Shuttles.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests._AU14.ZLevelBuilding;

[TestFixture]
public sealed class TileApplierSystemTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: AU14TestAnchoredTileDependent
  components:
  - type: Transform
    anchored: true

- type: entity
  id: AU14TestStructuralAnchor
  components:
  - type: Transform
    anchored: true
  - type: StructuralSupport
    isAnchor: true
    isVerticalSupport: true
    cantileverSpan: 3
";

    [Test]
    public async Task DeletingFloorSupportDefersTileRemovalPastTermination()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid support = default;
        EntityUid dependent = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var mapSystem = entities.System<SharedMapSystem>();
            mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(1, 0), map.Tile.Tile);
            support = entities.SpawnEntity("AU14TileFloorSupport", map.GridCoords);
            dependent = entities.SpawnEntity("AU14TestAnchoredTileDependent", map.GridCoords);

            Assert.That(entities.HasComponent<TileFloorSupportComponent>(support), Is.True);
            Assert.DoesNotThrow(() => entities.DeleteEntity(support));
        });

        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var mapSystem = entities.System<SharedMapSystem>();
            var tile = mapSystem.GetTileRef(map.Grid.Owner, map.Grid.Comp, map.GridCoords);

            Assert.Multiple(() =>
            {
                Assert.That(tile.Tile.IsEmpty, Is.True);
                Assert.That(entities.Deleted(support), Is.True);
                Assert.That(entities.Deleted(dependent), Is.False);
                Assert.That(entities.GetComponent<TransformComponent>(dependent).Anchored, Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ColocatedSupportsRemainValidWhenEitherIsRemoved()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid first = default;
        EntityUid second = default;
        await server.WaitAssertion(() =>
        {
            first = server.EntMan.SpawnEntity("AU14TestStructuralAnchor", map.GridCoords);
            second = server.EntMan.SpawnEntity("AU14TestStructuralAnchor", map.GridCoords);

            var supports = server.EntMan.System<ZLevelSupportSystem>();
            supports.RecomputeGrid(map.Grid);
            Assert.Multiple(() =>
            {
                Assert.That(server.EntMan.GetComponent<StructuralSupportComponent>(first).Supported, Is.True);
                Assert.That(server.EntMan.GetComponent<StructuralSupportComponent>(second).Supported, Is.True);
            });

            server.EntMan.DeleteEntity(first);
            supports.RecomputeGrid(map.Grid);
            Assert.That(server.EntMan.GetComponent<StructuralSupportComponent>(second).Supported, Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemovingLastBeamCollapsesDependentUpperFloor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid upperGrid = default;
        Vector2i upperTile = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var transforms = entities.System<SharedTransformSystem>();
            var maps = entities.System<SharedMapSystem>();
            var building = entities.System<ZLevelBuildingSystem>();
            var mapUid = entities.GetComponent<TransformComponent>(map.Grid.Owner).MapUid!.Value;
            var world = transforms.ToMapCoordinates(map.GridCoords).Position;

            var beam = entities.SpawnEntity("AU14NavalisSupportBeamGreen1Tile", map.GridCoords);
            Assert.That(building.EnsureNeighborLevel(mapUid, 1, map.Grid.Owner, world, out var upperMap, out upperGrid), Is.True);

            var upperMapComp = entities.GetComponent<MapComponent>(upperMap);
            var upperCoords = transforms.ToCoordinates(upperGrid, new MapCoordinates(world, upperMapComp.MapId));
            entities.SpawnEntity("AU14TileApplierPlating", upperCoords);

            var upperGridComp = entities.GetComponent<MapGridComponent>(upperGrid);
            upperTile = maps.TileIndicesFor(upperGrid, upperGridComp, upperCoords);
            entities.DeleteEntity(beam);
        });

        // Five-second warning plus scheduling slack.
        await pair.RunTicksSync(400);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            var upperGridComp = entities.GetComponent<MapGridComponent>(upperGrid);
            Assert.That(maps.GetTileRef(upperGrid, upperGridComp, upperTile).Tile.IsEmpty, Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CompromisedShuttleIsRejectedByGenericFtlGate()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            server.EntMan.EnsureComponent<ShuttleComponent>(map.Grid.Owner);
            server.EntMan.EnsureComponent<ZCollapseCompromisedComponent>(map.Grid.Owner);

            var shuttle = server.EntMan.System<ShuttleSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(shuttle.CanFTL(map.Grid.Owner, out var reason), Is.False);
                Assert.That(reason, Is.Not.Empty);
            });
        });

        await pair.CleanReturnAsync();
    }
}
