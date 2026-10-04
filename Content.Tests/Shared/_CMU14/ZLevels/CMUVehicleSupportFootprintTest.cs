using System.Collections.Generic;
using System.Numerics;
using Content.Shared._CMU14.ZLevels.Vehicles;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Shared._CMU14.ZLevels;

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
    }

    [Test]
    public void NormalizesInvertedBounds()
    {
        var samples = new List<Vector2>();

        CMUVehicleSupportFootprint.GenerateLocalSamples(
            new Box2(-0.5f, 1.5f, 0.5f, -0.5f),
            1f,
            0f,
            samples);

        Assert.That(samples, Has.Count.EqualTo(6));
        Assert.That(samples, Does.Contain(new Vector2(-0.5f, -0.5f)));
        Assert.That(samples, Does.Contain(new Vector2(0.5f, 1.5f)));
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
}
