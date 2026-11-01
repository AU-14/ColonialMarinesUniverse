using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client.CMU14.Xenomorphs.Reaper;

/// <summary>Layered, narrow charcoal vessels shared by the Reaper's procedural effects.</summary>
public sealed class CMUReaperVascularDrawing
{
    private const int Segments = 32;
    private readonly Vector2[] _points = new Vector2[Segments + 1];
    private readonly Vector2[] _strip = new Vector2[(Segments + 1) * 2];
    private static readonly Color Shadow = Color.FromHex("#11131B");
    private static readonly Color Tissue = Color.FromHex("#393442");
    private static readonly Color Edge = Color.FromHex("#8B7D94");

    public static float Smooth(float value)
    {
        value = Math.Clamp(value, 0, 1);
        return value * value * (3 - 2 * value);
    }

    public static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
    {
        var u = 1 - t;
        return u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d;
    }

    public void Curve(DrawingHandleWorld handle, Vector2 a, Vector2 b, Vector2 c, Vector2 d,
        float growth, float width, float clock, int seed, float alpha = 1)
    {
        growth = Math.Clamp(growth, 0, 1);
        if (growth <= 0 || alpha <= 0)
            return;

        for (var i = 0; i <= Segments; i++)
        {
            var t = growth * i / Segments;
            _points[i] = Bezier(a, b, c, d, t)
                + new Vector2(MathF.Sin(t * 20 + seed) * MathF.Sin(t * MathF.PI) * 0.035f, 0);
        }

        Strip(handle, width + 0.022f, Shadow.WithAlpha(alpha));
        Strip(handle, width, Tissue.WithAlpha(alpha));
        Strip(handle, width * 0.24f, Edge.WithAlpha(alpha * 0.8f));
        if (growth < 1)
            return;
        var flow = (clock * 0.15f + seed * 0.17f) % 1;
        handle.DrawCircle(Bezier(a, b, c, d, flow), width * 1.2f, Edge.WithAlpha(alpha * 0.65f));
    }

    private void Strip(DrawingHandleWorld handle, float width, Color color)
    {
        for (var i = 0; i <= Segments; i++)
        {
            var tangent = _points[Math.Min(Segments, i + 1)] - _points[Math.Max(0, i - 1)];
            var normal = tangent.LengthSquared() > 0.000001f
                ? Vector2.Normalize(new Vector2(-tangent.Y, tangent.X)) * width : Vector2.UnitY * width;
            _strip[i * 2] = _points[i] - normal;
            _strip[i * 2 + 1] = _points[i] + normal;
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, _strip.AsSpan(), color);
    }
}
