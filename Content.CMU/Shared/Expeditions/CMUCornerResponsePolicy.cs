using System.Numerics;

namespace Content.Shared.CMU14.Expeditions;

/// <summary>Geometry of remembered local fire, independent of entity visibility and routing.</summary>
public static class CMUCornerResponsePolicy
{
    public static float LaneCost(Vector2 point, Vector2 start, Vector2 end, float weight, float width)
    {
        if (width <= 0 || weight <= 0)
            return 0;
        var segment = end - start;
        var along = segment.LengthSquared() > 0.001f
            ? Math.Clamp(Vector2.Dot(point - start, segment) / segment.LengthSquared(), 0, 1) : 0;
        return weight * Math.Max(0, 1 - Vector2.Distance(point, start + segment * along) / width);
    }

    public static bool PassageSafe(Vector2 start, Vector2 end, Func<Vector2, float> cost)
    {
        var delta = end - start;
        var steps = Math.Clamp((int) MathF.Ceiling(delta.Length() / 0.35f), 1, 96);
        // An agent inside danger can leave along a level or decreasing gradient. The small
        // tolerance covers floating point error without making each route edge climb deeper.
        var limit = Math.Max(5, cost(start) + 0.01f);
        for (var step = 1; step <= steps; step++)
            if (cost(start + delta * ((float) step / steps)) > limit)
                return false;
        return true;
    }

    public static bool RelevantApproach(Vector2 start, Vector2 danger, Vector2 destination)
    {
        var direction = destination - start;
        if (direction.LengthSquared() < 1 || Vector2.DistanceSquared(start, danger) > 64)
            return false;
        var along = Vector2.Dot(danger - start, Vector2.Normalize(direction));
        return along >= -0.5f && along <= direction.Length() + 2;
    }

    public static bool ShelteredPassage(Vector2 start, Vector2 end, Func<Vector2, bool> sheltered,
        ref int remainingSamples)
    {
        var delta = end - start;
        var count = MathF.Ceiling(delta.Length() / 0.5f);
        // Exhaustion cannot certify a route. Do not stretch sample spacing on long segments.
        if (!float.IsFinite(count) || remainingSamples < 2 || count >= remainingSamples)
            return false;
        var steps = Math.Max(1, (int) count);
        remainingSamples -= steps + 1;
        for (var step = 0; step <= steps; step++)
            if (!sheltered(start + delta * ((float) step / steps)))
                return false;
        return true;
    }

    public static Vector2 FlankOffset(Vector2 start, Vector2 danger, int candidate)
    {
        var toward = danger - start;
        if (toward.LengthSquared() < 0.01f)
            return Vector2.Zero;
        toward = Vector2.Normalize(toward);
        var side = new Vector2(-toward.Y, toward.X);
        var width = candidate / 2 == 0 ? 4f : 6f;
        return toward * Math.Min(3, Vector2.Distance(start, danger)) + side * (candidate % 2 == 0 ? width : -width);
    }
}
