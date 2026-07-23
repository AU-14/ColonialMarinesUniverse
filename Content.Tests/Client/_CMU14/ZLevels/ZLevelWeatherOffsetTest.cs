using System.Numerics;
using Content.Client.Overlays;
using Content.Client.Viewport;
using NUnit.Framework;
using Robust.Shared.Graphics;
using Robust.Shared.Maths;

namespace Content.Tests.Client._CMU14.ZLevels;

[TestFixture]
public sealed class ZLevelWeatherOffsetTest
{
    [Test]
    public void OrdinaryPassHasNoWeatherTileOffset()
    {
        var eye = new Eye
        {
            Rotation = Angle.FromDegrees(90),
        };

        Assert.That(StencilOverlay.GetWeatherTileOffset(eye), Is.EqualTo(Vector2.Zero));
    }

    [Test]
    public void ZPassUsesItsSignedVisualOffset()
    {
        var lowerEye = new ScalingViewport.ZEye
        {
            VisualZOffset = new Vector2(-0.5f, 0f),
        };
        var upperEye = new ScalingViewport.ZEye
        {
            VisualZOffset = new Vector2(0.5f, 0f),
        };

        Assert.That(StencilOverlay.GetWeatherTileOffset(lowerEye), Is.EqualTo(lowerEye.VisualZOffset));
        Assert.That(StencilOverlay.GetWeatherTileOffset(upperEye), Is.EqualTo(upperEye.VisualZOffset));
    }
}
