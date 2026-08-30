using Content.Client.Paper;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Paper;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using static Content.Shared.Paper.PaperComponent;

namespace Content.IntegrationTests.Tests.Paper;

[TestFixture]
[TestOf(typeof(PaperVisualizerSystem))]
public sealed class PaperSpriteVisualRegressionTest : GameTest
{
    [Test]
    public async Task WritingAndStampingUpdateClientSpriteLayers()
    {
        var map = await Pair.CreateTestMap();
        EntityUid paper = default;

        await Server.WaitPost(() => paper = SEntMan.SpawnAtPosition("CMPaper", map.GridCoords));
        await Pair.RunUntilSynced();

        var clientPaper = CEntMan.GetEntity(SEntMan.GetNetEntity(paper));

        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.HasComponent<PaperVisualizerComponent>(clientPaper), Is.True,
                "CM paper prototypes need the sprite visualizer in addition to the paper UI visuals");
            AssertLayerVisibility(clientPaper, PaperVisualLayers.Writing, false);
            AssertLayerVisibility(clientPaper, PaperVisualLayers.Stamp, false);
        });

        await Server.WaitPost(() =>
        {
            var paperSystem = Server.System<PaperSystem>();
            var paperComponent = SEntMan.GetComponent<PaperComponent>(paper);
            paperSystem.SetContent((paper, paperComponent), "Written content");
            paperSystem.TryStamp(
                (paper, paperComponent),
                new StampDisplayInfo { StampedName = "Test stamp", StampedColor = Color.Red },
                "paper_stamped");
        });
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            AssertLayerVisibility(clientPaper, PaperVisualLayers.Writing, true);
            AssertLayerVisibility(clientPaper, PaperVisualLayers.Stamp, true);
        });
    }

    private void AssertLayerVisibility(EntityUid uid, PaperVisualLayers layer, bool expected)
    {
        var spriteSystem = Client.System<SpriteSystem>();
        var sprite = CEntMan.GetComponent<SpriteComponent>(uid);
        var index = spriteSystem.LayerMapGet((uid, sprite), layer);
        Assert.That(sprite[index].Visible, Is.EqualTo(expected), $"{layer} layer visibility");
    }
}
