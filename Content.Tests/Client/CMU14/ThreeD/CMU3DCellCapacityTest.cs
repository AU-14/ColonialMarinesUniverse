using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DCellCapacityTest
{
    [TestCase(false, 191)]
    [TestCase(false, 192)]
    [TestCase(false, 193)]
    [TestCase(false, 255)]
    [TestCase(false, 256)]
    [TestCase(false, 329)]
    [TestCase(false, 384)]
    [TestCase(false, 385)]
    [TestCase(false, 1025)]
    [TestCase(false, 8192)]
    [TestCase(true, 193)]
    [TestCase(true, 385)]
    [TestCase(true, 65536)]
    [TestCase(true, CMU3DSceneEncoding.ExtendedMaxBoxes)]
    public void LastCellCandidateRemainsPackedAndNearestPickable(bool extended, int count)
    {
        var encoding = new CMU3DSceneEncoding(extended);
        var source = new EntityUid(501);
        var boxes = Background(count - 1);
        boxes.Add(Panel(.25f, Color.Red, source));
        encoding.Build(boxes);
        Assert.Multiple(() =>
        {
            Assert.That(encoding.AcceptedBoxes, Is.EqualTo(count));
            Assert.That(encoding.OmittedBoxes, Is.Zero);
            Assert.That(encoding.ReferenceBudgetExceeded, Is.False);
            AssertPackedCells(encoding);
            var index = (int) -encoding.SpatialMin;
            Assert.That(encoding.Cell(index, index).ToArray(), Is.EqualTo(Enumerable.Range(1, count)));
            Assert.That(encoding.TryPick(new Vector3(.5f, -2, 1.3f), Vector3.UnitY, out var hit), Is.True);
            Assert.That(hit.Source, Is.EqualTo(source));
            Assert.That(hit.BoxIndex, Is.EqualTo(count - 1));
            Assert.That(hit.Distance, Is.EqualTo(2.19f).Within(.002f));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DenseEarlierSourceCannotDisplaceLaterSources(bool extended)
    {
        var early = new EntityUid(601);
        var later = new EntityUid(602);
        var backfill = new EntityUid(603);
        var cellA = Panel(.75f, Color.Blue);
        var cellB = Panel(.5f, Color.Green, later) with { Center = new Vector3(2.5f, .5f, 1.3f) };
        var boxes = Enumerable.Repeat(cellA, 100).ToList();
        boxes.AddRange(Enumerable.Repeat(Panel(.25f, Color.Red, early), 33));
        boxes.AddRange(Enumerable.Repeat(cellB with { Color = Color.Red, Source = early }, 95));
        boxes.AddRange(Enumerable.Repeat(cellB, 100));
        boxes.AddRange(Enumerable.Repeat(Panel(.5f, Color.Green, backfill), 40));
        var encoding = new CMU3DSceneEncoding(extended);
        encoding.Build(boxes);
        var index = (int) -encoding.SpatialMin;
        Assert.Multiple(() =>
        {
            Assert.That(encoding.AcceptedBoxes, Is.EqualTo(368));
            Assert.That(encoding.OmittedBoxes, Is.Zero);
            Assert.That(encoding.Boxes.Count(box => box.Source == early), Is.EqualTo(128));
            Assert.That(encoding.Boxes.Count(box => box.Source == later), Is.EqualTo(100));
            Assert.That(encoding.Boxes.Count(box => box.Source == backfill), Is.EqualTo(40));
            Assert.That(encoding.Cell(index, index).ToArray(),
                Is.EqualTo(Enumerable.Range(1, 133).Concat(Enumerable.Range(329, 40))));
            Assert.That(encoding.Cell(index + 2, index).ToArray(), Is.EqualTo(Enumerable.Range(134, 195)));
            AssertPackedCells(encoding);
            Assert.That(encoding.TryPick(new Vector3(2.5f, -2, 1.3f), Vector3.UnitY, out var hit), Is.True);
            Assert.That(hit.Source, Is.EqualTo(later));
            Assert.That(hit.BoxIndex, Is.EqualTo(327));
        });
    }

    [Test]
    public void ReferenceMemoryLimitRejectsSnapshotAndNextBuildRecovers()
    {
        var encoding = new CMU3DSceneEncoding(true);
        var covering = new CMU3DSceneBox(Vector3.Zero, new Vector3(31, 31, 1), 0, Color.White);
        encoding.Build(Enumerable.Repeat(covering, 1025).ToArray());
        Assert.Multiple(() =>
        {
            Assert.That(encoding.ReferenceBudgetExceeded, Is.True);
            Assert.That(encoding.AcceptedBoxes, Is.Zero);
            Assert.That(encoding.OmittedBoxes, Is.EqualTo(1025));
            Assert.That(encoding.GridReferences, Is.Zero);
            AssertPackedCells(encoding);
            Assert.That(encoding.TryPick(new Vector3(0, 0, 4), -Vector3.UnitZ, out _), Is.False);
        });
        encoding.Build([Panel(.25f, Color.Red)]);
        Assert.That(encoding.ReferenceBudgetExceeded, Is.False);
        Assert.That(encoding.AcceptedBoxes, Is.EqualTo(1));
        Assert.That(encoding.OmittedBoxes, Is.Zero);
        AssertPackedCells(encoding);
    }

    [Test]
    public void RebuildingClearsUnusedCellsAfterStorageGrowth()
    {
        var encoding = new CMU3DSceneEncoding(true);
        encoding.Build(Enumerable.Repeat(new CMU3DSceneBox(Vector3.Zero, new Vector3(31, 31, 1), 0, Color.White), 20).ToArray());
        Assert.That(encoding.GridReferences, Is.EqualTo(163840));
        AssertPackedCells(encoding);
        var rows = encoding.GridRows;
        encoding.Build([Panel(.25f, Color.Red)]);
        Assert.That(encoding.GridRows, Is.EqualTo(rows), "Reuse allocation during camera motion.");
        AssertPackedCells(encoding);
        encoding.Build(Array.Empty<CMU3DSceneBox>());
        Assert.That(encoding.GridReferences, Is.Zero);
        AssertPackedCells(encoding);
    }

    private static List<CMU3DSceneBox> Background(int count) => Enumerable.Repeat(Panel(.75f, Color.Blue), count).ToList();
    private static CMU3DSceneBox Panel(float y, Color color, EntityUid? source = null) =>
        new(new Vector3(.5f, y, 1.3f), new Vector3(.4f, .06f, .4f), 0, color, source);

    private static void AssertPackedCells(CMU3DSceneEncoding encoding)
    {
        var descriptors = encoding.SpatialSize * encoding.SpatialSize * encoding.SpatialDepth * 2;
        var expectedOffset = 0;
        for (var z = 0; z < encoding.SpatialDepth; z++)
        for (var y = 0; y < encoding.SpatialSize; y++)
        for (var x = 0; x < encoding.SpatialSize; x++)
        {
            var descriptor = ((z * encoding.SpatialSize + y) * encoding.SpatialSize + x) * 2;
            var startBytes = encoding.GridPixels[descriptor];
            var start = startBytes.R + startBytes.G * 256 + startBytes.B * 65536;
            var countBytes = encoding.GridPixels[descriptor + 1];
            var count = countBytes.R + countBytes.G * 256 + countBytes.B * 65536;
            var cell = encoding.Cell(x, y, z).ToArray();
            if (start != expectedOffset || count != cell.Length)
                Assert.Fail($"Cell {x},{y},{z}: expected offset {expectedOffset}, count {cell.Length}; got {start}, {count}.");
            for (var i = 0; i < count; i++)
            {
                var reference = start + i;
                var pair = encoding.GridPixels[descriptors + reference];
                var id = pair.R + pair.G * 256 + (pair.B >> 7) * 65536;
                Assert.That(id, Is.EqualTo(cell[i]), $"Cell {x},{y}, reference {i}");
                var box = encoding.Boxes[id - 1];
                Assert.That((pair.B & 127) * 2 - 128, Is.LessThanOrEqualTo(box.Center.Z - box.AxisAlignedHalfSize.Z));
                Assert.That(pair.A * 2 - 128, Is.GreaterThanOrEqualTo(box.Center.Z + box.AxisAlignedHalfSize.Z));
            }
            expectedOffset += count;
        }
        Assert.That(expectedOffset, Is.EqualTo(encoding.GridReferences));
        Assert.That(encoding.GridPixels.Length, Is.EqualTo(encoding.GridRows * CMU3DSceneEncoding.GridTextureWidth));
    }
}
