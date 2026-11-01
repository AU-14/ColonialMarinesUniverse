using System.Numerics;
using Content.Shared.CMU14.Xenomorphs.Reaper;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Xenomorphs.Reaper;

/// <summary>A fleshy cord crawls out of the caster, then coils around the bloom's footprint.</summary>
public sealed class CMUFleshBloomTelegraphOverlay(
    IEntityManager entities,
    IGameTiming timing,
    SharedTransformSystem transform) : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    private const int SegmentsPerEdge = 48;
    private static readonly Vector2[] Corners =
    {
        new(-1.5f, -1.5f), new(-1.5f, 1.5f), new(1.5f, 1.5f), new(1.5f, -1.5f),
    };

    private static readonly Color Outline = Color.FromHex("#11131B");
    private static readonly Color Flesh = Color.FromHex("#393442");
    private static readonly Color Highlight = Color.FromHex("#8B7D94");
    private float _alpha = 1;
    private readonly Vector2[] _points = new Vector2[SegmentsPerEdge + 1];
    private readonly Vector2[] _strip = new Vector2[(SegmentsPerEdge + 1) * 2];

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var query = entities.EntityQueryEnumerator<CMUFleshBloomTelegraphComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var visual, out var xform))
        {
            if (xform.MapID != args.MapId || visual.Duration <= TimeSpan.Zero)
                continue;

            var ended = visual.State != CMUReaperCastState.Charging;
            var after = ended ? (float) (timing.CurTime - visual.EndedAt).TotalSeconds : 0;
            var elapsed = (float) ((ended ? visual.EndedAt : timing.CurTime) - visual.StartedAt).TotalSeconds;
            _alpha = 1 - CMUReaperVascularDrawing.Smooth(after / (visual.State == CMUReaperCastState.Succeeded ? 1.5f : 0.7f));
            var progress = elapsed / (float) visual.Duration.TotalSeconds;
            if (progress < 0 || _alpha <= 0)
                continue;

            var bounds = new Box2(new Vector2(-1.7f), new Vector2(1.7f)).ExtendToContain(visual.Origin);
            var matrix = transform.GetWorldMatrix(uid);
            var bottomLeft = Vector2.Transform(bounds.BottomLeft, matrix);
            var worldBounds = new Box2(bottomLeft, bottomLeft);
            worldBounds = worldBounds.ExtendToContain(Vector2.Transform(bounds.TopLeft, matrix));
            worldBounds = worldBounds.ExtendToContain(Vector2.Transform(bounds.TopRight, matrix));
            worldBounds = worldBounds.ExtendToContain(Vector2.Transform(bounds.BottomRight, matrix));
            if (!worldBounds.Enlarged(0.2f).Intersects(args.WorldAABB))
                continue;

            handle.SetTransform(matrix);
            var firstCorner = 0;
            for (var i = 1; i < Corners.Length; i++)
            {
                if (Vector2.DistanceSquared(visual.Origin, Corners[i]) <
                    Vector2.DistanceSquared(visual.Origin, Corners[firstCorner]))
                    firstCorner = i;
            }

            // Establish the connection slowly, then accelerate around the footprint before spraying.
            var tip = DrawEdge(handle, visual.Origin, Corners[firstCorner],
                Math.Clamp(progress / 0.35f, 0, 1), elapsed);
            var wrap = MathF.Pow(Math.Clamp((progress - 0.35f) / 0.6f, 0, 1), 2.4f) * 4;
            for (var edge = 0; edge < 4 && wrap > edge; edge++)
            {
                tip = DrawEdge(handle, Corners[(firstCorner + edge) % 4],
                    Corners[(firstCorner + edge + 1) % 4], Math.Min(1, wrap - edge), elapsed);
            }

            if (visual.State == CMUReaperCastState.Succeeded)
                DrawSpray(handle, after);

            if (progress >= 0.99f)
                continue;
            // A small wet tip leads the fine vessel.
            handle.DrawCircle(tip, 0.045f, Outline.WithAlpha(_alpha));
            handle.DrawCircle(tip, 0.025f, Flesh.WithAlpha(_alpha));
            handle.DrawCircle(tip + new Vector2(-0.02f, 0.02f), 0.01f, Highlight.WithAlpha(_alpha));
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    private static void DrawSpray(DrawingHandleWorld handle, float after)
    {
        for (var i = 0; i < 112; i++)
        {
            var age = after - (i * 13 % 29) / 29f * 0.45f;
            if (age < 0 || age > 0.95f)
                continue;
            var edge = i % 4;
            var start = Vector2.Lerp(Corners[edge], Corners[(edge + 1) % 4], (i * 17 % 71) / 71f);
            var end = new Vector2((i * 7 % 19) / 19f - 0.5f, (i * 11 % 23) / 23f - 0.5f) * 0.8f;
            var travel = Math.Clamp(age / 0.8f, 0, 1);
            var ease = 1 - MathF.Pow(1 - travel, 1.5f);
            var point = Vector2.Lerp(start, end, ease);
            var tail = Vector2.Lerp(start, end, MathF.Max(0, ease - 0.14f));
            var alpha = 1 - CMUReaperVascularDrawing.Smooth(age / 0.95f);
            handle.DrawCircle(point, 0.075f + travel * 0.08f, Highlight.WithAlpha(alpha * 0.12f));
            handle.DrawLine(tail, point, Highlight.WithAlpha(alpha));
            handle.DrawCircle(point, 0.035f, Color.FromHex("#C7B7D0").WithAlpha(alpha * 0.85f));
        }
    }
    private Vector2 DrawEdge(DrawingHandleWorld handle, Vector2 start, Vector2 end, float fraction, float elapsed)
    {
        var delta = end - start;
        if (delta.LengthSquared() < 0.0001f)
            return start;

        var normal = Vector2.Normalize(new Vector2(-delta.Y, delta.X));
        var segments = Math.Max(1, (int) MathF.Ceiling(SegmentsPerEdge * fraction));
        for (var i = 0; i <= segments; i++)
        {
            var t = fraction * i / segments;
            // Pin the ends to the exact square corners while the cord writhes between them.
            var wave = MathF.Sin(t * MathF.PI) * MathF.Sin(t * 18 - elapsed * 20) * 0.055f;
            _points[i] = Vector2.Lerp(start, end, t) + normal * wave;
        }

        DrawStrip(handle, segments, normal, 0.035f, Outline.WithAlpha(_alpha));
        DrawStrip(handle, segments, normal, 0.021f, Flesh.WithAlpha(_alpha));
        DrawStrip(handle, segments, normal, 0.006f, Highlight.WithAlpha(_alpha));
        return _points[segments];
    }

    private void DrawStrip(DrawingHandleWorld handle, int segments, Vector2 normal, float width, Color color)
    {
        for (var i = 0; i <= segments; i++)
        {
            _strip[i * 2] = _points[i] - normal * width;
            _strip[i * 2 + 1] = _points[i] + normal * width;
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, _strip.AsSpan(0, (segments + 1) * 2), color);
    }
}
