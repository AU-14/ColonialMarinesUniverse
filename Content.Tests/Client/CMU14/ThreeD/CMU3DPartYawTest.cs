using System;
using System.Numerics;
using Content.Client.CMU14.ThreeD;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DPartYawTest
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "RA0039", Justification = "CMU14: This test supplies isolated mutable fixture data; prototype registration and inheritance are not exercised.")]
    [Test]
    public void RotatingAnOffCenterPartRetainsItsPivotAndExpandsItsReviewBounds()
    {
        var part = new CMU3DModelPart { Min = new Vector3(1,2,0), Max = new Vector3(3,2.2f,1), Yaw = 90 };
        part.Bounds(out var min, out var max);
        Assert.That(Vector3.Distance(min,new Vector3(1.9f,1.1f,0)), Is.LessThan(.00001));
        Assert.That(Vector3.Distance(max,new Vector3(2.1f,3.1f,1)), Is.LessThan(.00001));
        var renderer = new CMU3DModelRenderer();
        renderer.SetModel(new CMU3DModelPrototype { Parts = [part] });
        Assert.That(renderer.Min, Is.EqualTo(min));
        Assert.That(renderer.Max, Is.EqualTo(max));
        Assert.That(renderer.WithinBudget, Is.False, "Rotated box faces require the depth-buffered workbench, not the axis-aligned BSP.");
        var solid = new CMU3DSceneBox((part.Min+part.Max)/2,(part.Max-part.Min)/2,part.YawRadians,Color.White);
        Assert.That(CMU3DSceneEncoding.Intersect(solid,new Vector3(2,0,.5f),Vector3.UnitY,out var distance), Is.True);
        Assert.That(distance, Is.EqualTo(1.1f).Within(.00001));
        Assert.That(CMU3DSceneEncoding.Intersect(solid,new Vector3(2.5f,0,.5f),Vector3.UnitY,out _), Is.False);
    }

    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(361f)]
    public void InvalidPartAnglesAreRejected(float yaw)
    {
        var part = new CMU3DModelPart { Min = Vector3.Zero, Max = Vector3.One, Yaw = yaw };
        Assert.That(part.Valid, Is.False);
    }
}
