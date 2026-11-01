using Content.IntegrationTests.Fixtures;
using Content.Server.Light.EntitySystems;
using Content.Shared._RMC14.Areas;
using Content.Shared.Light.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.Light;

[TestFixture]
public sealed class RoofRenderBatchTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  id: RoofRenderOpenArea
  components:
  - type: Area
    weatherEnabled: true
- type: entity
  id: RoofRenderBlockedArea
  components:
  - type: Area
    weatherEnabled: false
- type: entity
  id: RoofRenderWideEntity
  components:
  - type: Physics
    bodyType: Static
  - type: Fixtures
    fixtures:
      roof:
        shape:
          !type:PhysShapeAabb
          bounds: "-2,-0.2,2,0.2"
        density: 1
        hard: false
  - type: IsRoof
- type: entity
  parent: RoofRenderWideEntity
  id: RoofRenderTileEntity
  components:
  - type: Fixtures
    fixtures:
      roof:
        shape:
          !type:PhysShapeAabb
          bounds: "-0.5,-0.5,0.5,0.5"
        density: 1
        hard: false
""";

    [Test]
    public async Task BatchIncludesOverlappingFixturesOnRotatedGridsAndOmitsOpenTiles()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var roofs = SEntMan.System<RoofSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(map.Grid.Owner);
            var roof = SEntMan.EnsureComponent<RoofComponent>(map.Grid.Owner);
            var index = map.Tile.GridIndices;
            var maps = SEntMan.System<SharedMapSystem>();
            for (var x = -4; x <= 5; x++)
            for (var y = -2; y <= 2; y++)
                maps.SetTile(map.Grid.Owner, grid, index + new Vector2i(x, y), map.Tile.Tile);
            var bounds = SEntMan.System<EntityLookupSystem>().GetLocalBounds(index, grid.TileSize).Enlarged(4);
            var marker = SEntMan.SpawnEntity("RoofRenderWideEntity",
                new EntityCoordinates(map.Grid.Owner, index.X + 2.3f, index.Y + .5f));
            SEntMan.GetComponent<IsRoofComponent>(marker).Color = Color.Red;
            transform.SetLocalRotation(map.Grid.Owner, Angle.FromDegrees(35));
            Assert.That(roofs.GetColor((map.Grid.Owner, grid, roof), index), Is.EqualTo(Color.Red),
                "The fixture must intersect this tile in the ordinary lookup before comparing the batch.");
            var tiles = new HashSet<Vector2i>();
            roofs.GetEntityRoofTiles((map.Grid.Owner, grid), bounds, tiles);
            Assert.That(tiles.Contains(index), Is.True, "A fixture can shade this tile while its entity origin is outside it.");
            Assert.That(roofs.GetColor((map.Grid.Owner, grid, roof), index, tiles.Contains(index)), Is.EqualTo(Color.Red));
            Assert.That(tiles.Contains(index + new Vector2i(-3, 0)), Is.False, "Open terrain must skip per-tile spatial searches.");
            // CMU14: the color batch must retain exact fixture geometry on rotated grids.
            transform.SetLocalRotation(marker, Angle.FromDegrees(40));
            var colors = new Dictionary<Vector2i, Color>();
            roofs.GetEntityRoofColors((map.Grid.Owner, grid, roof), bounds, colors, tiles);
            for (var x = -4; x <= 5; x++)
            for (var y = -2; y <= 2; y++)
            {
                var tile = index + new Vector2i(x, y);
                var color = roofs.GetColor((map.Grid.Owner, grid, roof), tile, tiles.Contains(tile),
                    colors.TryGetValue(tile, out var pointColor) ? pointColor : null);
                Assert.That(color, Is.EqualTo(roofs.GetColor((map.Grid.Owner, grid, roof), tile)), $"Tile {tile}");
            }
            SEntMan.DeleteEntity(marker);

            // CMU14: a full-tile fixture uses proven coverage. Rotation changes
            // its edge coverage, and deletion must remove its contribution.
            var wall = SEntMan.SpawnEntity("RoofRenderTileEntity",
                new EntityCoordinates(map.Grid.Owner, index.X + .5f, index.Y + .5f));
            SEntMan.GetComponent<IsRoofComponent>(wall).Color = Color.Blue;
            roofs.GetEntityRoofColors((map.Grid.Owner, grid, roof), bounds, colors, tiles);
            Assert.That(colors[index], Is.EqualTo(Color.Blue));
            Assert.That(tiles.Contains(index), Is.False);
            transform.SetLocalRotation(wall, Angle.FromDegrees(45));
            roofs.GetEntityRoofColors((map.Grid.Owner, grid, roof), bounds, colors, tiles);
            for (var x = -1; x <= 1; x++)
            for (var y = -1; y <= 1; y++)
            {
                var tile = index + new Vector2i(x, y);
                var color = roofs.GetColor((map.Grid.Owner, grid, roof), tile, tiles.Contains(tile),
                    colors.TryGetValue(tile, out var fixtureColor) ? fixtureColor : null);
                Assert.That(color, Is.EqualTo(roofs.GetColor((map.Grid.Owner, grid, roof), tile)), $"Rotated wall tile {tile}");
            }
            SEntMan.DeleteEntity(wall);
            roofs.GetEntityRoofColors((map.Grid.Owner, grid, roof), bounds, colors, tiles);
            Assert.That(colors.ContainsKey(index) || tiles.Contains(index), Is.False);
        });
    }

    [Test]
    public async Task RenderBatchTracksEntityRoofsAndPreservesTileAndAreaColors()
    {
        var map = await Pair.CreateTestMap();
        var otherMap = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var roofs = SEntMan.System<RoofSystem>();
            var areas = SEntMan.System<AreaSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(map.Grid.Owner);
            var roof = SEntMan.EnsureComponent<RoofComponent>(map.Grid.Owner);
            var areaGrid = SEntMan.EnsureComponent<AreaGridComponent>(map.Grid.Owner);
            var indices = map.Tile.GridIndices;
            var gridColor = roof.Color = Color.Black;
            var entityColor = Color.Red;
            var tiles = new HashSet<Vector2i>();
            var colors = new Dictionary<Vector2i, Color>(); // CMU14
            var exact = new HashSet<Vector2i>(); // CMU14
            var bounds = SEntMan.System<EntityLookupSystem>().GetLocalBounds(indices, grid.TileSize).Enlarged(4);
            areas.ReplaceArea(areaGrid, indices, "RoofRenderOpenArea");
            Check(false, null);

            var marker = SEntMan.SpawnEntity(null, otherMap.GridCoords);
            var entityRoof = SEntMan.AddComponent<IsRoofComponent>(marker);
            entityRoof.Color = entityColor;
            Check(false, null); // Roofs on another map cannot force this map's tile searches.

            transform.SetCoordinates(marker,
                new EntityCoordinates(map.Grid.Owner, indices.X + .5f, indices.Y + .5f));
            Check(true, entityColor);
            SEntMan.System<MetaDataSystem>().SetEntityPaused(marker, true);
            Check(true, entityColor); // Paused entities still contribute to rendering.
            SEntMan.System<MetaDataSystem>().SetEntityPaused(marker, false);
            entityRoof.Enabled = false;
            Check(false, null);
            areas.ReplaceArea(areaGrid, indices, "RoofRenderBlockedArea");
            Check(false, gridColor);
            entityRoof.Enabled = true;
            Check(true, entityColor);
            roofs.SetRoof((map.Grid.Owner, grid, roof), indices, true);
            Check(true, gridColor); // Explicit roof bits retain priority over entity colors.
            roofs.SetRoof((map.Grid.Owner, grid, roof), indices, false);
            Check(true, entityColor);
            SEntMan.DeleteEntity(marker);
            Check(false, gridColor);
            areas.ReplaceArea(areaGrid, indices, "RoofRenderOpenArea");
            Check(false, null);

            void Check(bool entities, Color? expected)
            {
                roofs.GetEntityRoofTiles((map.Grid.Owner, grid), bounds, tiles);
                var queryEntities = tiles.Contains(indices);
                Assert.That(queryEntities, Is.EqualTo(entities));
                var batched = roofs.GetColor((map.Grid.Owner, grid, roof), indices, queryEntities);
                Assert.That(batched, Is.EqualTo(expected));
                Assert.That(batched, Is.EqualTo(roofs.GetColor((map.Grid.Owner, grid, roof), indices)),
                    "Batch rendering must preserve the ordinary roof lookup result after every state change.");
                // CMU14: exercise the point fast path through the same live state transitions.
                roofs.GetEntityRoofColors((map.Grid.Owner, grid, roof), bounds, colors, exact);
                Assert.That(exact, Is.Empty, "Point roofs should not need per-tile spatial searches.");
                Assert.That(roofs.GetColor((map.Grid.Owner, grid, roof), indices, exact.Contains(indices),
                    colors.TryGetValue(indices, out var color) ? color : null), Is.EqualTo(expected));
                Assert.That(roofs.IsRooved((map.Grid.Owner, grid, roof), indices, exact.Contains(indices),
                    colors.ContainsKey(indices)), Is.EqualTo(roofs.IsRooved((map.Grid.Owner, grid, roof), indices)),
                    "Weather roof coverage must track entity and explicit roofs without treating area lighting as a roof.");
            }
        });
    }

    // CMU14
    [Test]
    public async Task ColorBatchPreservesBoundaryExclusionsColorConflictsAndContainedRoofs()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var roofs = SEntMan.System<RoofSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var containers = SEntMan.System<SharedContainerSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(map.Grid.Owner);
            var roof = SEntMan.EnsureComponent<RoofComponent>(map.Grid.Owner);
            var index = map.Tile.GridIndices;
            var bounds = SEntMan.System<EntityLookupSystem>().GetLocalBounds(index, grid.TileSize).Enlarged(3);
            var areaGrid = SEntMan.EnsureComponent<AreaGridComponent>(map.Grid.Owner);
            SEntMan.System<AreaSystem>().ReplaceArea(areaGrid, index, "RoofRenderOpenArea");
            var colors = new Dictionary<Vector2i, Color>();
            var exact = new HashSet<Vector2i>();
            var marker = SEntMan.SpawnEntity(null, new EntityCoordinates(map.Grid.Owner, index.X, index.Y + .5f));
            var markerRoof = SEntMan.AddComponent<IsRoofComponent>(marker);
            markerRoof.Color = Color.Red;
            Check(null); // A point on a tile boundary is excluded by the engine's shrunken query.
            transform.SetLocalPosition(marker, new System.Numerics.Vector2(index.X + .5f, index.Y + .5f));
            Check(Color.Red);

            var other = SEntMan.SpawnEntity(null, new EntityCoordinates(map.Grid.Owner, index.X + .6f, index.Y + .5f));
            SEntMan.AddComponent<IsRoofComponent>(other).Color = Color.Blue;
            Check(roofs.GetColor((map.Grid.Owner, grid, roof), index));
            Assert.That(exact.Contains(index), Is.True, "Different colors must retain the engine's lookup priority.");

            markerRoof.Enabled = false;
            var container = containers.EnsureContainer<Container>(marker, "roof-test");
            Assert.That(containers.Insert(other, container), Is.True);
            Check(Color.Blue); // A disabled parent can still contribute a contained roof.
            SEntMan.DeleteEntity(other);
            Check(null);
            SEntMan.DeleteEntity(marker);

            void Check(Color? expected)
            {
                roofs.GetEntityRoofColors((map.Grid.Owner, grid, roof), bounds, colors, exact);
                var batched = roofs.GetColor((map.Grid.Owner, grid, roof), index, exact.Contains(index),
                    colors.TryGetValue(index, out var color) ? color : null);
                Assert.That(batched, Is.EqualTo(expected));
                Assert.That(batched, Is.EqualTo(roofs.GetColor((map.Grid.Owner, grid, roof), index)));
            }
        });
    }
}
