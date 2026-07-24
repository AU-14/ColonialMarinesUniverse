using Content.Client._CMU14.ZLevels.Core;
using Content.Client.Viewport;
using Content.Shared._CMU14.ZLevels;
using NUnit.Framework;

namespace Content.Tests.Client._CMU14.ZLevels;

[TestFixture]
public sealed class ZLevelBlurOverlayTest
{
    [Test]
    public void BlurIsEnabledByDefault()
    {
        Assert.That(
            CMUZLevelBlurOverlay.IsBlurEnabled(CMUZLevelsCVars.BlurStrength.DefaultValue),
            Is.True);
    }

    [TestCase(0f, false)]
    [TestCase(1f, true)]
    public void BlurStrengthControlsWhetherBlurIsEnabled(float strength, bool enabled)
    {
        Assert.That(CMUZLevelBlurOverlay.IsBlurEnabled(strength), Is.EqualTo(enabled));
    }

    [Test]
    public void LowerLevelPassStillBlurs()
    {
        var eye = new ScalingViewport.ZEye
        {
            Depth = -1,
        };

        Assert.That(CMUZLevelBlurOverlay.ShouldBlurPass(eye), Is.True);
    }

    [Test]
    public void ManualLookUpBasePassBlurs()
    {
        var eye = new ScalingViewport.ZEye
        {
            Depth = 0,
            BlurCurrentLevel = true,
        };

        Assert.That(CMUZLevelBlurOverlay.ShouldBlurPass(eye), Is.True);
    }

    [Test]
    public void BasePassWithoutManualLookUpDoesNotBlur()
    {
        var eye = new ScalingViewport.ZEye
        {
            Depth = 0,
            BlurCurrentLevel = false,
        };

        Assert.That(CMUZLevelBlurOverlay.ShouldBlurPass(eye), Is.False);
    }

    [Test]
    public void LookedUpLevelPassDoesNotBlur()
    {
        var eye = new ScalingViewport.ZEye
        {
            Depth = 1,
            BlurCurrentLevel = true,
        };

        Assert.That(CMUZLevelBlurOverlay.ShouldBlurPass(eye), Is.False);
    }
}
