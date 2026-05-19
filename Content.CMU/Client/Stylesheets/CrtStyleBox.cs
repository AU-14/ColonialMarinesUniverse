using System;
using Robust.Client.Graphics;
using Robust.Shared.Maths;

namespace Content.Client.Stylesheets;

public sealed class CrtStyleBox : StyleBox
{
    public Color BackgroundColor { get; set; }
    public Color BorderColor { get; set; }
    public Color ScanlineColor { get; set; } = StyleNano.CrtGreen.WithAlpha(0.018f);
    public Color GridColor { get; set; } = StyleNano.CrtGreen.WithAlpha(0.018f);
    public Color NoiseColor { get; set; } = StyleNano.CrtGreenSoft.WithAlpha(0.045f);
    public Color CornerColor { get; set; } = StyleNano.CrtGreen.WithAlpha(0.55f);
    public Color PixelationColor { get; set; } = StyleNano.CrtGreen.WithAlpha(0.04f);
    public Color PixelationShadowColor { get; set; } = StyleNano.CrtButtonBackground.WithAlpha(0.18f);

    public Thickness BorderThickness { get; set; }
    public bool DrawScanlines { get; set; } = true;
    public bool DrawGrid { get; set; }
    public bool DrawNoise { get; set; }
    public bool DrawPixelation { get; set; }
    public bool DrawCornerTicks { get; set; } = true;
    public float ScanlineSpacing { get; set; } = 86f;
    public float GridSpacing { get; set; } = 88f;
    public float NoiseSpacing { get; set; } = 9f;
    public float PixelationBlockSize { get; set; } = 4f;
    public float PixelationSpacing { get; set; } = 132f;
    public float CornerLength { get; set; } = 14f;
    public int NoiseSeed { get; set; } = 7;
    public int NoiseChance { get; set; } = 7;
    public int PixelationSeed { get; set; } = 13;
    public int PixelationChance { get; set; } = 12;
    public int PixelationClusterSize { get; set; } = 2;
    public int MaxScanlines { get; set; } = 3;

    public CrtStyleBox()
    {
    }

    public CrtStyleBox(CrtStyleBox other) : base(other)
    {
        BackgroundColor = other.BackgroundColor;
        BorderColor = other.BorderColor;
        ScanlineColor = other.ScanlineColor;
        GridColor = other.GridColor;
        NoiseColor = other.NoiseColor;
        CornerColor = other.CornerColor;
        PixelationColor = other.PixelationColor;
        PixelationShadowColor = other.PixelationShadowColor;
        BorderThickness = other.BorderThickness;
        DrawScanlines = other.DrawScanlines;
        DrawGrid = other.DrawGrid;
        DrawNoise = other.DrawNoise;
        DrawPixelation = other.DrawPixelation;
        DrawCornerTicks = other.DrawCornerTicks;
        ScanlineSpacing = other.ScanlineSpacing;
        GridSpacing = other.GridSpacing;
        NoiseSpacing = other.NoiseSpacing;
        PixelationBlockSize = other.PixelationBlockSize;
        PixelationSpacing = other.PixelationSpacing;
        CornerLength = other.CornerLength;
        NoiseSeed = other.NoiseSeed;
        NoiseChance = other.NoiseChance;
        PixelationSeed = other.PixelationSeed;
        PixelationChance = other.PixelationChance;
        PixelationClusterSize = other.PixelationClusterSize;
        MaxScanlines = other.MaxScanlines;
    }

    protected override void DoDraw(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        var thickness = BorderThickness.Scale(uiScale);
        var inner = thickness.Deflate(box);

        handle.DrawRect(inner, BackgroundColor);

        if (!StyleNano.CrtUiEnabled)
        {
            DrawBorder(handle, box, thickness);
            return;
        }

        if (DrawPixelation)
            DrawPixelatedBreakup(handle, inner, uiScale);

        if (DrawGrid)
            DrawGridLines(handle, inner, uiScale);

        if (DrawScanlines)
            DrawScanlinesOverlay(handle, inner, uiScale);

        if (DrawNoise)
            DrawPixelNoise(handle, inner, uiScale);

        DrawBorder(handle, box, thickness);

        if (DrawCornerTicks)
            DrawCorners(handle, inner, uiScale);
    }

    protected override float GetDefaultContentMargin(Margin margin)
    {
        return margin switch
        {
            Margin.Top => BorderThickness.Top,
            Margin.Bottom => BorderThickness.Bottom,
            Margin.Right => BorderThickness.Right,
            Margin.Left => BorderThickness.Left,
            _ => throw new ArgumentOutOfRangeException(nameof(margin), margin, null)
        };
    }

    private void DrawBorder(DrawingHandleScreen handle, UIBox2 box, Thickness thickness)
    {
        var (left, top, right, bottom) = thickness;

        if (left > 0)
            handle.DrawRect(new UIBox2(box.Left, box.Top, box.Left + left, box.Bottom), BorderColor);

        if (top > 0)
            handle.DrawRect(new UIBox2(box.Left, box.Top, box.Right, box.Top + top), BorderColor);

        if (right > 0)
            handle.DrawRect(new UIBox2(box.Right - right, box.Top, box.Right, box.Bottom), BorderColor);

        if (bottom > 0)
            handle.DrawRect(new UIBox2(box.Left, box.Bottom - bottom, box.Right, box.Bottom), BorderColor);
    }

    private void DrawScanlinesOverlay(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        var spacing = MathF.Max(2f, ScanlineSpacing * uiScale);
        var lineHeight = MathF.Max(1f, uiScale);
        var drawn = 0;

        for (var y = MathF.Floor(box.Top + spacing); y < box.Bottom; y += spacing)
        {
            if (MaxScanlines > 0 && drawn >= MaxScanlines)
                break;

            handle.DrawRect(
                new UIBox2(box.Left, y, box.Right, MathF.Min(y + lineHeight, box.Bottom)),
                ScanlineColor);

            drawn++;
        }
    }

    private void DrawGridLines(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        var spacing = MathF.Max(24f, GridSpacing * uiScale);
        var lineWidth = MathF.Max(1f, uiScale);

        for (var x = box.Left + spacing; x < box.Right; x += spacing)
            handle.DrawRect(new UIBox2(x, box.Top, MathF.Min(x + lineWidth, box.Right), box.Bottom), GridColor);

        for (var y = box.Top + spacing; y < box.Bottom; y += spacing)
            handle.DrawRect(new UIBox2(box.Left, y, box.Right, MathF.Min(y + lineWidth, box.Bottom)), GridColor);
    }

    private void DrawPixelNoise(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        var pixel = MathF.Max(1f, uiScale);
        var xStep = MathF.Max(5f, NoiseSpacing * uiScale);
        var yStep = MathF.Max(5f, (NoiseSpacing - 2f) * uiScale);
        var chance = Math.Max(1, NoiseChance);
        var row = 0;

        for (var y = box.Top + yStep * 0.5f; y < box.Bottom; y += yStep)
        {
            var column = 0;
            for (var x = box.Left + xStep * 0.5f; x < box.Right; x += xStep)
            {
                if (((column * 29 + row * 17 + NoiseSeed) % chance) == 0)
                {
                    handle.DrawRect(
                        new UIBox2(x, y, MathF.Min(x + pixel, box.Right), MathF.Min(y + pixel, box.Bottom)),
                        NoiseColor);
                }

                column++;
            }

            row++;
        }
    }

    private void DrawPixelatedBreakup(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        var block = MathF.Max(2f, PixelationBlockSize * uiScale);
        var spacing = MathF.Max(block * 7f, PixelationSpacing * uiScale);
        var chance = Math.Max(1, PixelationChance);
        var clusterSize = Math.Max(1, PixelationClusterSize);
        var row = 0;

        for (var y = box.Top + spacing * 0.38f; y < box.Bottom; y += spacing)
        {
            var column = 0;
            for (var x = box.Left + spacing * 0.31f; x < box.Right; x += spacing)
            {
                var hash = Hash(column, row, PixelationSeed);
                if (hash % (uint) chance != 0)
                {
                    column++;
                    continue;
                }

                var originX = x + ((int) ((hash >> 5) % 9) - 4) * block;
                var originY = y + ((int) ((hash >> 9) % 7) - 3) * block;

                for (var i = 0; i < clusterSize; i++)
                {
                    var cellHash = Hash(column + i * 3, row + i * 5, PixelationSeed + i);
                    var offsetX = (int) ((cellHash >> 2) % 4) - 1;
                    var offsetY = (int) ((cellHash >> 7) % 4) - 1;
                    var width = ((int) ((cellHash >> 11) % 2) + 1) * block;
                    var height = ((int) ((cellHash >> 14) % 2) + 1) * block;
                    var px = originX + offsetX * block;
                    var py = originY + offsetY * block;

                    DrawClippedRect(handle, box, px + block * 0.45f, py + block * 0.45f, width, height, PixelationShadowColor);
                    DrawClippedRect(handle, box, px, py, width, height, PixelationColor);
                }

                column++;
            }

            row++;
        }
    }

    private static void DrawClippedRect(
        DrawingHandleScreen handle,
        UIBox2 clip,
        float left,
        float top,
        float width,
        float height,
        Color color)
    {
        var right = MathF.Min(left + width, clip.Right);
        var bottom = MathF.Min(top + height, clip.Bottom);
        left = MathF.Max(left, clip.Left);
        top = MathF.Max(top, clip.Top);

        if (right <= left || bottom <= top)
            return;

        handle.DrawRect(new UIBox2(left, top, right, bottom), color);
    }

    private void DrawCorners(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        var inset = MathF.Max(2f, uiScale * 2f);
        var line = MathF.Max(1f, uiScale);
        var length = MathF.Max(5f, CornerLength * uiScale);
        var left = box.Left + inset;
        var right = box.Right - inset;
        var top = box.Top + inset;
        var bottom = box.Bottom - inset;

        DrawCorner(handle, left, top, length, line, 1, 1);
        DrawCorner(handle, right, top, length, line, -1, 1);
        DrawCorner(handle, left, bottom, length, line, 1, -1);
        DrawCorner(handle, right, bottom, length, line, -1, -1);
    }

    private void DrawCorner(
        DrawingHandleScreen handle,
        float x,
        float y,
        float length,
        float line,
        int xDirection,
        int yDirection)
    {
        var horizontal = new UIBox2(
            xDirection > 0 ? x : x - length,
            y,
            xDirection > 0 ? x + length : x,
            y + line * yDirection);

        var vertical = new UIBox2(
            x,
            yDirection > 0 ? y : y - length,
            x + line * xDirection,
            yDirection > 0 ? y + length : y);

        handle.DrawRect(Normalize(horizontal), CornerColor);
        handle.DrawRect(Normalize(vertical), CornerColor);
    }

    private static UIBox2 Normalize(UIBox2 box)
    {
        return new UIBox2(
            MathF.Min(box.Left, box.Right),
            MathF.Min(box.Top, box.Bottom),
            MathF.Max(box.Left, box.Right),
            MathF.Max(box.Top, box.Bottom));
    }

    private static uint Hash(int column, int row, int seed)
    {
        unchecked
        {
            var hash = (uint) seed * 374761393u;
            hash += (uint) column * 668265263u;
            hash += (uint) row * 2246822519u;
            hash = (hash ^ (hash >> 13)) * 1274126177u;
            return hash ^ (hash >> 16);
        }
    }
}
