using System;
using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DFoliageTest
{
    [TestCase(0f)]
    [TestCase(.63f)]
    [TestCase(1.57f)]
    public void OpenLeafSprayRetainsGapsAndCannotExpandItsParentEllipsoid(float yaw)
    {
        var box = new CMU3DSceneBox(new Vector3(0,0,1), new Vector3(.8f,.35f,.6f), yaw, Color.White, Shape: CMU3DPartShape.Foliage);
        var encoding = new CMU3DSceneEncoding();
        encoding.Build([box]);
        Assert.That(encoding.AcceptedBoxes, Is.EqualTo(1));
        Assert.That(encoding.BoxPixels[5].R, Is.EqualTo(11));
        var solid = encoding.Boxes[0];
        var envelope = solid with { Shape = CMU3DPartShape.Ellipsoid };
        var gaps = 0;
        var hits = 0;
        var rotation = Matrix4x4.CreateRotationZ(yaw);
        var ray = Vector3.TransformNormal(Vector3.UnitY, rotation);
        for (var x = -18; x <= 18; x++)
        for (var z = -18; z <= 18; z++)
        {
            var origin = solid.Center + Vector3.TransformNormal(new Vector3(x*.04f,-3,z*.03f),rotation);
            var parentHit = CMU3DSceneEncoding.Intersect(envelope,origin,ray,out var parentDistance);
            if (!CMU3DSceneEncoding.Intersect(solid,origin,ray,out var distance))
            {
                if (parentHit) gaps++;
                continue;
            }
            hits++;
            Assert.That(parentHit, Is.True);
            Assert.That(distance, Is.GreaterThanOrEqualTo(parentDistance-.0001f));
            var point = Vector3.TransformNormal(origin+ray*distance-solid.Center, Matrix4x4.CreateRotationZ(-solid.Yaw))/solid.HalfSize;
            Assert.That(point.LengthSquared(), Is.LessThan(1.001f));
        }
        Assert.That(hits, Is.GreaterThan(100));
        Assert.That(gaps, Is.GreaterThan(100));
        Assert.That(CMU3DSceneEncoding.Intersect(solid,solid.Center,Vector3.Zero,out _), Is.False);
        Assert.That(CMU3DSceneEncoding.Intersect(solid with { Color = Color.Transparent },solid.Center,ray,out _), Is.False);
    }

    [Test]
    public void LeafSpraysRejectPlanarSurfacePaint()
    {
        var part = new CMU3DModelPart { Min = Vector3.Zero, Max = Vector3.One, Shape = CMU3DPartShape.Foliage };
        Assert.That(part.Valid, Is.True);
        part.Surface = "AnySurface";
        Assert.That(part.Valid, Is.False);
        var encoding = new CMU3DSceneEncoding();
        encoding.Build([new CMU3DSceneBox(Vector3.Zero,Vector3.One,0,Color.White,Shape:CMU3DPartShape.Foliage,SurfaceIndex:1)]);
        Assert.That(encoding.AcceptedBoxes, Is.Zero);
    }
}
