using System.Numerics;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.CMU14.TacticalMap;

[TestFixture]
public sealed class CMUReconGeometryTest
{
    private static byte[] Cells(int levels) => new byte[CMUReconGeometry.Size * CMUReconGeometry.Size * levels];

    [Test]
    public void LargeTreeCanopyRemainsPickableAtItsWiderEdge()
    {
        var cells = Cells(1);
        var directions = new byte[cells.Length];
        var index = CMUReconGeometry.Index(4, 4, 0);
        cells[index] = (byte) CMUReconMaterial.Tree;
        var origin = new Vector3(4.03f, 4.5f, 10);
        Assert.That(CMUReconGeometry.TryPick(cells, 1, 0, origin, -Vector3.UnitZ, out _, out _, out _, directions: directions), Is.False);
        directions[index] = 128;
        Assert.That(CMUReconGeometry.TryPick(cells, 1, 0, origin, -Vector3.UnitZ, out _, out var tile, out _, directions: directions), Is.True);
        Assert.That(tile, Is.EqualTo(new Vector2i(4, 4)));
    }

    [TestCase(0), TestCase(1), TestCase(6)]
    public void CutawaySelectsTheExactFloorAtIdenticalCoordinates(int cut)
    {
        var cells = Cells(7);
        for (var z = 0; z < 7; z++)
            cells[CMUReconGeometry.Index(12, 18, z)] = (byte) CMUReconMaterial.Floor;
        Assert.That(CMUReconGeometry.TryPick(cells, 7, cut, new Vector3(12.5f, 18.5f, 40), -Vector3.UnitZ,
            out var hit, out var tile, out var level), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(tile, Is.EqualTo(new Vector2i(12, 18)));
            Assert.That(level, Is.EqualTo(cut));
            Assert.That(hit.Z, Is.EqualTo(cut * 3 + CMUReconGeometry.FloorHeight).Within(0.001));
        });
    }

    [Test]
    public void OpeningRevealsTheLowerFloor()
    {
        var cells = Cells(2);
        cells[CMUReconGeometry.Index(8, 9, 0)] = (byte) CMUReconMaterial.Ground;
        Assert.That(CMUReconGeometry.TryPick(cells, 2, 1, new Vector3(8.5f, 9.5f, 12), -Vector3.UnitZ,
            out _, out _, out var level), Is.True);
        Assert.That(level, Is.Zero, "The UI must reject this for a Z+1 order rather than silently targeting Z0.");
    }

    [Test]
    public void WallOccludesGroundUntilItsCellIsReconstructed()
    {
        var cells = Cells(1);
        cells[CMUReconGeometry.Index(4, 4, 0)] = (byte) CMUReconMaterial.Wall;
        cells[CMUReconGeometry.Index(5, 4, 0)] = (byte) CMUReconMaterial.Floor;
        var origin = new Vector3(0.5f, 4.5f, 3);
        var ray = Vector3.Normalize(new Vector3(5, 0, -2.82f));
        Assert.That(CMUReconGeometry.TryPick(cells, 1, 0, origin, ray, out _, out var before, out _), Is.True);
        Assert.That(before.X, Is.EqualTo(4));
        Assert.That(CMUReconGeometry.CanOrder(cells[CMUReconGeometry.Index(before.X, before.Y, 0)]), Is.False);
        cells[CMUReconGeometry.Index(4, 4, 0)] = (byte) CMUReconMaterial.Floor;
        Assert.That(CMUReconGeometry.TryPick(cells, 1, 0, origin, ray, out _, out var after, out _), Is.True);
        Assert.That(after.X, Is.EqualTo(5));
    }

    [TestCase(-10, 0.5f, 1, 0), TestCase(60, 0.5f, -1, 0), TestCase(0.5f, -10, 0, 1), TestCase(0.5f, 60, 0, -1)]
    public void AxisAlignedRaysDoNotStallAtCellBoundaries(float x, float y, float dx, float dy)
    {
        var cells = Cells(1);
        cells[CMUReconGeometry.Index(0, 0, 0)] = (byte) CMUReconMaterial.Wall;
        Assert.That(CMUReconGeometry.TryPick(cells, 1, 0, new Vector3(x, y, 1), new Vector3(dx, dy, 0),
            out _, out var tile, out _), Is.True);
        Assert.That(tile, Is.EqualTo(Vector2i.Zero));
    }

    [Test]
    public void LowWallsUseTheDisplayedHeightForPicking()
    {
        var cells = Cells(1);
        cells[CMUReconGeometry.Index(4, 4, 0)] = (byte) CMUReconMaterial.Wall;
        Assert.That(CMUReconGeometry.TryPick(cells, 1, 0, new Vector3(4.5f, 4.5f, 10), -Vector3.UnitZ,
            out var hit, out _, out _, wallScale: 0.38f), Is.True);
        Assert.That(hit.Z, Is.EqualTo(2.8f * 0.38f).Within(0.001));
    }

    [Test]
    public void IsolatingAFloorDoesNotPickHiddenLowerFloorsThroughHoles()
    {
        var cells = Cells(2);
        cells[CMUReconGeometry.Index(4, 4, 0)] = (byte) CMUReconMaterial.Floor;
        Assert.That(CMUReconGeometry.TryPick(cells, 2, 1, new Vector3(4.5f, 4.5f, 10), -Vector3.UnitZ,
            out _, out _, out _, isolate: true), Is.False);
    }

    [Test]
    public void EmptySectorAndRaysPointingAwayDoNotSelectAnything()
    {
        var cells = Cells(1);
        Assert.That(CMUReconGeometry.TryPick(cells, 1, 0, new Vector3(24, 24, 10), -Vector3.UnitZ, out _, out _, out _), Is.False);
        cells[CMUReconGeometry.Index(24, 24, 0)] = (byte) CMUReconMaterial.Wall;
        Assert.That(CMUReconGeometry.TryPick(cells, 1, 0, new Vector3(24, 24, 10), Vector3.UnitZ, out _, out _, out _), Is.False);
    }

    [Test]
    public void InvalidGeometryAndNonFiniteRaysAreRejected()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CMUReconGeometry.TryPick([], 1, 0, Vector3.One, -Vector3.UnitZ, out _, out _, out _), Is.False);
            Assert.That(CMUReconGeometry.TryPick(Cells(1), 1, 0, new Vector3(float.NaN), Vector3.One, out _, out _, out _), Is.False);
            Assert.That(CMUReconGeometry.TryPick(Cells(1), 1, 0, Vector3.One, Vector3.Zero, out _, out _, out _), Is.False);
            Assert.That(CMUReconGeometry.ValidCells(Cells(9), 9), Is.False);
        });
    }

    [Test]
    public void FullMapPickingReachesFarEdgeAndPreservesFloorBelowInsetProps()
    {
        const int width = 416;
        const int height = 224;
        var cells = new byte[width * height];
        var appearance = new uint[cells.Length];
        var index = CMUReconGeometry.Index(400, 210, 0, width, height);
        cells[index] = (byte) CMUReconMaterial.Crate;
        appearance[index] = 1;
        Assert.That(CMUReconGeometry.TryPick(cells, 1, 0, new Vector3(400.02f, 210.02f, 20), -Vector3.UnitZ,
            out var floor, out var tile, out _, width: width, height: height, appearance: appearance), Is.True);
        Assert.That(tile, Is.EqualTo(new Vector2i(400, 210)));
        Assert.That(floor.Z, Is.EqualTo(CMUReconGeometry.FloorHeight).Within(0.001));
        Assert.That(CMUReconGeometry.TryPick(cells, 1, 0, new Vector3(400.5f, 210.5f, 20), -Vector3.UnitZ,
            out var crate, out _, out _, width: width, height: height, appearance: appearance), Is.True);
        Assert.That(crate.Z, Is.EqualTo(1.15f).Within(0.001));
    }

    [Test]
    public void RotatedDoorsHaveMatchingThinFootprints()
    {
        var cells = Cells(1);
        var directions = new byte[cells.Length];
        var index = CMUReconGeometry.Index(4, 4, 0);
        cells[index] = (byte) CMUReconMaterial.Door;
        var origin = new Vector3(4.1f, 4.5f, 10);
        Assert.That(CMUReconGeometry.TryPick(cells, 1, 0, origin, -Vector3.UnitZ, out _, out _, out _, directions: directions), Is.True);
        directions[index] = 1;
        Assert.That(CMUReconGeometry.TryPick(cells, 1, 0, origin, -Vector3.UnitZ, out _, out _, out _, directions: directions), Is.False);
    }

    [TestCase(CMUReconMaterial.Empty, false), TestCase(CMUReconMaterial.Wall, false), TestCase(CMUReconMaterial.Door, false),
     TestCase(CMUReconMaterial.Glass, false), TestCase(CMUReconMaterial.Barricade, false),
     TestCase(CMUReconMaterial.Floor, true), TestCase(CMUReconMaterial.Ground, true)]
    public void OrdersRequireSupportedClearGround(CMUReconMaterial material, bool expected) =>
        Assert.That(CMUReconGeometry.CanOrder((byte) material), Is.EqualTo(expected));
}
