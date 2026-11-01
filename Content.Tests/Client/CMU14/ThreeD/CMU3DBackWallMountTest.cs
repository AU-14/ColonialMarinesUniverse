using System;
using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DBackWallMountTest
{
    [TestCase(.5f,.02f,4)] [TestCase(-.5f,0,8)] [TestCase(0,.5f,1)]
    [TestCase(.02f,-.5f,2)] [TestCase(.3f,.3f,0)] [TestCase(.1f,0,0)]
    public void OffsetBasinSelectsOnlyAnUnambiguousGridSide(float x, float y, int expected)
    {
        foreach (var grid in new[] { 0f, .7f })
        {
            var c = MathF.Cos(grid);
            var s = MathF.Sin(grid);
            var delta = new Vector2(c * x - s * y, s * x + c * y);
            Assert.That(CMU3DSceneLayout.OffsetTargetMask(delta, grid), Is.EqualTo(expected));
        }
    }

    private static readonly CMU3DModelPart[] Basin =
    [new() { Min = new Vector3(-.3f,-.4f,.65f), Max = new Vector3(.3f,0,.85f) }];
    private static readonly CMU3DModelPart[] Wall =
    [new() { Min = new Vector3(-.5f,-.55f,0), Max = new Vector3(.5f,.55f,2.8f) }];

    [TestCase(0, .058f)] [TestCase(1, .008f)] [TestCase(2, .058f)] [TestCase(3, .008f)]
    public void WallFixtureClearsTrimAfterPivotNormalization(int turn, float expected)
    {
        CMU3DModelPart[] mirror = [new() { Min = new Vector3(-.2f,-.556f,1.2f), Max = new Vector3(.2f,-.502f,1.8f) }];
        var yaw = turn * MathF.PI / 2;
        var position = new Vector2(.62f, .38f);
        var center = new Vector2(.5f, .5f);
        var mount = CMU3DSceneLayout.WallMountOffset(position, center, yaw);
        Assert.That(CMU3DSceneLayout.TryBackWallMountOffset(mirror, yaw, Wall, 0, center - position - mount, out var clearance), Is.True);
        var front = new Vector2(MathF.Sin(yaw), -MathF.Cos(yaw));
        var right = new Vector2(MathF.Cos(yaw), MathF.Sin(yaw));
        var final = position + mount + clearance - center;
        Assert.That(Vector2.Dot(final, front), Is.EqualTo(expected).Within(.00001f));
        Assert.That(Vector2.Dot(final, right), Is.EqualTo(Vector2.Dot(position - center, right)).Within(.00001f));
    }

    [TestCase(0, 0)] [TestCase(1, 0)] [TestCase(2, 0)] [TestCase(3, 0)]
    [TestCase(0, .7f)] [TestCase(1, .7f)] [TestCase(2, .7f)] [TestCase(3, .7f)]
    public void RearFaceClearsWithSavedFacingAndSpacing(int turn, float gridYaw)
    {
        var yaw = gridYaw + turn * MathF.PI / 2;
        var front = new Vector2(MathF.Sin(yaw), -MathF.Cos(yaw));
        var right = new Vector2(MathF.Cos(yaw), MathF.Sin(yaw));
        Assert.That(CMU3DSceneLayout.TryBackWallMountOffset(Basin, yaw, Wall, yaw, -.15f * front + .08f * right, out var offset), Is.True);
        Assert.That(Vector2.Dot(offset, front) - (.55f - .15f), Is.EqualTo(.01f).Within(.00001f));
        Assert.That(Vector2.Dot(offset, right), Is.EqualTo(0).Within(.00001f));
        Assert.That(Wall[0].Min.Y, Is.EqualTo(-.55f));
    }

    [TestCase(0, .8f, 0)] [TestCase(1, .15f, 0)] [TestCase(0, -.2f, 0)]
    [TestCase(0, .15f, .2f)] [TestCase(0, 2, 0)]
    public void ClearSideFrontAndUncertainTargetsDoNotMove(float x, float y, float angle)
    {
        Assert.That(CMU3DSceneLayout.TryBackWallMountOffset(Basin, 0, Wall, angle, new Vector2(x,y), out var offset), Is.False);
        Assert.That(offset, Is.EqualTo(Vector2.Zero));
    }

    [TestCase(0, .41f)] [TestCase(1, .36f)] [TestCase(2, .41f)] [TestCase(3, .36f)]
    public void WallAxisIsIndependentOfFixtureFacing(int turn, float expected)
    {
        var yaw = turn * MathF.PI / 2;
        var front = new Vector2(MathF.Sin(yaw), -MathF.Cos(yaw));
        Assert.That(CMU3DSceneLayout.TryBackWallMountOffset(Basin, yaw, Wall, 0, -.15f * front, out var offset), Is.True);
        Assert.That(Vector2.Dot(offset, front), Is.EqualTo(expected).Within(.00001f));
    }

    [Test]
    public void OverheadAndFootingPartsDoNotExpandTheMountPlane()
    {
        CMU3DModelPart[] parts = [Wall[0],
            new() { Min = new Vector3(-.5f,-.9f,1.1f), Max = new Vector3(.5f,.5f,2.8f) },
            new() { Min = new Vector3(-.5f,-.9f,0), Max = new Vector3(.5f,.5f,.6f) },
            new() { Min = new Vector3(-.5f,-.9f,0), Max = new Vector3(.5f,.5f,2.8f), Shape = CMU3DPartShape.Ellipsoid }];
        Assert.That(CMU3DSceneLayout.TryBackWallMountOffset(Basin, 0, parts, 0, new Vector2(0,.15f), out var offset), Is.True);
        Assert.That(offset.Y, Is.EqualTo(-.41f).Within(.00001f));
        Assert.That(CMU3DSceneLayout.TryBackWallMountOffset([], 0, parts, 0, new Vector2(0,.15f), out _), Is.False);
    }
}
