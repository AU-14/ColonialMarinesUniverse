using System;
using System.Numerics;
using Content.Client.CMU14.ThreeD;
using NUnit.Framework;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DFirstPersonCameraTest
{
    [TestCase(0f)]
    [TestCase(1.5707963f)]
    [TestCase(-1.5707963f)]
    [TestCase(3.1415927f)]
    [TestCase(0.73f)]
    public void ForwardAndStrafeMatchExistingMovementRotation(float rotation)
    {
        var frame = CMU3DFirstPersonCamera.Frame(Vector2.Zero, rotation, 0, new Vector2(1200, 800));
        var expectedForward = new Vector3(-MathF.Sin(rotation), MathF.Cos(rotation), 0);
        var expectedRight = new Vector3(MathF.Cos(rotation), MathF.Sin(rotation), 0);
        Assert.That(Vector3.Distance(frame.Forward, expectedForward), Is.LessThan(1e-5));
        Assert.That(Vector3.Distance(frame.Right, expectedRight), Is.LessThan(1e-5));
        Assert.That(Vector3.Distance(frame.Up, Vector3.UnitZ), Is.LessThan(1e-5));
        Assert.That(Vector3.Distance(frame.RayDirection(frame.ScreenCenter), expectedForward), Is.LessThan(1e-5));
    }

    [TestCase(-100f)]
    [TestCase(0f)]
    [TestCase(100f)]
    public void PitchCannotFlipCameraAndProjectionMatchesRay(float pitch)
    {
        var camera = CMU3DFirstPersonCamera.Frame(new Vector2(.14f, -.23f), .71f, pitch, new Vector2(900, 500));
        Assert.That(camera.Up.Z, Is.GreaterThan(0));
        var pixel = new Vector2(350, 200);
        var world = camera.Origin + camera.RayDirection(pixel) * 3;
        Assert.That(camera.TryProject(world, out var projected), Is.True);
        Assert.That(Vector2.Distance(projected, pixel), Is.LessThan(.001f));
    }

    [Test]
    public void SamplingNewSceneOriginDoesNotMoveTheWorldRelativeToCamera()
    {
        var actor = new Vector2(17.26f, -38.92f);
        var oldSample = new Vector2(17.1f, -39);
        var viewport = new Vector2(800, 600);
        var before = CMU3DFirstPersonCamera.Frame(actor - oldSample, 0, 0, viewport);
        var after = CMU3DFirstPersonCamera.Frame(Vector2.Zero, 0, 0, viewport);
        var worldPoint = new Vector3(17.4f, -36, 1.1f);
        Assert.That(before.TryProject(worldPoint - new Vector3(oldSample, 0), out var a), Is.True);
        Assert.That(after.TryProject(worldPoint - new Vector3(actor, 0), out var b), Is.True);
        Assert.That(Vector2.Distance(a, b), Is.LessThan(.002f));
        Assert.That(before.Origin.Z, Is.EqualTo(CMU3DFirstPersonCamera.EyeHeight));
    }
}
