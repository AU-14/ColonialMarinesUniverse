using System;
using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DPartPitchTest
{
    [TestCase(-90f)]
    [TestCase(-40f)]
    [TestCase(0f)]
    [TestCase(25f)]
    [TestCase(90f)]
    public void TiltedThinStemPreservesItsCapAndRejectsRaysBesideIt(float degrees)
    {
        var part = new CMU3DModelPart
        {
            Min = new Vector3(-1,-.06f,.94f), Max = new Vector3(1,.06f,1.06f),
            Shape = CMU3DPartShape.CylinderX, Yaw = 32, Pitch = degrees,
        };
        var source = new CMU3DSceneBox(new Vector3(0,0,1), new Vector3(1,.06f,.06f),
            part.YawRadians, Color.White, Shape: part.Shape) { Pitch = part.PitchRadians };
        var encoding = new CMU3DSceneEncoding();
        encoding.Build(new[] { source });
        Assert.That(encoding.AcceptedBoxes, Is.EqualTo(1));
        var box = encoding.Boxes[0];
        var axis = new Vector3(MathF.Cos(box.Pitch)*MathF.Cos(box.Yaw),
            MathF.Cos(box.Pitch)*MathF.Sin(box.Yaw), MathF.Sin(box.Pitch));
        var side = new Vector3(-MathF.Sin(box.Yaw), MathF.Cos(box.Yaw), 0);
        var origin = box.Center + axis*2;
        Assert.That(encoding.TryPick(origin, -axis, out var hit), Is.True);
        Assert.That(hit.Distance, Is.EqualTo(1).Within(.0002));
        Assert.That(encoding.TryPick(origin + side*.08f, -axis, out _), Is.False);
        part.Bounds(out var min, out var max);
        Assert.That(Vector3.Distance((max-min)/2, source.AxisAlignedHalfSize), Is.LessThan(.00001));
        Assert.That(encoding.MaxZ, Is.GreaterThanOrEqualTo(box.Center.Z+box.AxisAlignedHalfSize.Z));
        var metadata = encoding.BoxPixels[5];
        if (degrees == 0)
            Assert.That((metadata.G,metadata.B,metadata.A), Is.EqualTo(((byte)0,(byte)0,(byte)0)));
        else
        {
            Assert.That(metadata.G, Is.GreaterThanOrEqualTo(128));
            var decoded = (((metadata.G-128)*256+metadata.A)-16384)*(MathF.PI/32768);
            Assert.That(box.Pitch, Is.EqualTo(decoded));
            Assert.That(MathF.Abs(box.Pitch-source.Pitch), Is.LessThan(.0001));
        }
    }

    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(91f)]
    public void InvalidTiltIsRejected(float degrees)
    {
        var part = new CMU3DModelPart { Min = Vector3.Zero, Max = Vector3.One, Pitch = degrees };
        Assert.That(part.Valid, Is.False);
        var encoding = new CMU3DSceneEncoding();
        encoding.Build(new[] { new CMU3DSceneBox(Vector3.Zero,Vector3.One,0,Color.White) { Pitch = part.PitchRadians } });
        Assert.That(encoding.AcceptedBoxes, Is.Zero);
    }

    [Test]
    public void TiltDoesNotAliasATextureSlot()
    {
        var encoding = new CMU3DSceneEncoding();
        encoding.Build(new[] { new CMU3DSceneBox(Vector3.Zero, Vector3.One, 0, Color.White, SurfaceIndex: 1) { Pitch = .2f } });
        Assert.That(encoding.AcceptedBoxes, Is.Zero);
    }
}
