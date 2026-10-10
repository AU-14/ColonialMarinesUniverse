using System;
using System.Numerics;
using Content.Client.CMU14.ThreeD;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DEquipmentTransformTest
{
    [Test]
    public void HeldGripFollowsLocalLookWithoutAReplicatedActorUpdate()
    {
        var pose = new CMU3DEquipmentPosePrototype { Pivot = new(-.1f, 0, .2f), Yaw = -90 };
        var before = CMU3DFirstPersonCamera.Frame(Vector2.Zero, 0, 0, new Vector2(800, 600));
        var after = CMU3DFirstPersonCamera.Frame(Vector2.Zero, .9f, .3f, new Vector2(800, 600));
        var first = CMU3DEquipmentTransform.Create(pose, Vector3.Zero, 0, before);
        var next = CMU3DEquipmentTransform.Create(pose, Vector3.Zero, 0, after);
        var expected = after.Origin + after.Right * pose.FirstPersonOffset.X +
                       after.Forward * pose.FirstPersonOffset.Y + after.Up * pose.FirstPersonOffset.Z;
        Assert.That(Vector3.Distance(next.Point(pose.Pivot), expected), Is.LessThan(1e-5));
        Assert.That(Vector3.Dot(Vector3.Normalize(next.X), after.Forward), Is.EqualTo(1).Within(1e-5));
        Assert.That(Vector3.Distance(first.Point(pose.Pivot), next.Point(pose.Pivot)), Is.GreaterThan(.1f));
        Assert.That(before.TryProject(first.Point(pose.Pivot), out var a), Is.True);
        Assert.That(after.TryProject(next.Point(pose.Pivot), out var b), Is.True);
        Assert.That(Vector2.Distance(a, b), Is.LessThan(.001f));
    }

    [Test]
    public void SwitchingHandsMovesTheGripWithoutMirroringAnAsymmetricModel()
    {
        var pose = new CMU3DEquipmentPosePrototype { Pivot = new(-.2f, .03f, .1f), Offset = new(-.27f, -.15f, 1) };
        var position = new Vector3(3, -4, 2.8f);
        var right = CMU3DEquipmentTransform.Create(pose, position, MathF.PI / 2);
        var left = CMU3DEquipmentTransform.Create(pose, position, MathF.PI / 2, leftHand: true);
        Assert.That(Vector3.Distance(right.Point(pose.Pivot), new Vector3(3.15f, -4.27f, 3.8f)), Is.LessThan(1e-5));
        Assert.That(Vector3.Distance(left.Point(pose.Pivot), new Vector3(3.15f, -3.73f, 3.8f)), Is.LessThan(1e-5));
        var sideDetail = new Vector3(.1f, .08f, .2f);
        Assert.That(Vector3.Distance(right.Direction(sideDetail), left.Direction(sideDetail)), Is.LessThan(1e-5));
        Assert.That(Vector3.Dot(Vector3.Cross(left.X, left.Y), left.Z), Is.GreaterThan(0));
    }

    [Test]
    public void AttachmentRayKeepsWorldDistanceAcrossRotationScaleAndStairHeight()
    {
        var pose = new CMU3DEquipmentPosePrototype { Pivot = new(.1f, 0, .1f), Yaw = -90, Pitch = 13, Roll = 90, Scale = .7f };
        var transform = CMU3DEquipmentTransform.Create(pose, new Vector3(4, -2, 5.6f), .37f).Quantized();
        var box = new CMU3DSceneBox(Vector3.Zero, new Vector3(.2f), 0, Color.White);
        var localOrigin = new Vector3(0, 0, 2);
        var origin = transform.Point(localOrigin);
        var ray = Vector3.Normalize(transform.Direction(-Vector3.UnitZ));
        Assert.That(CMU3DSceneEncoding.Intersect(box, transform.InversePoint(origin), transform.InverseDirection(ray), out var distance), Is.True);
        var hit = origin + ray * distance;
        Assert.That(Vector3.Distance(hit, transform.Point(new Vector3(0, 0, .2f))), Is.LessThan(.002f));
        Assert.That(distance, Is.EqualTo(1.8f * transform.Z.Length()).Within(.002f));
    }
}
