using Robust.Client.Graphics;
using Robust.Shared.Maths;

namespace Content.Client.CMU14.Interface;

/// <summary>
///     No fill, no frame - just a dashed line along the top edge, like a printed tear-off rule.
/// </summary>
public sealed class CmuDashedRuleStyleBox : StyleBox
{
    public Color Color { get; set; }
    public float DashLength { get; set; } = 4;
    public float GapLength { get; set; } = 3;
    public float Thickness { get; set; } = 1;

    protected override void DoDraw(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        var dash = MathF.Max(1, DashLength * uiScale);
        var step = dash + MathF.Max(1, GapLength * uiScale);

        var bottom = box.Top + MathF.Max(1, Thickness * uiScale);

        for (var x = box.Left; x < box.Right; x += step)
        {
            handle.DrawRect(new UIBox2(x, box.Top, MathF.Min(x + dash, box.Right), bottom), Color);
        }
    }

    protected override float GetDefaultContentMargin(Margin margin)
    {
        return margin == Margin.Top ? Thickness : 0;
    }
}
