using System;
using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DPrisonWindowMountTest
{
    private const string ShutterId = "CMU3DHybrisaWindowShutter";
    private static CMU3DModelPart[] Window() =>
    [
        new() { Label = "facade", Min = new(-.5f,-.525f,0), Max = new(.5f,.525f,.59f), Color = Color.Gray },
        new() { Label = "glazing", Min = new(-.38f,-.045f,.61f), Max = new(-.15f,.045f,2.24f), Color = new Color(.4f,.7f,.8f,.6f) },
        new() { Label = "header", Min = new(-.5f,-.525f,2.24f), Max = new(.5f,.525f,2.78f), Color = Color.Gray },
    ];

    [TestCase(0, 0)] [TestCase(1, 0)] [TestCase(2, 0)] [TestCase(3, 0)]
    [TestCase(0, .7f)] [TestCase(1, .7f)] [TestCase(2, .7f)] [TestCase(3, .7f)]
    public void BothAxesAndSidesKeepTheFacadeProfileAndBackingGap(int turn, float gridYaw)
    {
        var axis = turn % 2 == 0 ? 1 : 0;
        var source = CMU3DSceneLayout.ConnectedParts(Window(), turn % 2 == 0 ? 4 : 1);
        var yaw = gridYaw + turn * MathF.PI / 2;
        var side = CMU3DSceneLayout.PrisonShutterSide("RMCShutterHybrisaWindow", ShutterId, axis, yaw, gridYaw, Vector2.Zero);
        Assert.That(side, Is.EqualTo(turn is 0 or 3 ? 1 : 2));
        Assert.That(CMU3DSceneLayout.TryPrisonRecessParts(source, axis, side, out var recessed), Is.True);
        CMU3DModelPart[] shutter = [new() { Min = new(-.5f,-.0625f,0), Max = new(.5f,.0625f,2.75f) }];
        Assert.That(CMU3DSceneLayout.TryWindowMountOffset(shutter, yaw, recessed, gridYaw,
            Vector2.Zero, out var offset, exteriorAxis: axis), Is.True);
        var distance = Vector2.Dot(offset, new Vector2(MathF.Sin(yaw), -MathF.Cos(yaw)));
        Assert.That(distance, Is.EqualTo(.3875f).Within(.000001f));
        Assert.That(distance - .0625f - .305f, Is.EqualTo(.02f).Within(.000001f));
        for (var i = 0; i < source.Length; i++)
        {
            var original = source[i];
            var candidate = recessed[i];
            Assert.That(candidate, Is.Not.SameAs(original));
            Assert.That(candidate.Color, Is.EqualTo(original.Color));
            Assert.That(candidate.Label, Is.EqualTo(original.Label));
            Assert.That(candidate.Min.Z, Is.EqualTo(original.Min.Z));
            Assert.That(candidate.Max.Z, Is.EqualTo(original.Max.Z));
            Assert.That(axis == 0 ? candidate.Min.Y : candidate.Min.X, Is.EqualTo(axis == 0 ? original.Min.Y : original.Min.X));
            Assert.That(axis == 0 ? candidate.Max.Y : candidate.Max.X, Is.EqualTo(axis == 0 ? original.Max.Y : original.Max.X));
            if (original.Color.A < 1)
            {
                Assert.That(candidate.Min, Is.EqualTo(original.Min));
                Assert.That(candidate.Max, Is.EqualTo(original.Max));
            }
        }
        Assert.That(axis == 0 ? source[0].Min.X : source[0].Min.Y, Is.EqualTo(-.525f));
    }

    [Test]
    public void OppositeSidesAreCommutativeAndARecomputedMissingSideRestoresTheOriginalFacade()
    {
        var source = Window();
        var south = CMU3DSceneLayout.PrisonShutterSide("RMCShutterHybrisaWindow", ShutterId, 1, 0, 0, Vector2.Zero);
        var north = CMU3DSceneLayout.PrisonShutterSide("RMCShutterHybrisaWindowOpen", ShutterId + "Open", 1, MathF.PI, 0, Vector2.Zero);
        Assert.That(south | north, Is.EqualTo(north | south).And.EqualTo(3));
        Assert.That(CMU3DSceneLayout.TryPrisonRecessParts(source, 1, south | north, out var both), Is.True);
        Assert.That(both[0].Min.Y, Is.EqualTo(-.305f).Within(.000001f));
        Assert.That(both[0].Max.Y, Is.EqualTo(.305f).Within(.000001f));
        Assert.That(CMU3DSceneLayout.TryPrisonRecessParts(source, 1, south, out var remaining), Is.True);
        Assert.That(remaining[0].Min.Y, Is.EqualTo(-.305f).Within(.000001f));
        Assert.That(remaining[0].Max.Y, Is.EqualTo(.525f));
        Assert.That(CMU3DSceneLayout.TryPrisonRecessParts(source, 1, 0, out _), Is.False);
        Assert.That(source[0].Min.Y, Is.EqualTo(-.525f));
        Assert.That(source[0].Max.Y, Is.EqualTo(.525f));
        Assert.That(both[0].Max.Y, Is.EqualTo(.305f).Within(.000001f));
    }

    [Test]
    public void UnsupportedMountingAndFutureGeometryDoNotAcquireAGuessedRecess()
    {
        foreach (var mask in new[] { 0, 5, 15 })
            Assert.That(CMU3DSceneLayout.PrisonShutterSide("RMCShutterHybrisaWindow", ShutterId,
                CMU3DSceneLayout.ShutterExteriorAxis("RMCWindowPrisonCell", mask), 0, 0, Vector2.Zero), Is.Zero);
        Assert.That(CMU3DSceneLayout.PrisonShutterSide("Other", ShutterId, 1, 0, 0, Vector2.Zero), Is.Zero);
        Assert.That(CMU3DSceneLayout.PrisonShutterSide("RMCShutterHybrisaWindow", "Other", 1, 0, 0, Vector2.Zero), Is.Zero);
        Assert.That(CMU3DSceneLayout.PrisonShutterSide("RMCShutterHybrisaWindow", ShutterId, 1, MathF.PI / 2, 0, Vector2.Zero), Is.Zero);
        Assert.That(CMU3DSceneLayout.PrisonShutterSide("RMCShutterHybrisaWindow", ShutterId, 1, 0, 0, new Vector2(.01f,0)), Is.Zero);
        Assert.That(CMU3DSceneLayout.PrisonShutterSide("RMCShutterHybrisaWindow", ShutterId, 1, 0, 0, Vector2.Zero, inside: true), Is.Zero);
        Assert.That(CMU3DSceneLayout.PrisonShutterSide("RMCShutterHybrisaWindow", ShutterId, 1, float.NaN, 0, Vector2.Zero), Is.Zero);
        var source = Window();
        source[0].Yaw = 90;
        Assert.That(CMU3DSceneLayout.TryPrisonRecessParts(source, 1, 1, out _), Is.False);
        source[0].Yaw = 0;
        source[0].Shape = CMU3DPartShape.CylinderZ;
        Assert.That(CMU3DSceneLayout.TryPrisonRecessParts(source, 1, 1, out _), Is.False);
        source[0].Shape = CMU3DPartShape.Box;
        source[1].Min.Y = -.08f;
        Assert.That(CMU3DSceneLayout.TryPrisonRecessParts(source, 1, 1, out _), Is.False);
    }

    private static CMU3DModelPart[] Wall() =>
    [
        new() { Label = "full tile wall core", Min = new(-.5f,-.5f,0), Max = new(.5f,.5f,2.8f) },
        new() { Label = "west relief", Min = new(-.54f,-.3f,.2f), Max = new(-.505f,.3f,2.5f) },
        new() { Label = "east relief", Min = new(.505f,-.3f,.2f), Max = new(.54f,.3f,2.5f) },
        new() { Label = "south relief", Min = new(-.3f,-.54f,.2f), Max = new(.3f,-.505f,2.5f) },
        new() { Label = "north relief", Min = new(-.3f,.505f,.2f), Max = new(.3f,.54f,2.5f) },
        new() { Label = "spanning course", Min = new(-.5f,-.52f,2.56f), Max = new(.5f,.52f,2.77f) },
    ];

    [TestCase(0, 0)] [TestCase(1, 0)] [TestCase(2, 0)] [TestCase(3, 0)]
    [TestCase(0, .7f)] [TestCase(1, .7f)] [TestCase(2, .7f)] [TestCase(3, .7f)]
    public void JoiningFaceUsesRelativeRotationAndRetainsTheCoreAndOtherRelief(int turn, float gridYaw)
    {
        var yaw = gridYaw + turn * MathF.PI / 2;
        var delta = new Vector2(-MathF.Cos(gridYaw), -MathF.Sin(gridYaw));
        var face = CMU3DSceneLayout.PrisonWallJoinFace(delta, yaw, gridYaw, 12);
        Assert.That(face, Is.EqualTo(new[] { 1, 8, 2, 4 }[turn]));
        var source = Wall();
        Assert.That(CMU3DSceneLayout.TryPrisonJoinedReliefParts(source, face, out var joined), Is.True);
        Assert.That(joined[0].Min, Is.EqualTo(source[0].Min));
        Assert.That(joined[0].Max, Is.EqualTo(source[0].Max));
        var bit = face == 1 ? 0 : face == 2 ? 1 : face == 4 ? 2 : 3;
        for (var i = 1; i <= 4; i++)
        {
            Assert.That(joined[i].Label, Is.EqualTo(source[i].Label));
            Assert.That(joined[i].Color, Is.EqualTo(source[i].Color));
            Assert.That(Vector3.Distance(joined[i].Max - joined[i].Min, source[i].Max - source[i].Min), Is.LessThan(.000001f));
            if (i != bit + 1)
            {
                Assert.That(joined[i].Min, Is.EqualTo(source[i].Min));
                Assert.That(joined[i].Max, Is.EqualTo(source[i].Max));
            }
            else
            {
                var outside = bit < 2 ? (bit == 0 ? -joined[i].Min.X : joined[i].Max.X) :
                    (bit == 2 ? -joined[i].Min.Y : joined[i].Max.Y);
                Assert.That(.5f - outside, Is.EqualTo(.01f).Within(.000001f));
            }
        }
        Assert.That(source[1].Min.X, Is.EqualTo(-.54f));
    }

    [Test]
    public void MultipleJoinsAreIndependentAndUnsupportedInputsRetainOriginalGeometry()
    {
        var source = Wall();
        Assert.That(CMU3DSceneLayout.TryPrisonJoinedReliefParts(source, 15, out var all), Is.True);
        Assert.That(CMU3DSceneLayout.TryPrisonJoinedReliefParts(source, 1, out var remaining), Is.True);
        Assert.That(remaining[2].Max, Is.EqualTo(source[2].Max));
        Assert.That(all[2].Max.X, Is.EqualTo(.49f));
        Assert.That(CMU3DSceneLayout.TryPrisonJoinedReliefParts(source, 0, out _), Is.False);
        Assert.That(CMU3DSceneLayout.PrisonWallJoinFace(new Vector2(1.01f,0), 0, 0, 12), Is.Zero);
        Assert.That(CMU3DSceneLayout.PrisonWallJoinFace(new Vector2(0,1), 0, 0, 12), Is.Zero);
        Assert.That(CMU3DSceneLayout.PrisonWallJoinFace(new Vector2(1,0), .2f, 0, 12), Is.Zero);
        Assert.That(CMU3DSceneLayout.PrisonWallJoinFace(new Vector2(1,0), 0, 0, 5), Is.Zero);
        source[1].Shape = CMU3DPartShape.CylinderZ;
        Assert.That(CMU3DSceneLayout.TryPrisonJoinedReliefParts(source, 1, out _), Is.False);
    }
}
