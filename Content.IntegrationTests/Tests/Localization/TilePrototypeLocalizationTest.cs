using Content.IntegrationTests.Fixtures;
using Content.Shared.Maps;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.Tests.Localization;

public sealed class TilePrototypeLocalizationTest : GameTest
{
    [Test]
    public void EditorVisibleTilesHaveLocalizedNames()
    {
        var localization = Server.ResolveDependency<ILocalizationManager>();

        var invalidNames = Server.ProtoMan.EnumeratePrototypes<ContentTileDefinition>()
            .Where(tile => !tile.Abstract &&
                           !tile.EditorHidden &&
                           (string.IsNullOrWhiteSpace(tile.Name) || !localization.HasString(tile.Name)))
            .Select(tile => $"{tile.ID}: {(string.IsNullOrWhiteSpace(tile.Name) ? "<empty>" : tile.Name)}")
            .Order()
            .ToArray();

        Assert.That(invalidNames, Is.Empty,
            $"Editor-visible tiles with invalid en-US names:\n{string.Join('\n', invalidNames)}");
    }
}
