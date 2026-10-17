using System;
using System.Numerics;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using NUnit.Framework;

namespace Content.Tests.CMU14.TacticalMap;

[TestFixture]
public sealed class CMUReconFurnitureTest
{
    [Test]
    public void EveryFurnitureModelFitsTheLosslessGpuTableAndItsCell()
    {
        var pixels = CMUReconFurniture.EncodeTexture();
        Assert.That(pixels.Length, Is.EqualTo(2176));
        foreach (var material in Enum.GetValues<CMUReconMaterial>())
        {
            if (material < CMUReconMaterial.Chair) continue;
            Assert.That(CMUReconFurniture.TryGet((byte) material, out var model), Is.True, material.ToString());
            Assert.That(model.Parts.Length, Is.InRange(1, CMUReconFurniture.MaxParts));
            var at = ((int) material - CMUReconFurniture.FirstMaterial) * CMUReconFurniture.TextureWidth * 4;
            Assert.That(pixels[at] / 100f, Is.EqualTo(model.Height).Within(0.00001));
            Assert.That(pixels[at + 1] / 100f, Is.EqualTo(model.HalfSize.X).Within(0.00001));
            Assert.That(pixels[at + 2] / 100f, Is.EqualTo(model.HalfSize.Y).Within(0.00001));
            Assert.That(pixels[at + 3], Is.EqualTo(model.Parts.Length));
            at += 4;
            foreach (var part in model.Parts)
            {
                Assert.That(part.Min.X, Is.InRange(0, part.Max.X));
                Assert.That(part.Min.Y, Is.InRange(0, part.Max.Y));
                Assert.That(part.Min.Z, Is.InRange(CMUReconGeometry.FloorHeight, part.Max.Z));
                Assert.That(part.Max.X, Is.LessThanOrEqualTo(1));
                Assert.That(part.Max.Y, Is.LessThanOrEqualTo(1));
                Assert.That(part.Max.Z, Is.LessThan(CMUReconGeometry.LevelHeight));
                Assert.That(Vector3.Distance(new Vector3(pixels[at], pixels[at + 1], pixels[at + 2]) / 100f, part.Min), Is.LessThan(0.00001));
                Assert.That(pixels[at + 3], Is.EqualTo((byte) part.Finish));
                Assert.That(Vector3.Distance(new Vector3(pixels[at + 4], pixels[at + 5], pixels[at + 6]) / 100f, part.Max), Is.LessThan(0.00001));
                at += 8;
            }
        }
    }

    [TestCase(0, 0.5f, 0.75f)]
    [TestCase(1, 0.25f, 0.5f)]
    [TestCase(2, 0.5f, 0.25f)]
    [TestCase(3, 0.75f, 0.5f)]
    public void ChairSeatAndBackPickAtTheirActualHeightAndFacing(byte facing, float backX, float backY)
    {
        var cells = new byte[CMUReconGeometry.Size * CMUReconGeometry.Size];
        var directions = new byte[cells.Length];
        var at = CMUReconGeometry.Index(4, 4, 0);
        cells[at] = (byte) CMUReconMaterial.Chair;
        directions[at] = (byte) (facing | 28); // Floor rotation must not rotate the furniture.
        Assert.That(CMUReconGeometry.TryPick(cells, 1, 0, new Vector3(4.5f, 4.5f, 10), -Vector3.UnitZ,
            out var seat, out _, out _, directions: directions, wallScale: 0.55f), Is.True);
        Assert.That(seat.Z, Is.EqualTo(0.72f).Within(0.001), "Low walls must not shrink furniture.");
        Assert.That(CMUReconGeometry.TryPick(cells, 1, 0, new Vector3(4 + backX, 4 + backY, 10), -Vector3.UnitZ,
            out var back, out _, out _, directions: directions), Is.True);
        Assert.That(back.Z, Is.EqualTo(1.29f).Within(0.001));
    }

    [Test]
    public void RaysPassBetweenChairLegsAndStillReachTheFloorAroundTheChair()
    {
        var cells = new byte[CMUReconGeometry.Size * CMUReconGeometry.Size];
        var appearance = new uint[cells.Length];
        var at = CMUReconGeometry.Index(4, 4, 0);
        cells[at] = (byte) CMUReconMaterial.Chair;
        appearance[at] = 1;
        cells[CMUReconGeometry.Index(5, 4, 0)] = (byte) CMUReconMaterial.Wall;
        Assert.That(CMUReconGeometry.TryPick(cells, 1, 0, new Vector3(3.5f, 4.5f, 0.4f), Vector3.UnitX,
            out _, out var tile, out _, appearance: appearance), Is.True);
        Assert.That(tile.X, Is.EqualTo(5), "The old solid envelope incorrectly picked the gap under the seat.");
        Assert.That(CMUReconGeometry.TryPick(cells, 1, 0, new Vector3(4.1f, 4.1f, 10), -Vector3.UnitZ,
            out var floor, out _, out _, appearance: appearance), Is.True);
        Assert.That(floor.Z, Is.EqualTo(CMUReconGeometry.FloorHeight).Within(0.001));
    }

    [TestCase(CMUReconMaterial.Stool, 0.76f)]
    [TestCase(CMUReconMaterial.OfficeChair, 0.78f)]
    [TestCase(CMUReconMaterial.Table, 0.96f)]
    [TestCase(CMUReconMaterial.Bed, 0.65f)]
    [TestCase(CMUReconMaterial.BunkBed, 1.74f)]
    [TestCase(CMUReconMaterial.Shelf, 1.70f)]
    public void FurnitureSilhouettesHaveDistinctUsableSurfaces(CMUReconMaterial material, float height)
    {
        Assert.That(CMUReconFurniture.TryGet((byte) material, out var model), Is.True);
        Assert.That(model.TryPick(new Vector3(0.5f, 0.5f, 3), -Vector3.UnitZ, 0, out var distance), Is.True);
        Assert.That(3 - distance, Is.EqualTo(height).Within(0.001));
    }
}
