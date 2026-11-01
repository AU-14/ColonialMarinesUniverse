using Content.IntegrationTests.Fixtures;
using Content.Server.Light.EntitySystems;
using Content.Shared._RMC14.Areas;
using Content.Shared.Light.Components;
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
            SEntMan.DeleteEntity(marker);
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
            }
        });
    }
}
