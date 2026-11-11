using System.Numerics;

namespace Content.Shared.CMU14.Expeditions;

/// <summary>Clearance between a swept circular body and a fire's world bounds.</summary>
public static class CMUFireAvoidancePolicy
{
    public static float PointDistanceSquared(Vector2 point, Vector2 minimum, Vector2 maximum) =>
        Vector2.DistanceSquared(point, Vector2.Clamp(point, minimum, maximum));

    public static float PassageDistanceSquared(Vector2 start, Vector2 end, Vector2 minimum, Vector2 maximum)
    {
        var delta = end - start;
        var first = 0f;
        var last = 1f;
        if (Slab(start.X, delta.X, minimum.X, maximum.X, ref first, ref last) &&
            Slab(start.Y, delta.Y, minimum.Y, maximum.Y, ref first, ref last))
            return 0;
        var distance = Math.Min(PointDistanceSquared(start, minimum, maximum), PointDistanceSquared(end, minimum, maximum));
        distance = Math.Min(distance, SegmentDistanceSquared(minimum, start, end));
        distance = Math.Min(distance, SegmentDistanceSquared(maximum, start, end));
        distance = Math.Min(distance, SegmentDistanceSquared(new Vector2(minimum.X, maximum.Y), start, end));
        return Math.Min(distance, SegmentDistanceSquared(new Vector2(maximum.X, minimum.Y), start, end));
    }

    public static bool PassageSafe(Vector2 start, Vector2 end, Vector2 minimum, Vector2 maximum,
        float radius, bool escaping = false)
    {
        var threshold = radius * radius;
        if (PassageDistanceSquared(start, end, minimum, maximum) > threshold)
            return true;
        if (!escaping || PointDistanceSquared(start, minimum, maximum) > threshold ||
            PointDistanceSquared(end, minimum, maximum) <= threshold)
            return false;
        // Distance to a convex box cannot initially decrease on an escape. This permits
        // leaving an existing overlap, but rejects entering or cutting across another patch.
        return Vector2.Dot(end - start, start - Vector2.Clamp(start, minimum, maximum)) >= -0.0001f;
    }

    private static float SegmentDistanceSquared(Vector2 point, Vector2 start, Vector2 end)
    {
        var delta = end - start;
        var along = delta.LengthSquared() > 0.000001f
            ? Math.Clamp(Vector2.Dot(point - start, delta) / delta.LengthSquared(), 0, 1) : 0;
        return Vector2.DistanceSquared(point, start + delta * along);
    }

    private static bool Slab(float origin, float delta, float minimum, float maximum, ref float first, ref float last)
    {
        if (Math.Abs(delta) < 0.000001f)
            return origin >= minimum && origin <= maximum;
        var enter = (minimum - origin) / delta;
        var exit = (maximum - origin) / delta;
        first = Math.Max(first, Math.Min(enter, exit));
        last = Math.Min(last, Math.Max(enter, exit));
        return first <= last;
    }
}
