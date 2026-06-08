using System.Reflection;
using Content.Client.Eye.Blinding;
using Content.Client.Viewport;
using NUnit.Framework;
using GraphicsEye = Robust.Shared.Graphics.Eye;

namespace Content.Tests.Client.EyeBlinding;

[TestFixture]
public sealed class BlurryVisionOverlayTest
{
    [Test]
    public void PlayerEyeDrawsPlayerBlur()
    {
        var playerEye = new GraphicsEye();

        Assert.That(ShouldDrawForViewportEye(playerEye, playerEye), Is.True);
    }

    [Test]
    public void UnrelatedEyeDoesNotDrawPlayerBlur()
    {
        var playerEye = new GraphicsEye();
        var otherEye = new GraphicsEye();

        Assert.That(ShouldDrawForViewportEye(otherEye, playerEye), Is.False);
    }

    [Test]
    public void CurrentZLevelEyeDrawsPlayerBlur()
    {
        var playerEye = new GraphicsEye();
        var zEye = new ScalingViewport.ZEye
        {
            Depth = 0,
            BlurCurrentLevel = true,
        };

        Assert.That(ShouldDrawForViewportEye(zEye, playerEye), Is.True);
    }

    [Test]
    public void LookedUpZLevelEyeDoesNotDrawPlayerBlurAgain()
    {
        var playerEye = new GraphicsEye();
        var zEye = new ScalingViewport.ZEye
        {
            Depth = 1,
            BlurCurrentLevel = false,
        };

        Assert.That(ShouldDrawForViewportEye(zEye, playerEye), Is.False);
    }

    private static bool ShouldDrawForViewportEye(object viewportEye, GraphicsEye playerEye)
    {
        var method = typeof(BlurryVisionOverlay).GetMethod(
            "ShouldDrawForViewportEye",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);

        return (bool) method!.Invoke(null, new object[] { viewportEye, playerEye })!;
    }
}
