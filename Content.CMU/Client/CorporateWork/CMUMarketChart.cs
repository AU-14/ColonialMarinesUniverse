using System.Linq;
using System.Numerics;
using Content.Client.CMU14.Interface;
using Content.Shared.CMU14.CorporateWork;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.CorporateWork;

/// <summary>
///     A candlestick chart in the style of a trading terminal: one-minute candles, a volume strip
///     along the bottom, a price scale on the right, and a dashed line marking the last price.
/// </summary>
public sealed class CMUMarketChart : Control
{
    private const float AxisWidth = 64;
    private const float TimeAxisHeight = 16;
    private const float VolumeFraction = 0.18f;
    private const int GridLines = 5;

    private readonly Font _font;
    private IReadOnlyList<CMUMarketCandle> _candles = Array.Empty<CMUMarketCandle>();
    private IReadOnlyList<int> _newsCandles = Array.Empty<int>();

    /// <summary>
    ///     How many candles fit across the chart. Older candles scroll off the left.
    /// </summary>
    public int VisibleCandles = 45;

    public CMUMarketChart()
    {
        var fontResource = IoCManager.Resolve<IResourceCache>()
            .GetResource<FontResource>("/EngineFonts/NotoSans/NotoSansMono-Regular.ttf");
        _font = new VectorFont(fontResource, 10);
        RectClipContent = true;
        MinSize = new Vector2(320, 220);
    }

    /// <summary>
    ///     Sets the candles to draw, oldest first; the last one is still forming. <paramref name="newsCandles"/>
    ///     are indices into <paramref name="candles"/> that get a news marker.
    /// </summary>
    public void SetData(IReadOnlyList<CMUMarketCandle> candles, IReadOnlyList<int>? newsCandles = null)
    {
        _candles = candles;
        _newsCandles = newsCandles ?? Array.Empty<int>();
    }

    private static Color Up => StyleNanoCrt ? CrtTerminalPalette.Accent : Color.FromHex("#26A69A");
    private static Color Down => StyleNanoCrt ? CrtTerminalPalette.Alert : Color.FromHex("#EF5350");
    private static bool StyleNanoCrt => Content.Client.Stylesheets.StyleNano.CrtUiEnabled;

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var size = PixelSize;
        var scale = UIScale;
        var axis = AxisWidth * scale;
        var timeAxis = TimeAxisHeight * scale;
        var plot = new UIBox2(0, 0, size.X - axis, size.Y - timeAxis);
        var volumeHeight = plot.Height * VolumeFraction;
        var priceArea = new UIBox2(plot.Left, plot.Top + 6 * scale, plot.Right, plot.Bottom - volumeHeight - 4 * scale);

        handle.DrawRect(new UIBox2(Vector2.Zero, size), CrtTerminalPalette.Void);

        if (_candles.Count == 0)
        {
            var text = Loc.GetString("cmu-markets-chart-empty");
            var dims = handle.GetDimensions(_font, text, 1);
            handle.DrawString(_font, (size - dims) / 2, text, CrtTerminalPalette.TextDim);
            return;
        }

        var start = Math.Max(0, _candles.Count - VisibleCandles);
        var shown = _candles.Count - start;
        var slot = plot.Width / VisibleCandles;
        var body = MathF.Max(1, slot * 0.65f);

        var low = float.MaxValue;
        var high = float.MinValue;
        var maxVolume = 1;
        for (var i = start; i < _candles.Count; i++)
        {
            var c = _candles[i];
            low = MathF.Min(low, c.Low);
            high = MathF.Max(high, c.High);
            maxVolume = Math.Max(maxVolume, c.Volume);
        }

        // Pad the range so a flat market doesn't collapse to a line.
        var pad = MathF.Max((high - low) * 0.1f, high * 0.01f);
        low -= pad;
        high += pad;

        float Y(float price) => priceArea.Bottom - (price - low) / (high - low) * priceArea.Height;

        // Grid and price scale.
        for (var g = 0; g <= GridLines; g++)
        {
            var price = low + (high - low) * g / GridLines;
            var y = Y(price);
            handle.DrawLine(new Vector2(plot.Left, y), new Vector2(plot.Right, y), CrtTerminalPalette.Line.WithAlpha(0.35f));
            handle.DrawString(_font, new Vector2(plot.Right + 6 * scale, y - 6 * scale), $"{price:0.0}", CrtTerminalPalette.TextDim);
        }

        handle.DrawLine(new Vector2(plot.Right, plot.Top), new Vector2(plot.Right, plot.Bottom), CrtTerminalPalette.Line);
        handle.DrawLine(new Vector2(plot.Left, plot.Bottom), new Vector2(plot.Right, plot.Bottom), CrtTerminalPalette.Line);

        // Candles and volume, right-aligned so the forming candle sits at the edge.
        var offset = plot.Right - shown * slot;
        for (var i = start; i < _candles.Count; i++)
        {
            var c = _candles[i];
            var x = offset + (i - start) * slot + slot / 2;
            var color = c.Close >= c.Open ? Up : Down;

            handle.DrawLine(new Vector2(x, Y(c.High)), new Vector2(x, Y(c.Low)), color);
            var top = Y(MathF.Max(c.Open, c.Close));
            var bottom = MathF.Max(Y(MathF.Min(c.Open, c.Close)), top + 1);
            handle.DrawRect(new UIBox2(x - body / 2, top, x + body / 2, bottom), color);

            if (c.Volume > 0)
            {
                var v = (float) c.Volume / maxVolume * (volumeHeight - 2 * scale);
                handle.DrawRect(new UIBox2(x - body / 2, plot.Bottom - v, x + body / 2, plot.Bottom), color.WithAlpha(0.45f));
            }

            // Time labels every ten candles, counted back from now.
            var age = _candles.Count - 1 - i;
            if (age % 10 == 0)
            {
                var label = age == 0 ? Loc.GetString("cmu-markets-chart-now") : Loc.GetString("cmu-markets-chart-ago", ("minutes", age));
                var dims = handle.GetDimensions(_font, label, 1);
                handle.DrawString(_font, new Vector2(x - dims.X / 2, plot.Bottom + 2 * scale), label, CrtTerminalPalette.TextDim);
            }
        }

        // News markers along the top of the price area.
        foreach (var index in _newsCandles.Where(n => n >= start && n < _candles.Count))
        {
            var x = offset + (index - start) * slot + slot / 2;
            var y = plot.Top + 2 * scale;
            handle.DrawRect(new UIBox2(x - 3 * scale, y, x + 3 * scale, y + 6 * scale), CrtTerminalPalette.Caution);
        }

        // Last price.
        var last = _candles[^1];
        var lastY = Y(last.Close);
        var lastColor = last.Close >= last.Open ? Up : Down;
        handle.DrawDottedLine(new Vector2(plot.Left, lastY), new Vector2(plot.Right, lastY), lastColor, dashSize: 4 * scale, gapSize: 3 * scale);
        var tag = new UIBox2(plot.Right, lastY - 7 * scale, size.X, lastY + 7 * scale);
        handle.DrawRect(tag, lastColor);
        handle.DrawString(_font, new Vector2(plot.Right + 6 * scale, lastY - 6 * scale), $"{last.Close:0.0}", CrtTerminalPalette.Void);
    }
}
