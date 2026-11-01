using System;
using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DSurfaceTest
{
    private static CMU3DSceneSurfaces FourCorners(ushort index = 17)
    {
        var result = new CMU3DSceneSurfaces();
        result.Add(index, 2, 2, [new Rgba32(255, 0, 0, 255), new Rgba32(0, 255, 0, 255),
            new Rgba32(0, 0, 255, 255), new Rgba32(200, 90, 40, 0)]);
        return result;
    }

    [Test]
    public void AtlasRetainsOriginalPixelsAndCorrectSlot()
    {
        var surfaces = FourCorners();
        var atlas = surfaces.AtlasPixels();
        Assert.That(surfaces.CellSize, Is.EqualTo(2));
        Assert.That(surfaces.Width, Is.EqualTo(128));
        Assert.That(surfaces.Height, Is.EqualTo(2));
        Assert.That(atlas[32], Is.EqualTo(new Rgba32(255, 0, 0, 255)));
        Assert.That(atlas[surfaces.Width + 33], Is.EqualTo(new Rgba32(200, 90, 40, 0)));
        Assert.That(atlas[34].A, Is.Zero);
        Assert.That(surfaces.Information[16], Is.EqualTo(new Rgba32(1, 1, 0, 255)));
    }

    [Test]
    public void ANewLargerImageExpandsCellsWithoutChangingOriginalPixels()
    {
        var surfaces = FourCorners();
        surfaces.Add(1, 17, 3, new Rgba32[17 * 3]);
        Assert.That(surfaces.CellSize, Is.EqualTo(32));
        Assert.That(surfaces.Width, Is.EqualTo(2048));
        Assert.That(surfaces.Height, Is.EqualTo(32));
        Assert.That(surfaces.AtlasPixels()[512], Is.EqualTo(new Rgba32(255, 0, 0, 255)));
    }

    [TestCase(255)]
    [TestCase(256)]
    [TestCase(257)]
    [TestCase(4095)]
    public void ExtendedSlotsKeepTheirPixelsProjectionAndFlipWithoutAliasing(int slot)
    {
        var index = (ushort) slot;
        var surfaces = FourCorners(index);
        surfaces.Add(1, 1, 1, [new Rgba32(255, 255, 255, 255)]);
        Assert.That(surfaces.Sample(index, new Vector2(.75f, .75f)).A, Is.Zero);
        Assert.That(surfaces.Sample(1, new Vector2(.75f, .75f)).A, Is.EqualTo(255));
        foreach (var axis in new[] { CMU3DSurfaceAxis.XZ, CMU3DSurfaceAxis.XY, CMU3DSurfaceAxis.YZ })
        foreach (var flip in new[] { false, true })
        {
            var encoding = new CMU3DSceneEncoding();
            encoding.Build([new CMU3DSceneBox(Vector3.Zero, Vector3.One, 0, Color.White,
                SurfaceIndex: index, SurfaceAxis: axis, SurfaceFlipU: flip)]);
            Assert.That(encoding.AcceptedBoxes, Is.EqualTo(1));
            var metadata = encoding.BoxPixels[5];
            Assert.That(metadata.B + metadata.G / 8 * 256, Is.EqualTo(index));
            Assert.That(metadata.G % 4, Is.EqualTo((int) axis));
            Assert.That(metadata.G % 8 >= 4, Is.EqualTo(flip));
            Assert.That(encoding.Boxes[0].SurfaceIndex, Is.EqualTo(index));
        }
        var printed = new CMU3DSceneBox(Vector3.Zero, Vector3.One, 0, Color.White, SurfaceIndex: index);
        Assert.That(CMU3DSceneEncoding.Intersect(printed, new Vector3(.5f, -3, -.5f), Vector3.UnitY, out _, surfaces), Is.False);
        Assert.That(CMU3DSceneEncoding.Intersect(printed, new Vector3(-.5f, -3, .5f), Vector3.UnitY, out _, surfaces), Is.True);
    }

    [Test]
    public void OutOfRangeSlotsCannotWrapToUnrelatedArtwork()
    {
        var surfaces = FourCorners();
        Assert.Throws<ArgumentException>(() => surfaces.Add(4096, 1, 1, [new Rgba32(255, 255, 255, 255)]));
        var encoding = new CMU3DSceneEncoding();
        encoding.Build([new CMU3DSceneBox(Vector3.Zero, Vector3.One, 0, Color.White, SurfaceIndex: 4096)]);
        Assert.That(encoding.AcceptedBoxes, Is.Zero);
        Assert.That(encoding.OmittedBoxes, Is.EqualTo(1));
    }

    [TestCase(CMU3DSurfaceAxis.XZ, -.5f, 0, .5f)]
    [TestCase(CMU3DSurfaceAxis.XY, -.5f, .5f, 0)]
    [TestCase(CMU3DSurfaceAxis.YZ, 0, .5f, .5f)]
    public void EachProjectionPlacesSourceUpperLeftAtTheCorrectPhysicalCorner(CMU3DSurfaceAxis axis, float x, float y, float z)
    {
        var point = new Vector3(x, y, z);
        Assert.That(CMU3DSceneSurfaces.Coordinates(point, axis), Is.EqualTo(Vector2.Zero));
        Assert.That(CMU3DSceneSurfaces.Coordinates(-point, axis), Is.EqualTo(Vector2.One));
    }

    [TestCase(0f)]
    [TestCase(1.5707963f)]
    public void TransparentPrintedCornerSelectsTheObjectBehindTheRotatedPanel(float yaw)
    {
        var c = MathF.Cos(yaw);
        var s = MathF.Sin(yaw);
        Vector3 Rotate(Vector3 point) => new(c * point.X - s * point.Y, s * point.X + c * point.Y, point.Z);
        var encoding = new CMU3DSceneEncoding();
        encoding.Build([
            new CMU3DSceneBox(new Vector3(0, 0, 1), new Vector3(.5f, .05f, 1), yaw, Color.White, new EntityUid(1),
                SurfaceIndex: 17),
            new CMU3DSceneBox(Rotate(new Vector3(0, .5f, 1)), new Vector3(.5f, .05f, 1), yaw, Color.White, new EntityUid(2)),
        ]);
        Assert.That(encoding.TryPick(Rotate(new Vector3(.25f, -2, .5f)), Rotate(Vector3.UnitY), out var through, FourCorners()), Is.True);
        Assert.That(through.Source, Is.EqualTo(new EntityUid(2)));
        Assert.That(encoding.TryPick(Rotate(new Vector3(-.25f, -2, 1.5f)), Rotate(Vector3.UnitY), out var print, FourCorners()), Is.True);
        Assert.That(print.Source, Is.EqualTo(new EntityUid(1)));
    }

    [Test]
    public void CutoutCanExposeOpaqueArtworkOnTheExitFace()
    {
        var surfaces = new CMU3DSceneSurfaces();
        surfaces.Add(1, 2, 1, [new Rgba32(0, 0, 0, 0), new Rgba32(255, 255, 0, 255)]);
        var box = new CMU3DSceneBox(new Vector3(0, 0, 1), new Vector3(.5f, .2f, 1), 0, Color.White, SurfaceIndex: 1);
        var ray = Vector3.Normalize(new Vector3(.35f, 1, 0));
        Assert.That(CMU3DSceneEncoding.Intersect(box, new Vector3(-.65f, -2, 1), ray, out var distance, surfaces), Is.True);
        Assert.That(distance, Is.EqualTo(2.2f / ray.Y).Within(.0001f));
    }

    [TestCase(0f, 2)]
    [TestCase(.5f, 1)]
    public void ClearPaintPassesPickingThroughButTranslucentSurfacesRemainSelectable(float alpha, int expected)
    {
        var surfaces = new CMU3DSceneSurfaces();
        surfaces.Add(1, 1, 1, [new Rgba32(0, 0, 0, 128)]);
        var encoding = new CMU3DSceneEncoding();
        encoding.Build([
            new CMU3DSceneBox(Vector3.Zero, Vector3.One, 0, new Color(1f, 1f, 1f, alpha), new EntityUid(1), SurfaceIndex: 1),
            new CMU3DSceneBox(new Vector3(0, 3, 0), Vector3.One, 0, Color.White, new EntityUid(2)),
        ]);
        Assert.That(encoding.TryPick(new Vector3(0, -3, 0), Vector3.UnitY, out var picked, surfaces), Is.True);
        Assert.That(picked.Source, Is.EqualTo(new EntityUid(expected)));
    }

    [Test]
    public void PackedSurfaceMetadataAndCutawaySamplingAgreeWithDecodedGeometry()
    {
        var encoding = new CMU3DSceneEncoding();
        encoding.Build([new CMU3DSceneBox(Vector3.Zero, Vector3.One, 0, Color.White,
            SurfaceIndex: 17, SurfaceAxis: CMU3DSurfaceAxis.YZ, SurfaceVScale: .3f)]);
        var pixel = encoding.BoxPixels[5];
        Assert.That(pixel, Is.EqualTo(new Rgba32(0, 3, 17, 76)));
        var box = encoding.Boxes[0];
        var uv = CMU3DSceneSurfaces.Coordinates(new Vector3(0, -.5f, .5f), box.SurfaceAxis, box.SurfaceVScale);
        Assert.That(uv.X, Is.EqualTo(1));
        Assert.That(uv.Y, Is.EqualTo(1 - 76 / 255f));
    }

    [Test]
    public void RoomSideMountKeepsPrintedArtworkReadableInEncodingAndPicking()
    {
        var source = new CMU3DModelPart
        {
            Min = new Vector3(-.5f, -.55f, 0), Max = new Vector3(.5f, -.53f, 2),
            Surface = "PrintedFlag", SurfaceAxis = CMU3DSurfaceAxis.XZ,
        };
        var mounted = CMU3DSceneLayout.InsideWallParts([source])[0];
        Assert.That(mounted.SurfaceFlipU, Is.True);
        Assert.That(mounted.Surface, Is.EqualTo(source.Surface));
        Assert.That(CMU3DSceneLayout.InsideWallParts([mounted])[0].SurfaceFlipU, Is.False);
        var encoding = new CMU3DSceneEncoding();
        encoding.Build([new CMU3DSceneBox(Vector3.Zero, Vector3.One, 0, Color.White,
            SurfaceIndex: 17, SurfaceFlipU: true)]);
        Assert.That(encoding.BoxPixels[5].G, Is.EqualTo(5));
        var box = encoding.Boxes[0];
        Assert.That(FourCorners().OpaqueAt(box, new Vector3(-.25f, 0, -.25f)), Is.False);
        Assert.That(FourCorners().OpaqueAt(box, new Vector3(.25f, 0, -.25f)), Is.True);
    }
}
