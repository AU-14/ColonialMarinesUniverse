using System;
using System.Numerics;
using Content.Client.CMU14.ThreeD;
using NUnit.Framework;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DCameraTest
{
    [Test]
    public void SpriteFrustumRetainsEdgeAndNearPlaneCrossings()
    {
        var frame = new CMU3DCameraFrame(Vector3.Zero, Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ, 100, new Vector2(100));
        Assert.That(frame.IntersectsSphere(new Vector3(0, -2, 0), .5f), Is.False);
        Assert.That(frame.IntersectsSphere(new Vector3(0, -.1f, 0), .5f), Is.True);
        Assert.That(frame.IntersectsSphere(new Vector3(12, 10, 0), 2), Is.True,
            "A sprite whose center is outside the view can still extend into it.");
        Assert.That(frame.IntersectsSphere(new Vector3(14, 10, 0), 2), Is.False);
        Assert.That(frame.IntersectsSphere(new Vector3(0, 10, 12), 2), Is.True);
        Assert.That(frame.IntersectsSphere(new Vector3(0, 10, 14), 2), Is.False);
    }

    [TestCase(1000, 600)]
    [TestCase(300, 800)]
    [TestCase(800, 300)]
    public void FitKeepsEntireModelOnScreenThroughoutOrbit(int width, int height)
    {
        var min = new Vector3(-2, -0.4f, 0);
        var max = new Vector3(1, 0.7f, 3);
        var size = new Vector2(width, height);
        var camera = new CMU3DCamera();
        camera.Fit(min, max, (float) width / height);
        for (var yaw = -MathF.PI; yaw <= MathF.PI; yaw += MathF.PI / 8)
        for (var elevation = -MathF.PI / 2; elevation <= MathF.PI / 2; elevation += MathF.PI / 8)
        {
            camera.SetAngles(yaw, elevation);
            var frame = camera.Frame(size);
            foreach (var corner in Corners(min, max))
            {
                Assert.That(frame.TryProject(corner, out var pixel), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(pixel.X, Is.InRange(0, width));
                    Assert.That(pixel.Y, Is.InRange(0, height));
                });
            }
        }
    }

    [Test]
    public void ExtremeZoomCannotPassThroughModelOrNearPlane()
    {
        var min = new Vector3(-1, -1, 0);
        var max = new Vector3(1, 1, 2);
        var camera = new CMU3DCamera();
        camera.Fit(min, max, 1);
        camera.Zoom(10000);
        for (var yaw = -MathF.PI; yaw <= MathF.PI; yaw += MathF.PI / 8)
        for (var elevation = -MathF.PI / 2; elevation <= MathF.PI / 2; elevation += MathF.PI / 8)
        {
            camera.SetAngles(yaw, elevation);
            var frame = camera.Frame(new Vector2(600));
            foreach (var corner in Corners(min, max))
            {
                Assert.That(frame.Depth(corner), Is.GreaterThan(CMU3DCamera.NearPlane));
                Assert.That(frame.TryProject(corner, out _), Is.True);
            }
        }
        camera.Zoom(-10000);
        Assert.That(float.IsFinite(camera.Distance), Is.True);
    }

    [TestCase(-1.5707963f, 0.5235988f)]
    [TestCase(0f, 1.5707963f)]
    [TestCase(2.4f, -1.5707963f)]
    public void PickingRayMatchesProjectionIncludingCameraPoles(float yaw, float elevation)
    {
        var camera = new CMU3DCamera();
        camera.Fit(new Vector3(-1, -2, 0), new Vector3(1, 2, 3), 1.5f);
        camera.SetAngles(yaw, elevation);
        var frame = camera.Frame(new Vector2(900, 600));
        var point = new Vector3(0.6f, -0.8f, 2.3f);
        Assert.That(frame.TryProject(point, out var pixel), Is.True);
        var actual = frame.RayDirection(pixel);
        var expected = Vector3.Normalize(point - frame.Origin);
        Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.00001f));
        Assert.That(frame.TryProject(camera.Target, out var center), Is.True);
        Assert.That(Vector2.Distance(center, new Vector2(450, 300)), Is.LessThan(0.001f));
    }

    [Test]
    public void SouthFacingCameraPutsEastOnScreenRightAndHeightAboveCenter()
    {
        var camera = new CMU3DCamera();
        camera.Fit(new Vector3(-1, -1, -1), Vector3.One, 1);
        camera.SetAngles(-MathF.PI / 2, 0);
        var frame = camera.Frame(new Vector2(600));
        Assert.That(frame.TryProject(Vector3.UnitX, out var east), Is.True);
        Assert.That(frame.TryProject(Vector3.UnitZ, out var above), Is.True);
        Assert.That(east.X, Is.GreaterThan(300));
        Assert.That(above.Y, Is.LessThan(300));
        Assert.That(frame.TryProject(frame.Origin - frame.Forward, out _), Is.False);
    }

    private static Vector3[] Corners(Vector3 min, Vector3 max) =>
    [
        min, max, new(min.X, min.Y, max.Z), new(min.X, max.Y, min.Z),
        new(max.X, min.Y, min.Z), new(min.X, max.Y, max.Z),
        new(max.X, min.Y, max.Z), new(max.X, max.Y, min.Z),
    ];
}
