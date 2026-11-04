using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.CMU14.ThreeD;
using Content.Client.CMU14.ThreeD.Scene;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DFirstPersonExperienceTest
{
    [TestCase(40f)]
    [TestCase(75f)]
    [TestCase(110f)]
    public void VerticalFovMatchesEdgeRaysAndProjection(float degrees)
    {
        var frame = CMU3DFirstPersonCamera.Frame(Vector2.Zero, .7f, .2f, new Vector2(1920, 1080), degrees);
        var ray = frame.RayDirection(new Vector2(960, 0));
        var angle = MathF.Acos(Vector3.Dot(frame.Forward, ray)) * 180 / MathF.PI;
        Assert.That(angle, Is.EqualTo(degrees / 2).Within(.001));
        Assert.That(frame.TryProject(frame.Origin + ray * 20, out var pixel), Is.True);
        Assert.That(Vector2.Distance(pixel, new Vector2(960, 0)), Is.LessThan(.001));
    }

    [TestCase(float.NaN, 75f)]
    [TestCase(float.PositiveInfinity, 75f)]
    [TestCase(-5f, 40f)]
    [TestCase(200f, 110f)]
    public void InvalidFovCannotBreakTheProjection(float value, float expected)
    {
        var view = new Vector2(800, 600);
        var actual = CMU3DFirstPersonCamera.Frame(Vector2.Zero, 0, 0, view, value);
        var reference = CMU3DFirstPersonCamera.Frame(Vector2.Zero, 0, 0, view, expected);
        Assert.That(actual.FocalPixels, Is.EqualTo(reference.FocalPixels));
    }

    [TestCase(-24f)]
    [TestCase(24f)]
    public void ExtendedDistanceRetainsPackedGridMembershipAndPicking(float x)
    {
        var encoding = new CMU3DSceneEncoding(true);
        var target = new EntityUid(7);
        encoding.Build([new CMU3DSceneBox(new Vector3(x, .5f, 1), new Vector3(.2f), 0, Color.White, target)]);
        Assert.That(encoding.OmittedBoxes, Is.Zero);
        Assert.That(encoding.AcceptedBoxes, Is.EqualTo(1));
        var cellX = (int) (x - encoding.SpatialMin);
        var cellY = (int) (.5f - encoding.SpatialMin);
        Assert.That(encoding.Cell(cellX, cellY).ToArray(), Is.EqualTo(new[] { 1 }));
        Assert.That(encoding.TryPick(new Vector3(0, .5f, 1), new Vector3(MathF.Sign(x), 0, 0), out var hit), Is.True);
        Assert.That(hit.Source, Is.EqualTo(target));
        Assert.That(hit.Distance, Is.EqualTo(23.8f).Within(.002));
        Assert.That(encoding.GridPixels.Length, Is.EqualTo(encoding.GridRows * CMU3DSceneEncoding.GridTextureWidth));
    }

    [Test]
    public void ExtendedSceneRetainsMoreThanTheOld8192PartLimit()
    {
        var boxes = new List<CMU3DSceneBox>();
        for (var x = -16; x < 16; x++)
        for (var y = -16; y < 16; y++)
        for (var z = 0; z < 10; z++)
            boxes.Add(new CMU3DSceneBox(new Vector3(x + .5f, y + .5f, .1f + z * .2f), new Vector3(.02f), 0, Color.White));
        var encoding = new CMU3DSceneEncoding(true);
        encoding.Build(boxes);
        Assert.That(encoding.AcceptedBoxes, Is.EqualTo(10240));
        Assert.That(encoding.OmittedBoxes, Is.Zero);
        Assert.That(encoding.BoxRows, Is.EqualTo(3060));
    }

    [TestCase(0f)]
    [TestCase(.73f)]
    public void BillboardTargetingRespectsRotationBoundsAndNearerWalls(float rotation)
    {
        var frame = CMU3DFirstPersonCamera.Frame(Vector2.Zero, rotation, 0, new Vector2(800, 600));
        var center = frame.Origin + frame.Forward * 4;
        Assert.That(CMU3DTargeting.IntersectBillboard(frame.Origin, frame.Forward, center, new Vector2(1, 2), frame.Right, 10, out var distance, out var local), Is.True);
        Assert.That(distance, Is.EqualTo(4).Within(.0001));
        Assert.That(local.Length(), Is.LessThan(.0001));
        Assert.That(CMU3DTargeting.IntersectBillboard(frame.Origin, frame.Forward, center, new Vector2(1, 2), frame.Right, 3, out _, out _), Is.False);
        Assert.That(CMU3DTargeting.IntersectBillboard(frame.Origin + frame.Right, frame.Forward, center, new Vector2(1, 2), frame.Right, 10, out _, out _), Is.False);
        Assert.That(CMU3DTargeting.IntersectBillboard(frame.Origin, -frame.Forward, center, new Vector2(1, 2), frame.Right, 10, out _, out _), Is.False);
    }

    [Test]
    public void CeilingStopsUpwardRayWhileAdjacentOutdoorTileRemainsOpen()
    {
        var scene = new CMU3DSceneEncoding(true);
        scene.Build([new CMU3DSceneBox(new Vector3(.5f, .5f, 2.85f), new Vector3(.5f, .5f, .1f), 0, Color.Gray)]);
        Assert.That(scene.TryPick(new Vector3(.5f, .5f, 1.65f), Vector3.UnitZ, out var ceiling), Is.True);
        Assert.That(ceiling.Position.Z, Is.EqualTo(2.75f).Within(.002));
        Assert.That(scene.TryPick(new Vector3(1.5f, .5f, 1.65f), Vector3.UnitZ, out _), Is.False);
    }
}
