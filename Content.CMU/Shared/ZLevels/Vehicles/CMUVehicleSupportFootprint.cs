using System.Numerics;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Maths;

namespace Content.Shared._CMU14.ZLevels.Vehicles;

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

        var left = MathF.Min(localBounds.Left, localBounds.Right);
        var right = MathF.Max(localBounds.Left, localBounds.Right);
        var bottom = MathF.Min(localBounds.Bottom, localBounds.Top);
        var top = MathF.Max(localBounds.Bottom, localBounds.Top);

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

        var xSamples = new List<float>();
        var ySamples = new List<float>();
        AddAxisSamples(left, right, spacing, xSamples);
        AddAxisSamples(bottom, top, spacing, ySamples);

        foreach (var x in xSamples)
        {
            foreach (var y in ySamples)
            {
                AddSample(samples, new Vector2(x, y));
            }
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

    private static void AddAxisSamples(float min, float max, float spacing, List<float> samples)
    {
        samples.Add(min);

        var length = max - min;
        if (length <= DuplicateTolerance)
            return;

        var intervals = Math.Max(1, (int) MathF.Ceiling(length / spacing));
        for (var i = 1; i < intervals; i++)
        {
            var t = i / (float) intervals;
            samples.Add(MathHelper.Lerp(min, max, t));
        }

        samples.Add(max);
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
