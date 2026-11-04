using System.Numerics;
using Content.Shared.CMU14.ThreeD;

namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>Physical floor spacing, independent of the 2D sprite projection offset.</summary>
public static class CMU3DZProjection
{
    public const float StoryHeight = 3;

    // The caller supplies the two floors surrounding LocalPosition, including
    // negative positions during falls. Normalizing a crossing cannot move the eye.
    public static float Height(int depth, float localPosition, float lower, float upper)
    {
        var fraction = localPosition - MathF.Floor(localPosition);
        return (depth + localPosition) * StoryHeight + lower + fraction * (upper - lower);
    }

    public static float SupportedHeight(int depth, float localPosition, CMU3DElevationRamp ramp, Vector2 local)
    {
        var progress = CMU3DElevationField.Progress(ramp, local);
        var support = Curve(ramp.PhysicsCurve, progress) + ramp.PhysicsOffset;
        return depth * StoryHeight + ramp.Bottom + progress * (ramp.Top - ramp.Bottom) +
            (localPosition - support) * StoryHeight;
    }

    public static float Curve(IReadOnlyList<float> curve, float t)
    {
        var scaled = Math.Clamp(t, 0, 1) * (curve.Count - 1);
        var index = (int) scaled;
        return curve[index] + (curve[Math.Min(index + 1, curve.Count - 1)] - curve[index]) * (scaled - index);
    }

    /// <summary>Closed treads follow CMUZLevelsSystem's cardinal high-ground curve.</summary>
    public static CMU3DModelPart[] StairParts(IReadOnlyList<float> curve, Direction direction,
        bool corner, float lower, float upper)
    {
        var nx = corner || direction is Direction.East or Direction.West ? 12 : 1;
        var ny = corner || direction is Direction.North or Direction.South ? 12 : 1;
        var parts = new CMU3DModelPart[nx * ny];
        for (var y = 0; y < ny; y++)
        for (var x = 0; x < nx; x++)
        {
            var u = (x + .5f) / nx;
            var v = (y + .5f) / ny;
            var t = corner ? direction switch
            {
                Direction.East => (u + 1 - v) / 2,
                Direction.West => (1 - u + v) / 2,
                Direction.North => (u + v) / 2,
                _ => (2 - u - v) / 2,
            } : direction switch
            {
                Direction.East => u,
                Direction.West => 1 - u,
                Direction.North => v,
                _ => 1 - v,
            };
            // Use the higher tread edge: the last solid meets its landing exactly.
            var halfSpan = corner ? .5f / nx : .5f / Math.Max(nx, ny);
            var phase = MathF.Max(Curve(curve, t-halfSpan), MathF.Max(Curve(curve, t), Curve(curve, t+halfSpan)));
            var top = phase * StoryHeight + lower + Math.Clamp(phase, 0, 1) * (upper - lower);
            parts[y * nx + x] = new CMU3DModelPart
            {
                Min = new Vector3((float) x / nx - .5f, (float) y / ny - .5f, MathF.Min(lower, top)),
                Max = new Vector3((float) (x + 1) / nx - .5f, (float) (y + 1) / ny - .5f, top),
                Color = Color.FromHex("#69716F"),
            };
        }
        return parts;
    }
}
