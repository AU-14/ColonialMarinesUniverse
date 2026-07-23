using Content.Client.Parallax;
using NUnit.Framework;

namespace Content.Tests.Client._CMU14.ZLevels;

[TestFixture]
public sealed class ParallaxZLevelPolicyTest
{
    [Test]
    public void ViewportOptOutKeepsOrdinaryParallaxOnStackedMap()
    {
        Assert.That(
            ParallaxOverlay.ShouldDrawOrdinaryPass(
                viewportRenderZLevels: false,
                zLevelsEnabled: true,
                renderEnabled: true,
                hasLowerMap: true),
            Is.True);
    }

    [Test]
    public void ComposedViewportDrawsParallaxOnlyOnLowestPass()
    {
        Assert.That(
            ParallaxOverlay.ShouldDrawOrdinaryPass(
                viewportRenderZLevels: true,
                zLevelsEnabled: true,
                renderEnabled: true,
                hasLowerMap: true),
            Is.False);

        Assert.That(
            ParallaxOverlay.ShouldDrawOrdinaryPass(
                viewportRenderZLevels: true,
                zLevelsEnabled: true,
                renderEnabled: true,
                hasLowerMap: false),
            Is.True);
    }
}
