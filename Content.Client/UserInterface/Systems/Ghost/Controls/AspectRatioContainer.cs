using System;
using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client.UserInterface.Systems.Ghost.Controls;

public sealed class AspectRatioContainer : Container
{
    public float Ratio { get; set; } = 1f;

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var fitted = FitSize(availableSize);
        var desired = Vector2.Zero;

        foreach (var child in Children)
        {
            if (!child.Visible && !child.ReservesSpace)
                continue;

            child.Measure(fitted);
            desired = Vector2.Max(desired, child.DesiredSize);
        }

        return desired;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var fitted = FitSize(finalSize);
        var offset = (finalSize - fitted) / 2f;

        foreach (var child in Children)
        {
            if (!child.Visible && !child.ReservesSpace)
                continue;

            child.Arrange(UIBox2.FromDimensions(offset, fitted));
        }

        return finalSize;
    }

    private Vector2 FitSize(Vector2 availableSize)
    {
        if (Ratio <= 0 || !float.IsFinite(Ratio))
            return availableSize;

        var width = availableSize.X;
        var height = availableSize.Y;

        if (!float.IsFinite(width) && !float.IsFinite(height))
            return Vector2.Zero;

        if (!float.IsFinite(width))
            width = height * Ratio;

        if (!float.IsFinite(height))
            height = width / Ratio;

        if (width <= 0 || height <= 0)
            return Vector2.Zero;

        if (width / height > Ratio)
            width = height * Ratio;
        else
            height = width / Ratio;

        return new Vector2(MathF.Floor(width), MathF.Floor(height));
    }
}
