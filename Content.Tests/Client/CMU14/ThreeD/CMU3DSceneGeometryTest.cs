using System;
using System.Linq;
using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DSceneGeometryTest
{
    [Test]
    public void SpatialPickingMatchesExactGeometryAcrossCellsAndFloors()
    {
        var random = new Random(1941);
        float Sample(float min, float max) => min + (max - min) * random.NextSingle();
        var boxes = Enumerable.Range(0, 128).Select(i => new CMU3DSceneBox(
            new Vector3(Sample(-26, 26), Sample(-26, 26), Sample(-8, 12)),
            new Vector3(Sample(.05f, 2), Sample(.05f, 2), Sample(.05f, 2)), Sample(-3, 3), Color.White,
            new EntityUid(i + 1), (CMU3DPartShape) (i % 7)) { Pitch = i % 4 == 0 ? .73f : 0 }).ToList();
        boxes.Add(new CMU3DSceneBox(new Vector3(0, 0, -1), new Vector3(25, 25, .07f), 0, Color.White));
        var encoding = new CMU3DSceneEncoding(true);
        encoding.Build(boxes);
        for (var sample = 0; sample < 300; sample++)
        {
            var target = encoding.Boxes[sample % encoding.Boxes.Count].Center;
            var origin = new Vector3(Sample(-40, 40), Sample(-40, 40), Sample(-15, 20));
            var direction = sample % 3 == 0 ? -Vector3.UnitZ : Vector3.Normalize(target - origin);
            var normalized = Vector3.Normalize(direction); // TryPick normalizes its input once.
            var expected = float.PositiveInfinity;
            foreach (var box in encoding.Boxes)
            {
                if (!CMU3DSceneEncoding.Intersect(box, origin, normalized, out var distance)) continue;
                var point = origin + normalized * distance;
                if (point.X >= -32 && point.X <= 32 && point.Y >= -32 && point.Y <= 32)
                    expected = Math.Min(expected, distance);
            }
            var found = encoding.TryPick(origin, direction, out var hit);
            Assert.That(found, Is.EqualTo(float.IsFinite(expected)), $"Ray {sample}: {origin} / {direction}");
            if (found) Assert.That(hit.Distance, Is.EqualTo(expected).Within(.0001), $"Ray {sample}");
        }
    }

    [TestCase(CMU3DPartShape.CylinderX, 0f)]
    [TestCase(CMU3DPartShape.CylinderY, 0f)]
    [TestCase(CMU3DPartShape.CylinderZ, 0f)]
    [TestCase(CMU3DPartShape.CylinderX, .73f)]
    [TestCase(CMU3DPartShape.CylinderY, .73f)]
    [TestCase(CMU3DPartShape.CylinderZ, .73f)]
    public void CappedCylindersRetainAxisScaleCapsAndEmptyCorners(CMU3DPartShape shape, float yaw)
    {
        var solid = new CMU3DSceneBox(new Vector3(1, -1, 2), new Vector3(2, .5f, 1), yaw, Color.White, Shape: shape);
        var axis = (int) shape - (int) CMU3DPartShape.CylinderX;
        Vector3 Local(int i) => i == 0 ? Vector3.UnitX : i == 1 ? Vector3.UnitY : Vector3.UnitZ;
        Vector3 World(Vector3 p) => new(MathF.Cos(yaw) * p.X - MathF.Sin(yaw) * p.Y,
            MathF.Sin(yaw) * p.X + MathF.Cos(yaw) * p.Y, p.Z);
        var along = World(Local(axis));
        var half = axis == 0 ? 2 : axis == 1 ? .5f : 1;
        foreach (var sign in new[] { -1, 1 })
        {
            Assert.That(CMU3DSceneEncoding.Intersect(solid, solid.Center + along * 5 * sign, -along * sign, out var cap), Is.True);
            Assert.That(cap, Is.EqualTo(5 - half).Within(.00001));
        }
        Assert.That(CMU3DSceneEncoding.Intersect(solid, solid.Center, along, out var exit), Is.True);
        Assert.That(exit, Is.EqualTo(half).Within(.00001));
        var radial1 = Local((axis + 1) % 3);
        var radial2 = Local((axis + 2) % 3);
        var gap = World((radial1 + radial2) * solid.HalfSize * .8f);
        Assert.That(CMU3DSceneEncoding.Intersect(solid, solid.Center + gap - along * 5, along, out _), Is.False);
        var radialExtent = World(radial1 * solid.HalfSize);
        var radialLength = radialExtent.Length();
        var radialDirection = radialExtent / radialLength;
        Assert.That(CMU3DSceneEncoding.Intersect(solid, solid.Center - radialDirection * 5, radialDirection, out var side), Is.True);
        Assert.That(side, Is.EqualTo(5 - radialLength).Within(.00001));
        Assert.That(CMU3DSceneEncoding.Intersect(solid, solid.Center, Vector3.Zero, out _), Is.False);
        var encoding = new CMU3DSceneEncoding();
        encoding.Build([solid]);
        Assert.That(encoding.AcceptedBoxes, Is.EqualTo(1));
        Assert.That(encoding.BoxPixels[5].R, Is.EqualTo((byte) shape));
    }

    [Test]
    public void EllipsoidPickingMissesEmptyBoundsCornersAndRetainsTheBackground()
    {
        var rounded = new CMU3DSceneBox(Vector3.Zero, new Vector3(2, 1, 1), 0, Color.Red,
            new EntityUid(2), CMU3DPartShape.Ellipsoid);
        var background = new CMU3DSceneBox(new Vector3(0, 2, 0), new Vector3(3, .1f, 2), 0, Color.Blue, new EntityUid(1));
        var encoding = new CMU3DSceneEncoding();
        encoding.Build([background, rounded]);
        Assert.That(encoding.TryPick(new Vector3(1.8f, -5, .8f), Vector3.UnitY, out var gap), Is.True);
        Assert.That(gap.Source, Is.EqualTo(background.Source), "The bounding box corner is outside the curved surface.");
        Assert.That(encoding.TryPick(new Vector3(0, -5, 0), Vector3.UnitY, out var front), Is.True);
        Assert.That(front.Source, Is.EqualTo(rounded.Source));
        Assert.That(front.Distance, Is.EqualTo(4).Within(.001));
        Assert.That(encoding.Boxes[1].Shape, Is.EqualTo(CMU3DPartShape.Ellipsoid));
        Assert.That(encoding.BoxPixels[CMU3DSceneEncoding.BoxTexels + 5].R, Is.EqualTo(1));
        Assert.That(encoding.BoxPixels[5].R, Is.Zero);
    }

    [TestCase(0f)]
    [TestCase(.73f)]
    public void EllipsoidPickingHandlesNonuniformScaleRotationAndInteriorRays(float yaw)
    {
        var solid = new CMU3DSceneBox(new Vector3(1, -1, 2), new Vector3(2, .5f, 1), yaw, Color.White,
            Shape: CMU3DPartShape.Ellipsoid);
        var along = new Vector3(MathF.Cos(yaw), MathF.Sin(yaw), 0);
        Assert.That(CMU3DSceneEncoding.Intersect(solid, solid.Center + along * 5, -along, out var front), Is.True);
        Assert.That(front, Is.EqualTo(3).Within(.00001));
        Assert.That(CMU3DSceneEncoding.Intersect(solid, solid.Center, along, out var exit), Is.True);
        Assert.That(exit, Is.EqualTo(2).Within(.00001));
        Assert.That(CMU3DSceneEncoding.Intersect(solid, solid.Center + Vector3.UnitZ * 1.1f, along, out _), Is.False);
    }

    [TestCase(0f)]
    [TestCase(0.736521f)]
    [TestCase(-2.163293f)]
    [TestCase(1.5707963f)]
    public void PackedTextureDecodesToTheGeometryUsedForPicking(float yaw)
    {
        var input = new CMU3DSceneBox(new Vector3(-7.93741f, 6.13721f, -0.04731f),
            new Vector3(0.12917f, 0.77121f, 4.04923f), yaw, new Color(0.2f, 0.4f, 0.8f, 0.6f));
        var encoding = new CMU3DSceneEncoding();
        encoding.Build([input]);
        Assert.That(encoding.AcceptedBoxes, Is.EqualTo(1));
        var cpu = encoding.Boxes[0];
        var a = Pair(encoding.BoxPixels[0]);
        var b = Pair(encoding.BoxPixels[1]);
        var c = Pair(encoding.BoxPixels[2]);
        var rotation = Vector2.Normalize((Pair(encoding.BoxPixels[3]) - new Vector2(32768)) / 32767f);
        var gpuCenter = (new Vector3(a, b.X) - new Vector3(32768)) / 1024f;
        var gpuHalf = new Vector3(b.Y, c.X, c.Y) / 2048f;
        Assert.Multiple(() =>
        {
            Assert.That(cpu.Center, Is.EqualTo(gpuCenter));
            Assert.That(cpu.HalfSize, Is.EqualTo(gpuHalf));
            Assert.That(Vector2.Distance(new Vector2(MathF.Cos(cpu.Yaw), MathF.Sin(cpu.Yaw)), rotation), Is.LessThan(0.0000003f));
            Assert.That(Vector3.Distance(cpu.Center, input.Center), Is.LessThan(0.00085f));
            Assert.That(Vector3.Distance(cpu.HalfSize, input.HalfSize), Is.LessThan(0.00043f));
            Assert.That(encoding.BoxPixels[4], Is.EqualTo(new Rgba32(51, 102, 204, 153)));
        });
    }

    [Test]
    public void PickingReturnsNearestLowDetailInsteadOfTheLargeBackgroundFace()
    {
        var background = new CMU3DSceneBox(new Vector3(0, 0, 1.5f), new Vector3(2, 0.2f, 1.5f), 0, Color.Gray, new EntityUid(1));
        var detail = new CMU3DSceneBox(new Vector3(0, -0.25f, 0.3f), new Vector3(0.3f, 0.1f, 0.2f), 0, Color.Red, new EntityUid(2));
        foreach (var boxes in new[] { new[] { background, detail }, new[] { detail, background } })
        {
            var encoding = new CMU3DSceneEncoding();
            encoding.Build(boxes);
            Assert.That(encoding.TryPick(new Vector3(0, -6, 0.3f), Vector3.UnitY, out var hit), Is.True);
            Assert.That(hit.Source, Is.EqualTo(detail.Source));
            Assert.That(hit.Distance, Is.EqualTo(5.65f).Within(0.001f));
        }
    }

    [Test]
    public void RotatedBoxPickingHandlesParallelRaysAndOriginsInsideTheBox()
    {
        var box = new CMU3DSceneBox(new Vector3(1, -1, 1), new Vector3(0.75f, 0.2f, 0.5f), 0.7f, Color.White);
        var encoding = new CMU3DSceneEncoding();
        encoding.Build([box]);
        var decoded = encoding.Boxes[0];
        var outward = new Vector3(MathF.Cos(decoded.Yaw), MathF.Sin(decoded.Yaw), 0);
        Assert.That(encoding.TryPick(decoded.Center + outward * 4, -outward, out var entry), Is.True);
        Assert.That(entry.Distance, Is.EqualTo(3.25f).Within(0.00001f));
        Assert.That(encoding.TryPick(decoded.Center, outward, out var exit), Is.True);
        Assert.That(exit.Distance, Is.EqualTo(0.75f).Within(0.00001f));
        Assert.That(encoding.TryPick(decoded.Center + outward * 4, Vector3.UnitZ, out _), Is.False);
        Assert.That(encoding.TryPick(decoded.Center, Vector3.Zero, out _), Is.False);

        var aligned = box with { Center = Vector3.Zero, HalfSize = Vector3.One, Yaw = 0 };
        Assert.That(CMU3DSceneEncoding.Intersect(aligned, new Vector3(1, 0, 4), -Vector3.UnitZ, out var boundary), Is.True);
        Assert.That(boundary, Is.EqualTo(3));
    }

    [Test]
    public void SceneCapacityRejectsAnEntireSourceEvenWhenItsPartsAreNotAdjacent()
    {
        var encoding = new CMU3DSceneEncoding();
        var busy = new CMU3DSceneBox(new Vector3(0.5f, 0.5f, 1), new Vector3(0.1f), 0, Color.White);
        var empty = busy with { Center = new Vector3(3.5f, 3.5f, 1) };
        var source = new EntityUid(42);
        var input = Enumerable.Repeat(busy, CMU3DSceneEncoding.MaxBoxes - 1).ToList();
        input.Add(busy with { Source = source });
        input.Add(empty);
        input.Add(busy with { Source = source });
        input.Add(empty with { Source = source });
        encoding.Build(input);
        Assert.Multiple(() =>
        {
            Assert.That(encoding.AcceptedBoxes, Is.EqualTo(CMU3DSceneEncoding.MaxBoxes));
            Assert.That(encoding.OmittedBoxes, Is.EqualTo(3));
            Assert.That(encoding.Boxes.All(b => b.Source != source), Is.True);
            Assert.That(encoding.Cell(8, 8).Length, Is.EqualTo(CMU3DSceneEncoding.MaxBoxes - 1));
            Assert.That(encoding.Cell(11, 11).Length, Is.EqualTo(1));

        });
        var cellId = encoding.Cell(11, 11).Span[0];
        Assert.That(encoding.Boxes[cellId - 1].Center, Is.EqualTo(empty.Center));
        Assert.That(cellId, Is.EqualTo(CMU3DSceneEncoding.MaxBoxes));
    }

    [Test]
    public void InvalidPartRejectsItsWholeSourceWithoutPoisoningOtherGeometry()
    {
        var source = new EntityUid(7);
        var valid = new CMU3DSceneBox(Vector3.Zero, Vector3.One, 0, Color.White, source);
        var encoding = new CMU3DSceneEncoding();
        encoding.Build([valid, valid with { Source = null, Center = new Vector3(4, 0, 0) },
            valid with { HalfSize = new Vector3(float.NaN) }]);
        Assert.That(encoding.AcceptedBoxes, Is.EqualTo(1));
        Assert.That(encoding.OmittedBoxes, Is.EqualTo(2));
        Assert.That(encoding.TryPick(new Vector3(0, 0, 5), -Vector3.UnitZ, out _), Is.False);
        Assert.That(encoding.TryPick(new Vector3(4, 0, 5), -Vector3.UnitZ, out _), Is.True);
    }

    [Test]
    public void RotatedFootprintAndSharedBoundariesRemainInEveryCoveredCell()
    {
        var encoding = new CMU3DSceneEncoding();
        var box = new CMU3DSceneBox(Vector3.Zero, new Vector3(1.3f, 0.2f, 0.5f), MathF.PI / 4, Color.White);
        encoding.Build([box]);
        var decoded = encoding.Boxes[0];
        for (var x = -1; x <= 1; x += 2)
        for (var y = -1; y <= 1; y += 2)
        {
            var local = new Vector2(x * decoded.HalfSize.X, y * decoded.HalfSize.Y);
            var world = new Vector2(MathF.Cos(decoded.Yaw) * local.X - MathF.Sin(decoded.Yaw) * local.Y,
                MathF.Sin(decoded.Yaw) * local.X + MathF.Cos(decoded.Yaw) * local.Y);
            Assert.That(encoding.Cell((int) MathF.Floor(world.X + 8), (int) MathF.Floor(world.Y + 8)).ToArray(), Contains.Item(1));
        }
        encoding.Build([box with { Center = new Vector3(0.5f, 0.5f, 0), HalfSize = new Vector3(0.5f), Yaw = 0 }]);
        Assert.That(encoding.Cell(7, 8).ToArray(), Contains.Item(1), "A parallel ray on the exact shared face must still find the box.");
        Assert.That(encoding.Cell(9, 8).ToArray(), Contains.Item(1));
    }

    [Test]
    public void OversizedSnapshotsAndClearingRemoveAllOldGridReferences()
    {
        var encoding = new CMU3DSceneEncoding();
        var box = new CMU3DSceneBox(Vector3.Zero, Vector3.One, 0, Color.White);
        encoding.Build([box]);
        encoding.Build(new CMU3DSceneBox[CMU3DSceneEncoding.MaxSnapshotBoxes + 1]);
        Assert.That(encoding.AcceptedBoxes, Is.Zero);
        Assert.That(encoding.OmittedBoxes, Is.EqualTo(CMU3DSceneEncoding.MaxSnapshotBoxes + 1));
        Assert.That(encoding.Cell(8, 8).Length, Is.Zero);
        Assert.That(encoding.GridPixels.All(pixel => pixel == default(Rgba32)), Is.True);
        Assert.That(encoding.TryPick(new Vector3(0, 0, 5), -Vector3.UnitZ, out _), Is.False);
        encoding.Build([box]);
        Assert.That(encoding.OmittedBoxes, Is.Zero);
        Assert.That(encoding.TryPick(new Vector3(0, 0, 5), -Vector3.UnitZ, out _), Is.True);
        encoding.Build(Array.Empty<CMU3DSceneBox>());
        Assert.That(encoding.Cell(8, 8).Length, Is.Zero);
    }

    private static Vector2 Pair(Rgba32 pixel) => new(pixel.R + pixel.G * 256, pixel.B + pixel.A * 256);
}
