using System.Numerics;
using Content.Client.CMU14.ThreeD;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DZProjectionTest
{
    [Test]
    public void FittedFlightPreservesHeightAcrossMapChangeAndJumpAboveSupport()
    {
        var lower = new CMU3DElevationRamp
        {
            Tile = new Vector2i(0, 0), Direction = new Vector2i(1, 0), Bottom = 1.26f, Top = 2.13f,
            PhysicsCurve = [.1f, .575f, 1.05f, 1.05f],
        };
        var upper = new CMU3DElevationRamp
        {
            Tile = lower.Tile, Direction = lower.Direction, Bottom = lower.Bottom - 3, Top = lower.Top - 3,
            PhysicsCurve = lower.PhysicsCurve, PhysicsOffset = -1, Geometry = false,
        };
        var point = new Vector2(.8f, .5f);
        var before = CMU3DZProjection.SupportedHeight(0, 1.05f, lower, point);
        var after = CMU3DZProjection.SupportedHeight(1, .05f, upper, point);
        Assert.That(after, Is.EqualTo(before).Within(.00001f));
        Assert.That(before, Is.EqualTo(1.956f).Within(.00001f));
        Assert.That(CMU3DZProjection.SupportedHeight(1, .25f, upper, point),
            Is.EqualTo(after + .6f).Within(.00001f), "Jump height must not be flattened onto the ramp.");
    }

    [TestCase(0, 1.02f, 1, .02f)]
    [TestCase(0, -.08f, -1, .92f)]
    public void NormalizingMapCrossingPreservesCameraHeight(int depth, float local, int nextDepth, float nextLocal)
    {
        var before = CMU3DZProjection.Height(depth, local, -.39f, .39f);
        var after = CMU3DZProjection.Height(nextDepth, nextLocal, -.39f, .39f);
        var eye = CMU3DFirstPersonCamera.Frame(Vector2.Zero, 0, 0, new Vector2(800, 600), groundHeight: before);
        var nextEye = CMU3DFirstPersonCamera.Frame(Vector2.Zero, 0, 0, new Vector2(800, 600), groundHeight: after);
        Assert.That(nextEye.Origin.Z, Is.EqualTo(eye.Origin.Z).Within(.00001f));
    }

    [Test]
    public void StairTreadsFaceThePhysicsAscentAndRemainSolid()
    {
        float[] curve = [1.05f, 1.05f, .575f, .1f];
        var south = CMU3DZProjection.StairParts(curve, Direction.South, false, 0, 0);
        var east = CMU3DZProjection.StairParts(curve, Direction.East, false, 0, 0);
        Assert.That(south[^1].Max.Z, Is.GreaterThan(south[0].Max.Z));
        Assert.That(east[0].Max.Z, Is.GreaterThan(east[^1].Max.Z));
        Assert.That(south[^1].Max.Z, Is.EqualTo(3.15f).Within(.00001f));
        foreach (var part in south)
        {
            Assert.That(part.Valid, Is.True);
            Assert.That(part.Min.Z, Is.Zero, "Each tread must have a base and closed sides.");
        }
    }
}
