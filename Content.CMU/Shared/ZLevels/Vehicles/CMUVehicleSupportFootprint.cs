using System.Numerics;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Maths;

namespace Content.Shared.CMU14.ZLevels.Vehicles;

public static class CMUVehicleSupportFootprint
{
    public const float DefaultSampleSpacing = 0.5f;
    public const float DefaultSampleInset = 0.05f;
    public const float DefaultEdgeTipUnsupportedFraction = 0.5f;
    private const float MinSampleSpacing = 0.05f;
    private const float DuplicateTolerance = 0.0001f;

    public static void GenerateLocalSamples(
        Box2 localBounds,
        float sampleSpacing,
        float sampleInset,
        List<Vector2> samples)
    {
        samples.Clear();

        var left = localBounds.Left;
        var right = localBounds.Right;
        var bottom = localBounds.Bottom;
        var top = localBounds.Top;

        var width = right - left;
        var height = top - bottom;
        if (width <= 0f || height <= 0f)
        {
            samples.Add(localBounds.Center);
            return;
        }

        var maxInset = MathF.Max(0f, MathF.Min(width, height) * 0.5f - DuplicateTolerance);
        var inset = Math.Clamp(sampleInset, 0f, maxInset);
        var spacing = MathF.Max(sampleSpacing, MinSampleSpacing);

        left += inset;
        right -= inset;
        bottom += inset;
        top -= inset;

        var xIntervals = AxisIntervals(left, right, spacing);
        var yIntervals = AxisIntervals(bottom, top, spacing);
        // A regular grid has no duplicate points when both axes are sufficiently separated.
        // Check the actual rounded coordinates, since tiny bounds or large offsets can collapse
        // samples. Those cases keep the original distance-based duplicate filtering.
        var distinct = AxisSamplesAreDistinct(left, right, xIntervals) &&
                       AxisSamplesAreDistinct(bottom, top, yIntervals);
        for (var xIndex = 0; ; xIndex++)
        {
            var x = AxisSample(left, right, xIntervals, xIndex);
            for (var yIndex = 0; ; yIndex++)
            {
                var sample = new Vector2(x, AxisSample(bottom, top, yIntervals, yIndex));
                if (distinct)
                    samples.Add(sample);
                else
                    AddSample(samples, sample);
                if (yIndex == yIntervals)
                    break;
            }
            if (xIndex == xIntervals)
                break;
        }
    }

    public static void GenerateWorldSamples(
        Box2 localBounds,
        float sampleSpacing,
        float sampleInset,
        Vector2 worldOrigin,
        Angle worldRotation,
        List<Vector2> samples)
    {
        GenerateLocalSamples(localBounds, sampleSpacing, sampleInset, samples);

        for (var i = 0; i < samples.Count; i++)
        {
            samples[i] = worldOrigin + worldRotation.RotateVec(samples[i]);
        }
    }

    public static float GetUnsupportedFraction(int supportedSamples, int totalSamples)
    {
        if (totalSamples <= 0)
            return 1f;

        var supported = Math.Clamp(supportedSamples, 0, totalSamples);
        return 1f - supported / (float) totalSamples;
    }

    public static bool ShouldTip(
        int supportedSamples,
        int totalSamples,
        float edgeTipUnsupportedFraction = DefaultEdgeTipUnsupportedFraction)
    {
        var threshold = Math.Clamp(edgeTipUnsupportedFraction, 0f, 1f);
        return GetUnsupportedFraction(supportedSamples, totalSamples) > threshold;
    }

    public static bool IsSampleSupported(
        float distanceToGround,
        bool stickyGround,
        float maxStepHeight,
        float snapDistance,
        bool falling = false)
    {
        if (stickyGround)
            return true;

        if (falling)
            return distanceToGround <= MathF.Max(0f, snapDistance);

        return distanceToGround >= -MathF.Max(0f, maxStepHeight) &&
               distanceToGround <= MathF.Max(0f, snapDistance);
    }

    public static bool ShouldRejectSupport(
        int supportedSamples,
        int totalSamples,
        float edgeTipUnsupportedFraction = DefaultEdgeTipUnsupportedFraction,
        bool falling = false)
    {
        if (supportedSamples <= 0)
            return true;

        if (falling)
            return false;

        return ShouldTip(supportedSamples, totalSamples, edgeTipUnsupportedFraction);
    }

    public static float GetSupportSnapDistance(
        float supportedDistanceSum,
        int supportedSamples,
        float highestSupportedSurfaceDistance,
        bool stickyGround)
    {
        if (supportedSamples <= 0)
            return 0f;

        return stickyGround ? highestSupportedSurfaceDistance : supportedDistanceSum / supportedSamples;
    }

    public static int CountSupportedSamples(List<Vector2> samples, Predicate<Vector2> isSupported)
    {
        var supported = 0;
        foreach (var sample in samples)
        {
            if (isSupported(sample))
                supported++;
        }

        return supported;
    }

    public static bool TryGetFixtureLocalAabb(FixturesComponent fixtures, out Box2 aabb)
    {
        var first = true;
        aabb = default;

        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (!fixture.Hard)
                continue;

            for (var i = 0; i < fixture.Shape.ChildCount; i++)
            {
                var child = fixture.Shape.ComputeAABB(Transform.Empty, i);

                if (first)
                {
                    aabb = child;
                    first = false;
                }
                else
                {
                    aabb = aabb.Union(child);
                }
            }
        }

        return !first;
    }

    private static int AxisIntervals(float min, float max, float spacing)
    {
        var length = max - min;
        return length <= DuplicateTolerance ? 0 : Math.Max(1, (int) MathF.Ceiling(length / spacing));
    }

    private static float AxisSample(float min, float max, int intervals, int index)
    {
        if (index == 0)
            return min;
        return index == intervals ? max : MathHelper.Lerp(min, max, index / (float) intervals);
    }

    private static bool AxisSamplesAreDistinct(float min, float max, int intervals)
    {
        if (!float.IsFinite(min) || !float.IsFinite(max))
            return false;
        var previous = min;
        for (var i = 1; i < intervals; i++)
        {
            var current = AxisSample(min, max, intervals, i);
            if (!(current - previous > DuplicateTolerance))
                return false;
            previous = current;
        }
        return intervals == 0 || max - previous > DuplicateTolerance;
    }

    private static void AddSample(List<Vector2> samples, Vector2 sample)
    {
        foreach (var existing in samples)
        {
            if ((existing - sample).LengthSquared() <= DuplicateTolerance * DuplicateTolerance)
                return;
        }

        samples.Add(sample);
    }
}
