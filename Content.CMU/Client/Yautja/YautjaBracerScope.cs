using System.Numerics;
using Content.Client.CMU14.Yautja.ClanHall;
using Content.Shared.CMU14.Yautja;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Yautja;

// the tracker drawn as a scope: you in the middle, north up, every signal the server reported
// plotted at its real bearing. distance is square-rooted so close and far signals both stay readable
internal sealed class YautjaBracerScope : Control
{
    private static readonly int[] Ranges = { 15, 30, 60, 120, 250, 500, 1000 };
    private static readonly string[] SectorNames = { "N", "NE", "SE", "S", "SW", "NW" };

    private readonly Font _small;
    private readonly List<YautjaGearTrackerEntry> _entries = new();
    private readonly int[] _sectorCounts = new int[SectorNames.Length];
    private int _range = Ranges[0];

    public int Highlight = -1;

    public YautjaBracerScope(Font small)
    {
        _small = small;
        MinSize = new Vector2(240, 240);
        HorizontalExpand = true;
    }

    public void Set(List<YautjaGearTrackerEntry> entries)
    {
        _entries.Clear();
        _entries.AddRange(entries);
        Array.Clear(_sectorCounts);

        var furthest = 0;
        foreach (var entry in entries)
        {
            furthest = Math.Max(furthest, entry.Distance);
            if (entry.Direction < _sectorCounts.Length)
                _sectorCounts[entry.Direction] += Math.Max(1, entry.Count);
        }

        _range = Ranges[^1];
        foreach (var range in Ranges)
        {
            if (range < furthest)
                continue;

            _range = range;
            break;
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        // stay square: as tall as the column is wide
        var side = MathF.Max(MinSize.X, MathF.Min(availableSize.X, 300));
        return new Vector2(side, side);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var s = UIScale;
        var c = PixelSize / 2;
        var r = MathF.Min(PixelSize.X, PixelSize.Y) / 2 - 26 * s;
        if (r <= 0)
            return;

        // sector spokes on the boundaries between the six directions the server buckets into
        for (var i = 0; i < 6; i++)
        {
            var a = Angle(30 + i * 60);
            handle.DrawLine(c + a * r * 0.12f, c + a * r, Hud.Dim);
        }

        // range rings at an eighth and a half of the range, outer one dotted. with the square root
        // they land at about a third and two thirds of the radius and keep round labels
        Hud.Ring(handle, c, r * MathF.Sqrt(1 / 8f), Hud.Dim);
        Hud.Ring(handle, c, r * MathF.Sqrt(1 / 2f), Hud.Dim);
        for (var i = 0; i < 120; i++)
        {
            var p = c + Angle(i * 3) * r;
            handle.DrawRect(UIBox2.FromDimensions(p - new Vector2(0.75f * s), new Vector2(1.5f * s)), Hud.Line);
        }

        DrawRangeLabel(handle, c, r, 1 / 8f, s);
        DrawRangeLabel(handle, c, r, 1 / 2f, s);
        DrawRangeLabel(handle, c, r, 1f, s);

        // direction names at the rim, lit with a count where something is waiting
        for (var i = 0; i < SectorNames.Length; i++)
        {
            var count = _sectorCounts[i];
            var text = count > 0 ? $"{SectorNames[i]} {count}" : SectorNames[i];
            DrawCentered(handle, text, c + Angle(i * 60) * (r + 16 * s), count > 0 ? Hud.Bright : Hud.Muted);
        }

        // you
        var up = new Vector2(0, -6 * s);
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList,
            new[] { c + up, c + new Vector2(4.5f * s, 4 * s), c + new Vector2(-4.5f * s, 4 * s) },
            Hud.Text);

        // farthest first so the nearest blip sits on top
        for (var i = _entries.Count - 1; i >= 0; i--)
            DrawBlip(handle, c, r, s, i);
    }

    private void DrawBlip(DrawingHandleScreen handle, Vector2 c, float r, float s, int index)
    {
        var entry = _entries[index];
        var fraction = MathF.Sqrt(Math.Clamp(entry.Distance / (float) _range, 0, 1));
        var p = c + Angle(entry.Bearing) * r * fraction;
        var nearest = index == 0;
        var lit = index == Highlight;
        var size = (entry.Count > 1 ? 5.5f : 4f) * s;
        var color = nearest || lit ? Hud.Hot : Hud.Bright;

        if (nearest || lit)
            handle.DrawCircle(p, size * 2.2f, color.WithAlpha(0.18f));

        var diamond = new[] { p + new Vector2(0, -size), p + new Vector2(size, 0), p + new Vector2(0, size), p + new Vector2(-size, 0) };
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, diamond, color);

        if (!lit)
            return;

        Hud.Ring(handle, p, size * 2.6f, Hud.Hot, 24);
        DrawCentered(handle, $"{entry.Distance}m", p + new Vector2(0, -size * 2.6f - 7 * s), Hud.Text);
    }

    private void DrawRangeLabel(DrawingHandleScreen handle, Vector2 c, float r, float fraction, float s)
    {
        var meters = (int) MathF.Round(_range * fraction);
        var p = c + new Vector2(r * MathF.Sqrt(fraction) + 2 * s, 2 * s);
        handle.DrawString(_small, p, $"{meters}m", UIScale, Hud.Muted);
    }

    // bearing in degrees, 0 north, clockwise, to a screen direction (y down)
    private static Vector2 Angle(float degrees)
    {
        var rad = degrees * MathF.PI / 180f;
        return new Vector2(MathF.Sin(rad), -MathF.Cos(rad));
    }

    private void DrawCentered(DrawingHandleScreen handle, string text, Vector2 center, Color color)
    {
        var size = handle.GetDimensions(_small, text, UIScale);
        handle.DrawString(_small, center - size / 2, text, UIScale, color);
    }
}
