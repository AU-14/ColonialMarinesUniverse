using System.Numerics;
using Content.Client.CMU14.Interface;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.ColonyEconomy;

// last half hour of one commodity's price multiplier. the dim line is the normal price,
// so anything under it is selling below base
public sealed class CommoditySparkline : Control
{
    private float[] _history = [];

    public CommoditySparkline()
    {
        MinSize = new Vector2(84, 24);
        VerticalAlignment = VAlignment.Center;
    }

    public void SetHistory(float[] history)
    {
        _history = history;
    }

    public static Color ColorFor(float multiplier)
    {
        return multiplier switch
        {
            >= 1.05f => CrtTerminalPalette.Accent,
            > 0.6f => CrtTerminalPalette.Text,
            > 0.4f => CrtTerminalPalette.Caution,
            _ => CrtTerminalPalette.Alert,
        };
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var box = PixelSizeBox;
        handle.DrawRect(box, CrtTerminalPalette.Surface2);

        var top = 1.5f;
        foreach (var value in _history)
            top = MathF.Max(top, value);

        float Y(float value) => box.Bottom - 2 - (box.Height - 4) * Math.Clamp(value / top, 0f, 1f);

        var baseline = Y(1f);
        handle.DrawLine(new Vector2(box.Left, baseline), new Vector2(box.Right, baseline), CrtTerminalPalette.Line);

        if (_history.Length == 0)
            return;

        var last = _history[^1];
        var color = ColorFor(last);
        if (_history.Length == 1)
        {
            handle.DrawRect(UIBox2.FromDimensions(new Vector2(box.Right - 4, Y(last) - 1), new Vector2(3, 3)), color);
            return;
        }

        var step = (box.Width - 4f) / (_history.Length - 1);
        var previous = new Vector2(box.Left + 2, Y(_history[0]));
        for (var i = 1; i < _history.Length; i++)
        {
            var point = new Vector2(box.Left + 2 + step * i, Y(_history[i]));
            handle.DrawLine(previous, point, color);
            previous = point;
        }

        handle.DrawRect(UIBox2.FromDimensions(previous - new Vector2(1, 1), new Vector2(3, 3)), color);
    }
}
