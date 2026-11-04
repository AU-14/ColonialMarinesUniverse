using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.ZLevels;

[TestFixture]
public sealed class CMUZShotPathTest : GameTest
{
    [TestCase(0)]
    [TestCase(90)]
    public async Task OpenAirShotsRespectFloorsOnSeparateGrids(int rotation)
    {
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var zLevels = SEntMan.System<CMUSharedZLevelsSystem>();
            var tiles = Server.ResolveDependency<ITileDefinitionManager>();
            var map = maps.CreateMap(out var mapId, runMapInit: true);
            try
            {
                Assert.That(zLevels.IsZShotPathOpen(map, Vector2.Zero, Vector2.One), Is.True);

                var grid = maps.CreateGridEntity(mapId);
                transform.SetLocalPosition(grid.Owner, new Vector2(10, 20));
                transform.SetLocalRotation(grid.Owner, Angle.FromDegrees(rotation));
                var from = transform.ToMapCoordinates(new EntityCoordinates(grid, new Vector2(0.5f, 0.5f))).Position;
                var to = transform.ToMapCoordinates(new EntityCoordinates(grid, new Vector2(4.5f, 0.5f))).Position;
                maps.SetTile(grid, new Vector2i(2, 0), new Tile(tiles["Plating"].TileId));

                Assert.That(zLevels.IsZShotPathOpen(map, from, to), Is.False,
                    "A map entity without MapGrid must still check floors on its child grids.");

                maps.SetTile(grid, new Vector2i(2, 0), new Tile(tiles["Lattice"].TileId));
                Assert.That(zLevels.IsZShotPathOpen(map, from, to), Is.True);

                var secondGrid = maps.CreateGridEntity(mapId);
                transform.SetLocalPosition(secondGrid.Owner, new Vector2(10, 20));
                transform.SetLocalRotation(secondGrid.Owner, Angle.FromDegrees(rotation));
                maps.SetTile(secondGrid, new Vector2i(3, 0), new Tile(tiles["Plating"].TileId));
                Assert.That(zLevels.IsZShotPathOpen(map, from, to), Is.False,
                    "Every grid crossed by the shot must be checked.");
            }
            finally
            {
                SEntMan.DeleteEntity(map);
            }
        });
    }
}
