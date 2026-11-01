using System;
using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;
using Robust.Shared.Maths;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DWedgeTest
{
    [TestCase(0f, false)]
    [TestCase(.63f, false)]
    [TestCase(3.1415927f, false)]
    [TestCase(0f, true)]
    [TestCase(.63f, true)]
    [TestCase(3.1415927f, true)]
    public void SlopedSolidLeavesTheUpperOpenHalfEmptyAtEveryYaw(float yaw, bool reverse)
    {
        var sign = reverse ? -1 : 1;
        Vector3 Rotate(Vector3 p) => new(MathF.Cos(yaw)*p.X-MathF.Sin(yaw)*p.Y*sign,
            MathF.Sin(yaw)*p.X+MathF.Cos(yaw)*p.Y*sign,p.Z);
        var box = new CMU3DSceneBox(Vector3.Zero, new Vector3(1,.5f,2), yaw, Color.White,
            Shape: reverse ? CMU3DPartShape.WedgeYReverse : CMU3DPartShape.WedgeY);
        Assert.That(CMU3DSceneEncoding.Intersect(box, Rotate(new Vector3(-3,-.25f,1)), Rotate(Vector3.UnitX),out _),Is.False);
        Assert.That(CMU3DSceneEncoding.Intersect(box, Rotate(new Vector3(-3,.25f,-1)), Rotate(Vector3.UnitX),out var side),Is.True);
        Assert.That(side,Is.EqualTo(2).Within(.00001));
        Assert.That(CMU3DSceneEncoding.Intersect(box, Rotate(new Vector3(0,-3,1)), Rotate(Vector3.UnitY),out var slope),Is.True);
        Assert.That(slope,Is.EqualTo(3.25f).Within(.00001));
        Assert.That(CMU3DSceneEncoding.Intersect(box, Rotate(new Vector3(0,0,-1)), Vector3.UnitZ,out var inside),Is.True);
        Assert.That(inside,Is.EqualTo(1).Within(.00001));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SlopedSurfacePackingAndCutoutPreserveTheOpaqueExit(bool reverse)
    {
        var surfaces = new CMU3DSceneSurfaces();
        surfaces.Add(1,2,1,[new Rgba32(0,0,0,0),new Rgba32(255,255,0,255)]);
        var encoding = new CMU3DSceneEncoding();
        encoding.Build([new CMU3DSceneBox(Vector3.Zero,Vector3.One,0,Color.White,
            Shape: reverse ? CMU3DPartShape.WedgeYReverse : CMU3DPartShape.WedgeY,SurfaceIndex:1)]);
        Assert.That(encoding.AcceptedBoxes,Is.EqualTo(1));
        Assert.That(encoding.BoxPixels[5].R,Is.EqualTo(reverse ? 6 : 5));
        var sign = reverse ? -1 : 1;
        var ray = Vector3.Normalize(new Vector3(.4f,sign,0));
        Assert.That(CMU3DSceneEncoding.Intersect(encoding.Boxes[0],new Vector3(-1.4f,-3*sign,0),ray,out var distance,surfaces),Is.True);
        Assert.That(distance,Is.EqualTo(4/MathF.Abs(ray.Y)).Within(.00001));
        Assert.That(CMU3DSceneEncoding.Intersect(encoding.Boxes[0],new Vector3(-.5f,-3*sign,0),Vector3.UnitY*sign,out _,surfaces),Is.False);
    }
}
