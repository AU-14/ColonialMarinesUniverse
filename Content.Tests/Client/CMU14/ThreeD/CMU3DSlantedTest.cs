using System;
using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DSlantedTest
{
    [TestCase(CMU3DPartShape.SlantedX)]
    [TestCase(CMU3DPartShape.SlantedXReverse)]
    [TestCase(CMU3DPartShape.SlantedY)]
    [TestCase(CMU3DPartShape.SlantedYReverse)]
    public void TaperedDiagonalAndEmptyCornersRemainCorrectAfterYawAndPacking(CMU3DPartShape shape)
    {
        var alongX = shape is CMU3DPartShape.SlantedX or CMU3DPartShape.SlantedXReverse;
        var sign = shape is CMU3DPartShape.SlantedXReverse or CMU3DPartShape.SlantedYReverse ? -1 : 1;
        foreach (var yaw in new[] { 0f, .63f, MathF.PI })
        {
            Vector3 Rotate(Vector3 p) => Vector3.Transform(p, Matrix4x4.CreateRotationZ(yaw));
            Vector3 Local(float horizontal, float depth, float height) => alongX
                ? new Vector3(horizontal, depth, height) : new Vector3(depth, horizontal, height);
            var half = Local(.8f, .2f, .6f);
            var box = new CMU3DSceneBox(new Vector3(0,0,1), half, yaw, Color.White, Shape: shape);
            var encoding = new CMU3DSceneEncoding();
            encoding.Build([box]);
            Assert.That(encoding.AcceptedBoxes, Is.EqualTo(1));
            Assert.That(encoding.BoxPixels[5].R, Is.EqualTo((byte) shape));
            foreach (var solid in new[] { box, encoding.Boxes[0] })
            {
                var direction = Rotate(Local(0,1,0));
                var origin = solid.Center + Rotate(Local(.8f*.85f*.6f*sign,-3,.6f*.6f));
                Assert.That(CMU3DSceneEncoding.Intersect(solid,origin,direction,out var hit), Is.True);
                Assert.That(hit, Is.EqualTo(3-.2f*.8f).Within(.002));
                var empty = solid.Center + Rotate(Local(-.8f*.6f*sign,-3,.6f*.6f));
                Assert.That(CMU3DSceneEncoding.Intersect(solid,empty,direction,out _), Is.False);
                Assert.That(CMU3DSceneEncoding.Intersect(solid,solid.Center,direction,out var exit), Is.True);
                Assert.That(exit, Is.EqualTo(.2f).Within(.002));
            }
        }
        var part = new CMU3DModelPart { Min = Vector3.Zero, Max = Vector3.One, Shape = shape };
        Assert.That(part.Valid, Is.True);
        part.Surface = "AnySurface";
        Assert.That(part.Valid, Is.False);
        var rejected = new CMU3DSceneEncoding();
        rejected.Build([new CMU3DSceneBox(Vector3.Zero,Vector3.One,0,Color.White,Shape:shape,SurfaceIndex:1)]);
        Assert.That(rejected.AcceptedBoxes, Is.Zero);
    }
}
