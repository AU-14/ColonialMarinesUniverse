using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._AU14.Radio;

/// <summary>
///     Segmented detection meter on the splice panel. Segments light green through amber to red, and the lit
///     end blinks once the meter is high enough that another missed lock ends the job.
/// </summary>
public sealed class AU14NetSpliceMeter : Control
{
    private const int Segments = 20;
    private const float BlinkThreshold = 70f;

    private static readonly Color Empty = Color.FromHex("#141A16");
    private static readonly Color EmptyBorder = Color.FromHex("#1F2A22");
    private static readonly Color Low = Color.FromHex("#4FCB6B");
    private static readonly Color Mid = Color.FromHex("#D69B32");
    private static readonly Color High = Color.FromHex("#C4453C");

    private float _value;
    private float _blink;

    public AU14NetSpliceMeter()
    {
        MinSize = new Vector2(0, 16);
        HorizontalExpand = true;
    }

    public void SetValue(float value)
    {
        _value = Math.Clamp(value, 0f, 100f);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _blink += args.DeltaSeconds;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var width = PixelWidth;
        var height = PixelHeight;

        if (width <= 0 || height <= 0)
            return;

        var segmentWidth = width / (float) Segments;
        var lit = (int) MathF.Ceiling(_value / 100f * Segments);
        var flash = _value >= BlinkThreshold && MathF.Sin(_blink * 8f) > 0f;

        for (var i = 0; i < Segments; i++)
        {
            var left = i * segmentWidth;
            var box = new UIBox2(left + 1f, 0f, left + segmentWidth - 1f, height);

            if (i >= lit)
            {
                handle.DrawRect(box, Empty);
                handle.DrawRect(new UIBox2(box.Left, box.Bottom - 1f, box.Right, box.Bottom), EmptyBorder);
                continue;
            }

            var fraction = i / (float) (Segments - 1);

            var color = fraction switch
            {
                < 0.5f => Color.InterpolateBetween(Low, Mid, fraction / 0.5f),
                _ => Color.InterpolateBetween(Mid, High, (fraction - 0.5f) / 0.5f),
            };

            // only the leading segments blink, so the meter still reads as a level rather than strobing
            if (flash && i >= lit - 3)
                color = color.WithAlpha(0.35f);

            handle.DrawRect(box, color);
        }
    }
}
