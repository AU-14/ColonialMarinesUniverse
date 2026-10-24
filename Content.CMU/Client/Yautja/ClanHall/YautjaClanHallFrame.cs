using System.Collections.Generic;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Yautja.ClanHall;

// the wrist unit around the projection: chamfered alloy, rivets, glyph strips. child 0 sits in
// the top band (title, close), child 1 and anything after fill the screen
internal sealed class YautjaClanHallBezel : Container
{
    public const float Side = 14;
    public const float Top = 40;
    public const float Bottom = 18;
    private const float Chamfer = 18;

    private readonly List<Vector2> _poly = new();

    public YautjaClanHallBezel()
    {
        MouseFilter = MouseFilterMode.Pass;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var inner = Vector2.Max(Vector2.Zero, availableSize - new Vector2(Side * 2, Top + Bottom));
        var size = Vector2.Zero;
        var i = 0;
        foreach (var child in Children)
        {
            child.Measure(i == 0 ? new Vector2(inner.X, Top) : inner);
            if (i > 0)
                size = Vector2.Max(size, child.DesiredSize);
            i++;
        }

        return size + new Vector2(Side * 2, Top + Bottom);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var i = 0;
        foreach (var child in Children)
        {
            child.Arrange(i == 0
                ? UIBox2.FromDimensions(Side + Chamfer, 2, finalSize.X - (Side + Chamfer) * 2, Top - 4)
                : UIBox2.FromDimensions(Side, Top, finalSize.X - Side * 2, finalSize.Y - Top - Bottom));
            i++;
        }

        return finalSize;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var s = UIScale;
        var w = PixelSize.X;
        var h = PixelSize.Y;
        var c = Chamfer * s;

        // body
        _poly.Clear();
        _poly.Add(new Vector2(c, 0));
        _poly.Add(new Vector2(w - c, 0));
        _poly.Add(new Vector2(w, c));
        _poly.Add(new Vector2(w, h - c));
        _poly.Add(new Vector2(w - c, h));
        _poly.Add(new Vector2(c, h));
        _poly.Add(new Vector2(0, h - c));
        _poly.Add(new Vector2(0, c));
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, _poly, YautjaClanHallStyle.Bezel);
        for (var i = 0; i < _poly.Count; i++)
            handle.DrawLine(_poly[i], _poly[(i + 1) % _poly.Count], YautjaClanHallStyle.BezelEdge);

        // a second, inset outline so the plate reads as machined rather than flat
        var inset = 3 * s;
        for (var i = 0; i < _poly.Count; i++)
        {
            var a = Inset(_poly[i], w, h, inset);
            var b = Inset(_poly[(i + 1) % _poly.Count], w, h, inset);
            handle.DrawLine(a, b, YautjaClanHallStyle.BezelShadow);
        }

        // screen recess with a red glow bleeding onto the bezel
        var screen = UIBox2.FromDimensions(Side * s, Top * s, w - Side * 2 * s, h - (Top + Bottom) * s);
        for (var g = 3; g >= 1; g--)
        {
            var grow = g * s;
            handle.DrawRect(new UIBox2(screen.Left - grow, screen.Top - grow, screen.Right + grow, screen.Bottom + grow),
                YautjaClanHallStyle.HotRed.WithAlpha(0.05f * (4 - g)), filled: false);
        }

        handle.DrawRect(screen, YautjaClanHallStyle.Screen);
        handle.DrawRect(screen, YautjaClanHallStyle.Border, filled: false);

        // rivets
        var r = 2.2f * s;
        foreach (var p in new[]
                 {
                     new Vector2(c * 0.55f, c * 0.55f), new Vector2(w - c * 0.55f, c * 0.55f),
                     new Vector2(c * 0.55f, h - c * 0.55f), new Vector2(w - c * 0.55f, h - c * 0.55f),
                 })
        {
            handle.DrawCircle(p, r, YautjaClanHallStyle.BezelShadow);
            handle.DrawCircle(p - new Vector2(0.6f * s), r * 0.6f, YautjaClanHallStyle.BezelEdge);
        }

        // glyph strips along the bottom lip, and a lit power pip
        var glyphY = h - Bottom * s + 5 * s;
        YautjaClanHallStyle.DrawGlyphs(handle, new Vector2(c + 8 * s, glyphY), 7, 9, s * 1.1f, YautjaClanHallStyle.Border);
        YautjaClanHallStyle.DrawGlyphs(handle, new Vector2(w - c - 8 * s - 63 * s * 1.1f, glyphY), 19, 9, s * 1.1f, YautjaClanHallStyle.Border);
        var pip = new Vector2(w / 2, h - Bottom * s / 2);
        handle.DrawCircle(pip, 3 * s, YautjaClanHallStyle.HotRed.WithAlpha(0.25f));
        handle.DrawCircle(pip, 1.6f * s, YautjaClanHallStyle.HotRed);
    }

    private static Vector2 Inset(Vector2 p, float w, float h, float by)
    {
        return new Vector2(
            p.X <= 0.5f ? by : p.X >= w - 0.5f ? w - by : p.X,
            p.Y <= 0.5f ? by : p.Y >= h - 0.5f ? h - by : p.Y);
    }
}

// drawn over the screen: faint scanlines all the time, and a short flicker + sweep when the
// projection changes page, so a page change reads as the bracer re-projecting
internal sealed class YautjaClanHallHolo : Control
{
    private const float FlickerSeconds = 0.32f;
    private float _flicker;

    public YautjaClanHallHolo()
    {
        MouseFilter = MouseFilterMode.Ignore;
        HorizontalExpand = true;
        VerticalExpand = true;
    }

    public void Flicker()
    {
        _flicker = FlickerSeconds;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_flicker > 0)
            _flicker = MathF.Max(0, _flicker - args.DeltaSeconds);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var s = UIScale;
        var w = PixelSize.X;
        var h = PixelSize.Y;

        var step = MathF.Max(2, MathF.Round(3 * s));
        for (var y = 0f; y < h; y += step)
            handle.DrawLine(new Vector2(0, y), new Vector2(w, y), Color.Black.WithAlpha(0.16f));

        // the projector's edge falls off a little at the top and bottom
        var band = 10 * s;
        for (var i = 0; i < band; i++)
        {
            var a = 0.22f * (1 - i / band);
            handle.DrawLine(new Vector2(0, i), new Vector2(w, i), Color.Black.WithAlpha(a));
            handle.DrawLine(new Vector2(0, h - 1 - i), new Vector2(w, h - 1 - i), Color.Black.WithAlpha(a));
        }

        if (_flicker <= 0)
            return;

        var t = 1 - _flicker / FlickerSeconds;
        // two quick dips then settle, like the emitter catching
        var dip = t < 0.25f ? 0.55f : t < 0.4f ? 0.1f : t < 0.55f ? 0.35f : 0.12f * (1 - t);
        handle.DrawRect(UIBox2.FromDimensions(0, 0, w, h), YautjaClanHallStyle.Screen.WithAlpha(dip));
        var sweepY = t * h;
        handle.DrawRect(UIBox2.FromDimensions(0, sweepY - 6 * s, w, 12 * s), YautjaClanHallStyle.HotRed.WithAlpha(0.10f * (1 - t)));
        handle.DrawLine(new Vector2(0, sweepY), new Vector2(w, sweepY), YautjaClanHallStyle.HotRed.WithAlpha(0.45f * (1 - t)));
    }
}
