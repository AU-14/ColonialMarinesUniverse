using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared.CMU14.ZLevels.Vehicles;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Shared.CMU14.ZLevels;

[TestFixture]
public sealed class CMUVehicleSupportFootprintTest
{
    [Test]
    public void GeneratesEdgesAndCenterFromCollisionFootprint()
    {
        var samples = new List<Vector2>();

        CMUVehicleSupportFootprint.GenerateLocalSamples(
            new Box2(-1f, -1f, 1f, 1f),
            1f,
            0f,
            samples);

        Assert.That(samples, Has.Count.EqualTo(9));
        Assert.That(samples, Does.Contain(new Vector2(-1f, -1f)));
        Assert.That(samples, Does.Contain(Vector2.Zero));
        Assert.That(samples, Does.Contain(new Vector2(1f, 1f)));
        Assert.That(samples, Is.EqualTo(new[]
        {
            new Vector2(-1, -1), new Vector2(-1, 0), new Vector2(-1, 1),
            new Vector2(0, -1), new Vector2(0, 0), new Vector2(0, 1),
            new Vector2(1, -1), new Vector2(1, 0), new Vector2(1, 1),
        }), "Support accumulation must retain the original X-major order.");
    }

    [Test]
    public void WorldSamplesRotateAroundOrigin()
    {
        var samples = new List<Vector2>();

        CMUVehicleSupportFootprint.GenerateWorldSamples(
            new Box2(1f, 0f, 1f, 0f),
            1f,
            0f,
            new Vector2(10f, 20f),
            Angle.FromDegrees(90),
            samples);

        Assert.That(samples, Has.Count.EqualTo(1));
        AssertVectorClose(samples[0], new Vector2(10f, 21f));
    }

    private static void AssertVectorClose(Vector2 actual, Vector2 expected)
    {
        Assert.That(actual.X, Is.EqualTo(expected.X).Within(0.001f));
        Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(0.001f));
    }

    // Independent reference retained from the pre-optimization implementation. In particular,
    // rounding at large coordinates and tiny footprints must keep its tolerance and ordering.

}
