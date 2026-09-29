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

    [Test]
    public void ExactlyHalfUnsupportedDoesNotTip()
    {
        Assert.That(CMUVehicleSupportFootprint.ShouldTip(5, 10), Is.False);
    }

    [Test]
    public void MoreThanHalfUnsupportedTips()
    {
        Assert.That(CMUVehicleSupportFootprint.ShouldTip(4, 10), Is.True);
    }

    [Test]
    public void EmptySupportSetFailsClosed()
    {
        Assert.That(CMUVehicleSupportFootprint.GetUnsupportedFraction(0, 0), Is.EqualTo(1f));
        Assert.That(CMUVehicleSupportFootprint.ShouldTip(0, 0), Is.True);
    }

    [Test]
    public void CountsSupportedSamplesWithPredicate()
    {
        var samples = new List<Vector2>();

        CMUVehicleSupportFootprint.GenerateLocalSamples(
            new Box2(-1f, -1f, 1f, 1f),
            1f,
            0f,
            samples);

        var supported = CMUVehicleSupportFootprint.CountSupportedSamples(samples, sample => sample.X <= 0f);

        Assert.That(supported, Is.EqualTo(6));
        Assert.That(CMUVehicleSupportFootprint.ShouldTip(supported, samples.Count), Is.False);
    }

    [Test]
    public void StickySampleIsAlwaysSupported()
    {
        Assert.That(CMUVehicleSupportFootprint.IsSampleSupported(0.9f, true, 0.5f, 0.05f), Is.True);
        Assert.That(CMUVehicleSupportFootprint.IsSampleSupported(-0.9f, true, 0.5f, 0.05f), Is.True);
    }

    [Test]
    public void NonStickySampleSupportsWithinStepWindow()
    {
        Assert.That(CMUVehicleSupportFootprint.IsSampleSupported(-0.5f, false, 0.5f, 0.05f), Is.True);
        Assert.That(CMUVehicleSupportFootprint.IsSampleSupported(0.05f, false, 0.5f, 0.05f), Is.True);
    }

    [Test]
    public void NonStickySampleRejectsBeyondStepWindow()
    {
        Assert.That(CMUVehicleSupportFootprint.IsSampleSupported(-0.51f, false, 0.5f, 0.05f), Is.False);
        Assert.That(CMUVehicleSupportFootprint.IsSampleSupported(0.06f, false, 0.5f, 0.05f), Is.False);
    }

    [Test]
    public void FallingSampleSupportsAfterOvershootingFloor()
    {
        Assert.That(CMUVehicleSupportFootprint.IsSampleSupported(-1.25f, false, 0.5f, 0.05f, falling: true), Is.True);
    }

    [Test]
    public void NonFallingSampleStillRejectsLargeUpwardStep()
    {
        Assert.That(CMUVehicleSupportFootprint.IsSampleSupported(-1.25f, false, 0.5f, 0.05f), Is.False);
    }

    [Test]
    public void FallingPartialSupportCanLand()
    {
        Assert.That(CMUVehicleSupportFootprint.ShouldRejectSupport(1, 10, falling: true), Is.False);
    }

    [Test]
    public void NonFallingPartialSupportStillTips()
    {
        Assert.That(CMUVehicleSupportFootprint.ShouldRejectSupport(1, 10), Is.True);
    }

    [Test]
    public void NonStickySupportUsesAverageDistance()
    {
        var distance = CMUVehicleSupportFootprint.GetSupportSnapDistance(
            -1f,
            4,
            -0.75f,
            false);

        Assert.That(distance, Is.EqualTo(-0.25f).Within(0.001f));
    }

    [Test]
    public void StickySupportUsesHighestSurfaceDistance()
    {
        var distance = CMUVehicleSupportFootprint.GetSupportSnapDistance(
            -1f,
            4,
            -0.75f,
            true);

        Assert.That(distance, Is.EqualTo(-0.75f).Within(0.001f));
    }

    private static void AssertVectorClose(Vector2 actual, Vector2 expected)
    {
        Assert.That(actual.X, Is.EqualTo(expected.X).Within(0.001f));
        Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(0.001f));
    }

    [TestCase(0f, 0f, 2f, 4f, 0.5f, 0.05f)]
    [TestCase(-3f, -2f, 1f, 1f, 0.33f, 0.02f)]
    [TestCase(0f, 0f, 0f, 2f, 1f, 0f)]
    [TestCase(0f, 0f, 0.00005f, 0.0002f, 1f, 0f)]
    [TestCase(-1f, -1f, 1f, 1f, 0f, 0.99999f)]
    [TestCase(1000000f, 0f, 1000001f, 1f, 0.1f, 0f)]
    [TestCase(100000000f, 0f, 100000016f, 1f, 0.5f, 0f)]
    public void SamplesPreserveLegacyCoordinatesAndOrder(
        float left, float bottom, float right, float top, float spacing, float inset)
    {
        var bounds = new Box2(left, bottom, right, top);
        var expected = LegacySamples(bounds, spacing, inset);
        var actual = new List<Vector2> { new(999f, 999f) };
        CMUVehicleSupportFootprint.GenerateLocalSamples(bounds, spacing, inset, actual);
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void VariedFootprintsPreserveEveryRotatedSample()
    {
        var random = new Random(3716);
        var actual = new List<Vector2>();
        for (var i = 0; i < 100; i++)
        {
            var origin = new Vector2(random.NextSingle() * 20 - 10, random.NextSingle() * 20 - 10);
            var bounds = new Box2(-random.NextSingle() * 2, -random.NextSingle() * 2,
                random.NextSingle() * 2, random.NextSingle() * 2);
            var spacing = random.NextSingle() + 0.1f;
            var inset = random.NextSingle();
            var rotation = Angle.FromDegrees(random.Next(360));
            var expected = LegacySamples(bounds, spacing, inset);
            for (var j = 0; j < expected.Count; j++)
                expected[j] = origin + rotation.RotateVec(expected[j]);
            CMUVehicleSupportFootprint.GenerateWorldSamples(bounds, spacing, inset, origin, rotation, actual);
            Assert.That(actual, Is.EqualTo(expected), $"Footprint {i}");
        }
    }

    [Test]
    public void WarmedFootprintGenerationAllocatesNoTemporaryLists()
    {
        var samples = new List<Vector2>();
        var bounds = new Box2(-1, -2, 1, 2);
        for (var i = 0; i < 256; i++)
            CMUVehicleSupportFootprint.GenerateLocalSamples(bounds, 0.5f, 0.05f, samples);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 256; i++)
            CMUVehicleSupportFootprint.GenerateLocalSamples(bounds, 0.5f, 0.05f, samples);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.That(samples, Has.Count.EqualTo(45));
        Assert.That(allocated, Is.Zero);
    }

    // Independent reference retained from the pre-optimization implementation. In particular,
    // rounding at large coordinates and tiny footprints must keep its tolerance and ordering.
    private static List<Vector2> LegacySamples(Box2 bounds, float spacing, float inset)
    {
        const float tolerance = 0.0001f;
        var samples = new List<Vector2>();
        var width = bounds.Right - bounds.Left;
        var height = bounds.Top - bounds.Bottom;
        if (width <= 0 || height <= 0)
        {
            samples.Add(bounds.Center);
            return samples;
        }
        inset = Math.Clamp(inset, 0, MathF.Max(0, MathF.Min(width, height) * 0.5f - tolerance));
        spacing = MathF.Max(spacing, 0.05f);
        var xs = Axis(bounds.Left + inset, bounds.Right - inset);
        var ys = Axis(bounds.Bottom + inset, bounds.Top - inset);
        foreach (var x in xs)
        foreach (var y in ys)
        {
            var sample = new Vector2(x, y);
            var duplicate = false;
            foreach (var existing in samples)
            {
                if ((existing - sample).LengthSquared() > tolerance * tolerance)
                    continue;
                duplicate = true;
                break;
            }
            if (!duplicate) samples.Add(sample);
        }
        return samples;

        List<float> Axis(float min, float max)
        {
            var axis = new List<float> { min };
            if (max - min <= tolerance) return axis;
            var intervals = Math.Max(1, (int) MathF.Ceiling((max - min) / spacing));
            for (var i = 1; i < intervals; i++)
                axis.Add(MathHelper.Lerp(min, max, i / (float) intervals));
            axis.Add(max);
            return axis;
        }
    }
}
