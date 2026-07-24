using Content.IntegrationTests.Fixtures;
using Robust.Shared.Localization;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.ZLevels;

[TestFixture]
public sealed class CMUMultiZTileLocalizationTest : GameTest
{
    private static readonly (string Id, string Name)[] ExpectedTiles =
    [
        ("AU14LatticeTile2", "MultiZ Destroyable Grate 1"),
        ("AU14LatticeTile3", "MultiZ Destroyable Grate 2"),
        ("AU14LatticeTile4", "MultiZ Destroyable Grate 3"),
        ("AU14LatticeTile5", "MultiZ Destroyable Grate 4"),
        ("AU14LatticeTile6", "MultiZ Destroyable Grate 5"),
        ("AU14LatticeTile7", "MultiZ Destroyable Grate 6"),
        ("AU14LatticeTileHalf1", "MultiZ Destroyable Half Grate 1"),
        ("AU14LatticeTileHalf2", "MultiZ Destroyable Half Grate 2"),
        ("AU14LatticeTileHalf3", "MultiZ Destroyable Half Grate 3"),
        ("AU14LatticeTileHalf4", "MultiZ Destroyable Half Grate 4"),
        ("AU14LatticeTile2Indestructible", "MultiZ Indestructible Grate 1"),
        ("AU14LatticeTile3Indestructible", "MultiZ Indestructible Grate 2"),
        ("AU14LatticeTile4Indestructible", "MultiZ Indestructible Grate 3"),
        ("AU14LatticeTile5Indestructible", "MultiZ Indestructible Grate 4"),
        ("AU14LatticeTile6Indestructible", "MultiZ Indestructible Grate 5"),
        ("AU14LatticeTile8Indestructible", "MultiZ Indestructible Grate 6"),
        ("AU14LatticeTileHalf1Indestructible", "MultiZ Destroyable Half Grate 1"),
        ("AU14LatticeTileHalf2Indestructible", "MultiZ Destroyable Half Grate 2"),
        ("AU14LatticeTileHalf3Indestructible", "MultiZ Destroyable Half Grate 3"),
        ("AU14LatticeTileHalf4Indestructible", "MultiZ Destroyable Half Grate 4"),
    ];

    [Test]
    public async Task EditorTileNamesResolveWithoutFallback()
    {
        await Client.WaitAssertion(() =>
        {
            var localization = Client.ResolveDependency<ILocalizationManager>();
            var tiles = Client.ResolveDependency<ITileDefinitionManager>();

            Assert.Multiple(() =>
            {
                foreach (var (id, expectedName) in ExpectedTiles)
                {
                    Assert.That(tiles.TryGetDefinition(id, out var tile), Is.True, $"Missing tile {id}.");
                    Assert.That(localization.HasString(tile!.Name), Is.True,
                        $"Tile {id} uses unknown localization ID {tile.Name}.");
                    Assert.That(localization.GetString(tile.Name), Is.EqualTo(expectedName));
                }
            });
        });
    }
}
