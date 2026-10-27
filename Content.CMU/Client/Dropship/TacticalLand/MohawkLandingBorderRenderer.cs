using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Maths;

namespace Content.Client.CMU14.Dropship.TacticalLand;

/// <summary>Compact holographic beacons mark the landing footprint with open gaps between projectors.</summary>
internal sealed class MohawkLandingBorderRenderer
{
    private static readonly Color Cyan = new(0.20f, 0.85f, 1f);
    private static readonly Color Core = new(0.80f, 0.97f, 1f);
    private static readonly Color Amber = new(1f, 0.72f, 0.25f);
    private readonly Vector2[] _quad = new Vector2[4];

    public void Draw(DrawingHandleWorld handle, Box2 bounds, float seconds)
    {
        for (var beacon = 0; beacon < MohawkApproachAnimation.BeaconCount; beacon++)
        {
            var center = MohawkApproachAnimation.BeaconPosition(bounds, beacon);
            var pulse = MohawkApproachAnimation.Pulse(seconds, beacon);
            var color = Cyan.WithAlpha(0.65f + 0.35f * pulse);

            // Projector cores coincide with real, shadow-casting point lights.
            handle.DrawCircle(center, 0.32f, Cyan.WithAlpha(0.06f + pulse * 0.04f));
            handle.DrawCircle(center, 0.075f, Core.WithAlpha(0.8f + pulse * 0.2f));
            for (var segment = 0; segment < 12; segment++)
            {
                if (segment % 3 == 2)
                    continue;

                var start = segment * MathF.Tau / 12f;
                var end = (segment + 1) * MathF.Tau / 12f;
                Stroke(handle, center + new Vector2(MathF.Cos(start) * 0.24f, MathF.Sin(start) * 0.12f),
                    center + new Vector2(MathF.Cos(end) * 0.24f, MathF.Sin(end) * 0.12f), 0.035f, color);
            }

            var hologram = center + new Vector2(0, 0.52f + 0.035f * pulse);
            var left = hologram + new Vector2(-0.3f, -0.18f);
            var right = hologram + new Vector2(0.3f, -0.18f);
            var top = hologram + new Vector2(0, 0.32f);

            // A translucent fan and interrupted rays make the warning read as a projection above its emitter.
            _quad[0] = center - new Vector2(0.045f, 0);
            _quad[1] = center + new Vector2(0.045f, 0);
            _quad[2] = left;
            _quad[3] = right;
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, _quad, Cyan.WithAlpha(0.07f));
            Stroke(handle, Vector2.Lerp(center, left, 0.2f), Vector2.Lerp(center, left, 0.7f),
                0.025f, Cyan.WithAlpha(0.3f));
            Stroke(handle, Vector2.Lerp(center, right, 0.2f), Vector2.Lerp(center, right, 0.7f),
                0.025f, Cyan.WithAlpha(0.3f));

            _quad[0] = left;
            _quad[1] = right;
            _quad[2] = top;
            _quad[3] = top;
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, _quad, Cyan.WithAlpha(0.08f));
            GlowStroke(handle, left, top, color);
            GlowStroke(handle, top, right, color);
            GlowStroke(handle, right, left, color);

            var scan = (seconds * 0.7f + beacon * 0.13f) % 1f;
            var scanY = -0.15f + scan * 0.43f;
            var scanWidth = (0.32f - scanY) * 0.6f;
            Stroke(handle, hologram + new Vector2(-scanWidth, scanY),
                hologram + new Vector2(scanWidth, scanY), 0.025f, Core.WithAlpha(0.4f));

            var warning = Amber.WithAlpha(0.8f + pulse * 0.2f);
            Stroke(handle, hologram + new Vector2(0, 0.12f), hologram + new Vector2(0, -0.015f),
                0.055f, warning);
            handle.DrawCircle(hologram + new Vector2(0, -0.10f), 0.033f, warning);
        }
    }

    private void GlowStroke(DrawingHandleWorld handle, Vector2 start, Vector2 end, Color color)
    {
        Stroke(handle, start, end, 0.13f, color.WithAlpha(0.12f));
        Stroke(handle, start, end, 0.05f, color);
        Stroke(handle, start, end, 0.018f, Core.WithAlpha(color.A * 0.8f));
    }

    private void Stroke(DrawingHandleWorld handle, Vector2 start, Vector2 end, float width, Color color)
    {
        var tangent = Vector2.Normalize(end - start);
        var normal = new Vector2(-tangent.Y, tangent.X) * (width * 0.5f);
        _quad[0] = start + normal;
        _quad[1] = start - normal;
        _quad[2] = end + normal;
        _quad[3] = end - normal;
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, _quad, color);
    }
}
