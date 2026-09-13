using System.Numerics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Chat.Widgets;

/// <summary>Measures both expanding chat panes at the same split widths used during arrangement.</summary>
public sealed class ChatPaneLayout : BoxContainer
{
    private bool BothVisible => ChildCount == 2 && Children[0].Visible && Children[1].Visible;
    private float Separation => SeparationOverride ?? StylePropertyDefault(StylePropertySeparation, 0);
    private float Main(Vector2 size) => Orientation == LayoutOrientation.Horizontal ? size.X : size.Y;
    private float Cross(Vector2 size) => Orientation == LayoutOrientation.Horizontal ? size.Y : size.X;
    private Vector2 SizeOnAxis(float main, float cross) => Orientation == LayoutOrientation.Horizontal
        ? new Vector2(main, cross)
        : new Vector2(cross, main);

    private float FirstShare(float available)
    {
        var first = Children[0];
        var second = Children[1];
        var ratio = first.SizeFlagsStretchRatio / (first.SizeFlagsStretchRatio + second.SizeFlagsStretchRatio);
        var minimum = MathF.Min(available, Main(first.DesiredSize));
        var maximum = MathF.Max(minimum, available - Main(second.DesiredSize));
        return Math.Clamp(available * ratio, minimum, maximum);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        if (!BothVisible || !float.IsFinite(Main(availableSize)))
            return base.MeasureOverride(availableSize);

        var available = MathF.Max(0, Main(availableSize) - Separation);
        var cross = Cross(availableSize);
        var first = FirstShare(available);
        Children[0].Measure(SizeOnAxis(first, cross));
        Children[1].Measure(SizeOnAxis(available - first, cross));

        // Showing "scroll to latest" can introduce a minimum width. Resolve it before arranging,
        // rather than making a horizontal BoxContainer repeatedly rewrap both panes afterward.
        var adjusted = FirstShare(available);
        if (adjusted != first)
        {
            Children[0].Measure(SizeOnAxis(adjusted, cross));
            Children[1].Measure(SizeOnAxis(available - adjusted, cross));
        }

        return SizeOnAxis(Main(Children[0].DesiredSize) + Main(Children[1].DesiredSize) + Separation,
            MathF.Max(Cross(Children[0].DesiredSize), Cross(Children[1].DesiredSize)));
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        if (!BothVisible)
            return base.ArrangeOverride(finalSize);

        MeasureOverride(finalSize);
        var available = MathF.Max(0, Main(finalSize) - Separation);
        var first = FirstShare(available);
        var cross = Cross(finalSize);
        Children[0].Arrange(UIBox2.FromDimensions(Vector2.Zero, SizeOnAxis(first, cross)));
        Children[1].Arrange(UIBox2.FromDimensions(SizeOnAxis(first + Separation, 0),
            SizeOnAxis(available - first, cross)));
        return finalSize;
    }
}
