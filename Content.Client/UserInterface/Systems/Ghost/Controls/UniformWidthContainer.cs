using System;
using System.Linq;
using System.Numerics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client.UserInterface.Systems.Ghost.Controls;

public sealed class UniformWidthContainer : Container
{
    public int? SeparationOverride { get; set; }

    private int ActualSeparation => SeparationOverride ?? 0;

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var visibleChildren = Children
            .Where(child => child.Visible || child.ReservesSpace)
            .ToList();
        var visibleCount = visibleChildren.Count;
        if (visibleCount == 0)
            return Vector2.Zero;

        var separation = ActualSeparation * (visibleCount - 1);
        var maxHeight = 0f;
        var minChildWidth = 0f;

        foreach (var child in visibleChildren)
        {
            child.Measure(new Vector2(float.PositiveInfinity, availableSize.Y));
            maxHeight = Math.Max(maxHeight, child.DesiredSize.Y);
            minChildWidth = Math.Max(minChildWidth, child.DesiredSize.X);
        }

        if (float.IsFinite(availableSize.X))
        {
            var fillChildWidth = Math.Max(0, (availableSize.X - separation) / visibleCount);
            minChildWidth = Math.Max(minChildWidth, fillChildWidth);
        }

        return new Vector2((minChildWidth * visibleCount) + separation, maxHeight);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var visibleChildren = Children
            .Where(child => child.Visible || child.ReservesSpace)
            .ToList();
        var visibleCount = visibleChildren.Count;
        if (visibleCount == 0)
            return finalSize;

        var separation = ActualSeparation * (visibleCount - 1);
        var minChildWidth = visibleChildren.Max(child => child.DesiredSize.X);
        var childWidth = Math.Max(minChildWidth, (finalSize.X - separation) / visibleCount);
        var offset = 0f;

        foreach (var child in visibleChildren)
        {
            child.Arrange(UIBox2.FromDimensions(offset, 0, childWidth, finalSize.Y));
            offset += childWidth + ActualSeparation;
        }

        return finalSize;
    }
}
