using Content.Client.CMU14.ZLevels.Core;
using Content.Client.Viewport;
using NUnit.Framework;
using GraphicsEye = Robust.Shared.Graphics.Eye;

namespace Content.Tests.Client.CMU14.ZLevels;

[TestFixture]
public sealed class CMUViewportEyeExtensionsTest
{
    [Test]
    public void PlayerEyeIsPlayerView()
    {
        var playerEye = new GraphicsEye();

        Assert.That(playerEye.IsPlayerView(playerEye), Is.True);
    }

    [Test]
    public void UnrelatedEyeIsNotPlayerView()
    {
        Assert.That(new GraphicsEye().IsPlayerView(new GraphicsEye()), Is.False);
    }

    [Test]
    public void NullEyeIsNotPlayerView()
    {
        Assert.That(((Robust.Shared.Graphics.IEye?) null).IsPlayerView(new GraphicsEye()), Is.False);
    }

    [TestCase(true, true)]
    [TestCase(false, false)]
    public void ZEyeIsPlayerViewOnlyOnFinalPass(bool isFinalPass, bool expected)
    {
        var playerEye = new GraphicsEye();
        var zEye = new ScalingViewport.ZEye { SourceEye = playerEye, IsFinalPass = isFinalPass };

        Assert.That(zEye.IsPlayerView(playerEye), Is.EqualTo(expected));
    }

    [Test]
    public void ZEyeFromAnotherViewportIsNotPlayerView()
    {
        var zEye = new ScalingViewport.ZEye { SourceEye = new GraphicsEye(), IsFinalPass = true };

        Assert.That(zEye.IsPlayerView(new GraphicsEye()), Is.False);
    }

    [Test]
    public void ZEyeWithoutSourceIsNotPlayerView()
    {
        var zEye = new ScalingViewport.ZEye { IsFinalPass = true };

        Assert.That(zEye.IsPlayerView(new GraphicsEye()), Is.False);
    }
}
