using Robust.Client.Graphics;
using Robust.Shared.Maths;

namespace Content.Client.CMU14.Interface;

/// <summary>
///     A filled key with a separately coloured top and bottom edge. <see cref="StyleBoxFlat"/> has one
///     border colour, which can't draw a bevel.
/// </summary>
public sealed class CmuBevelStyleBox : StyleBox
{
    public Color BackgroundColor { get; set; }
    public Color TopColor { get; set; }
    public Color BottomColor { get; set; }
    public float TopThickness { get; set; } = 1;
    public float BottomThickness { get; set; } = 2;

    protected override void DoDraw(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        handle.DrawRect(box, BackgroundColor);

        var top = TopThickness * uiScale;
        if (top > 0)
            handle.DrawRect(new UIBox2(box.Left, box.Top, box.Right, box.Top + top), TopColor);

        var bottom = BottomThickness * uiScale;
        if (bottom > 0)
            handle.DrawRect(new UIBox2(box.Left, box.Bottom - bottom, box.Right, box.Bottom), BottomColor);
    }

    protected override float GetDefaultContentMargin(Margin margin)
    {
        return margin switch
        {
            Margin.Top => TopThickness,
            Margin.Bottom => BottomThickness,
            _ => 0,
        };
    }
}
