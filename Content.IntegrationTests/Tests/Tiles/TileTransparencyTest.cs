using Content.IntegrationTests.Fixtures;
using Content.Shared._CMU14.ZLevels.Core;
using Content.Shared.Maps;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.Tiles;

[TestFixture]
[TestOf(typeof(ContentTileDefinition))]
[TestOf(typeof(CMUZLevelOpeningCache))]
public sealed class TileTransparencyTest : GameTest
{
    [Test]
    public async Task TransparentTilesDeserializeAndDriveOpeningSelection()
    {
        var tiles = Server.ResolveDependency<ITileDefinitionManager>();

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                AssertTile(tiles, "Space", expectedTransparent: true);
                AssertTile(tiles, "Lattice", expectedTransparent: true);
                AssertTile(tiles, "CMUFloorEmpty", expectedTransparent: true);

                AssertTile(tiles, "FloorSteel", expectedTransparent: false);
                AssertTile(tiles, "Plating", expectedTransparent: false);

                Assert.That(CMUZLevelOpeningCache.IsOpeningTile(Tile.Empty, tiles), Is.True,
                    "An empty tile must remain an opening independently of prototype data.");
            });
        });
    }

    private static void AssertTile(
        ITileDefinitionManager tiles,
        string id,
        bool expectedTransparent)
    {
        Assert.That(tiles[id], Is.TypeOf<ContentTileDefinition>());
        var definition = (ContentTileDefinition) tiles[id];

        Assert.Multiple(() =>
        {
            Assert.That(definition.Transparent, Is.EqualTo(expectedTransparent),
                $"Tile prototype {id} did not deserialize its transparent field as expected.");
            Assert.That(
                CMUZLevelOpeningCache.IsOpeningTile(new Tile(definition.TileId), tiles),
                Is.EqualTo(expectedTransparent),
                $"The z-level opening cache did not consume {id}'s transparent value.");
        });
    }
}
