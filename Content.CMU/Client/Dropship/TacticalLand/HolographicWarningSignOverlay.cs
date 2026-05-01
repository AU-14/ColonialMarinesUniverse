using System.Numerics;
using Content.Shared._CMU14.Dropship.TacticalLand;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client._CMU14.Dropship.TacticalLand;

public sealed class HolographicWarningSignOverlay : Overlay
{
    private readonly IEntityManager _entMan;
    private readonly IGameTiming _timing;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public HolographicWarningSignOverlay(IEntityManager entMan, IGameTiming timing)
    {
        _entMan = entMan;
        _timing = timing;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var transform = _entMan.System<SharedTransformSystem>();
        var handle = args.WorldHandle;
        var query = _entMan.EntityQueryEnumerator<HolographicWarningSignComponent, TransformComponent>();

        var slow = (float)((Math.Sin(_timing.RealTime.TotalSeconds * 3.5) + 1.0) * 0.5);
        var fast = (float)((Math.Sin(_timing.RealTime.TotalSeconds * 8.0) + 1.0) * 0.5);

        var amber = new Color(1.00f, 0.78f, 0.10f);
        var lemon = new Color(1.00f, 0.92f, 0.32f);
        var cream = new Color(1.00f, 0.97f, 0.70f);

        while (query.MoveNext(out _, out _, out var xform))
        {
            var c = transform.GetWorldPosition(xform);

            var cx = MathF.Floor(c.X) + 0.5f;
            var cy = MathF.Floor(c.Y) + 0.5f;

            var halo = new Box2(cx - 0.50f, cy - 0.50f, cx + 0.50f, cy + 0.50f);
            handle.DrawRect(halo, amber.WithAlpha(0.10f + 0.10f * slow));

            var apex   = new Vector2(cx,          cy + 0.42f);
            var bl     = new Vector2(cx - 0.42f,  cy - 0.30f);
            var br     = new Vector2(cx + 0.42f,  cy - 0.30f);

            DrawTriangleOutline(handle, apex, bl, br, amber.WithAlpha(0.45f + 0.20f * slow), thicken: 0.04f);

            var innerApex = new Vector2(cx,          cy + 0.34f);
            var innerBl   = new Vector2(cx - 0.34f,  cy - 0.22f);
            var innerBr   = new Vector2(cx + 0.34f,  cy - 0.22f);
            DrawTriangleOutline(handle, innerApex, innerBl, innerBr, lemon.WithAlpha(0.85f + 0.15f * slow), thicken: 0.02f);

            var bar = new Box2(cx - 0.04f, cy - 0.05f, cx + 0.04f, cy + 0.20f);
            handle.DrawRect(bar, cream.WithAlpha(0.95f));
            handle.DrawRect(bar.Enlarged(0.025f), amber.WithAlpha(0.50f * slow));

            var dot = new Box2(cx - 0.05f, cy - 0.20f, cx + 0.05f, cy - 0.10f);
            handle.DrawRect(dot, cream.WithAlpha(0.95f));
            handle.DrawRect(dot.Enlarged(0.025f), amber.WithAlpha(0.50f * slow));

            var sweep = (float)((_timing.RealTime.TotalSeconds * 0.6) % 1.0);
            var sy = cy - 0.45f + sweep * 0.90f;
            var line = new Box2(cx - 0.48f, sy - 0.012f, cx + 0.48f, sy + 0.012f);
            handle.DrawRect(line, lemon.WithAlpha(0.30f + 0.20f * fast));
        }
    }

    private static void DrawTriangleOutline(DrawingHandleWorld handle, Vector2 a, Vector2 b, Vector2 c, Color color, float thicken)
    {
        // No stackalloc here — the Robust content sandbox forbids it.
        DrawTri(handle, a, b, c, color, Vector2.Zero);
        if (thicken <= 0)
            return;

        DrawTri(handle, a, b, c, color, new Vector2( thicken, 0));
        DrawTri(handle, a, b, c, color, new Vector2(-thicken, 0));
        DrawTri(handle, a, b, c, color, new Vector2(0,  thicken));
        DrawTri(handle, a, b, c, color, new Vector2(0, -thicken));
    }

    private static void DrawTri(DrawingHandleWorld handle, Vector2 a, Vector2 b, Vector2 c, Color color, Vector2 off)
    {
        handle.DrawLine(a + off, b + off, color);
        handle.DrawLine(b + off, c + off, color);
        handle.DrawLine(c + off, a + off, color);
    }
}
