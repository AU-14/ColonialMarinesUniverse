using System;
using Robust.Client.Graphics;
using Robust.Shared.Maths;

namespace Content.Client.UserInterface.Systems.Chat.Widgets;

/// <summary>
///     Background for a chat message row that marks its channel with a filled triangle in the
///     top-right corner instead of a stripe down the left edge. Needs a custom StyleBox because
///     StyleBoxFlat can only draw rectangles.
/// </summary>
public sealed class ChatAccentStyleBox : StyleBox
{
    public Color BackgroundColor { get; set; }
    public Color AccentColor { get; set; }

    /// <summary>
    ///     Length of the triangle's legs along the top and right edges, in virtual pixels.
    /// </summary>
    public float AccentSize { get; set; } = 10f;

    /// <summary>
    ///     Optional full outline, used for messages that carry a background override so a run of
    ///     them doesn't melt into one block.
    /// </summary>
    public Thickness BorderThickness { get; set; }

    public Color BorderColor { get; set; }

    protected override void DoDraw(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        handle.DrawRect(box, BackgroundColor);

        var (left, top, right, bottom) = BorderThickness.Scale(uiScale);

        if (left > 0)
            handle.DrawRect(new UIBox2(box.Left, box.Top, box.Left + left, box.Bottom), BorderColor);
        if (top > 0)
            handle.DrawRect(new UIBox2(box.Left, box.Top, box.Right, box.Top + top), BorderColor);
        if (right > 0)
            handle.DrawRect(new UIBox2(box.Right - right, box.Top, box.Right, box.Bottom), BorderColor);
        if (bottom > 0)
            handle.DrawRect(new UIBox2(box.Left, box.Bottom - bottom, box.Right, box.Bottom), BorderColor);

        // Right angle at the top-right corner, running left along the top edge and down the right.
        // Built from 1px rows rather than DrawPrimitives: the UI render path batches quads, and
        // every other custom stylebox here (see CrtStyleBox) stays on DrawRect for that reason.
        var size = MathF.Min(AccentSize * uiScale, MathF.Min(box.Width, box.Height));
        if (size <= 0)
            return;

        var step = MathF.Max(1f, uiScale);
        for (var offset = 0f; offset < size; offset += step)
        {
            var y = box.Top + offset;
            if (y >= box.Bottom)
                break;

            var width = size - offset;
            handle.DrawRect(
                new UIBox2(box.Right - width, y, box.Right, MathF.Min(y + step, box.Bottom)),
                AccentColor);
        }
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
}
