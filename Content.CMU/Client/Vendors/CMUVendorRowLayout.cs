using System.Numerics;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Vendors;

/// <summary>Reserves the icon and issue button before measuring the wrapping equipment description.</summary>
public sealed class CMUVendorRowLayout : Control
{
    private const float IconWidth = 48;
    private const float ActionWidth = 104;
    private const float Gap = 12;

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        if (ChildCount != 3)
            return base.MeasureOverride(availableSize);

        var icon = GetChild(0);
        var details = GetChild(1);
        var action = GetChild(2);
        icon.Measure(new Vector2(IconWidth, availableSize.Y));
        action.Measure(new Vector2(ActionWidth, availableSize.Y));
        var width = Math.Max(0, availableSize.X - IconWidth - ActionWidth - Gap * 2);
        details.Measure(new Vector2(width, availableSize.Y));

        return new Vector2(IconWidth + ActionWidth + Gap * 2 + details.DesiredSize.X,
            Math.Max(icon.DesiredSize.Y, Math.Max(details.DesiredSize.Y, action.DesiredSize.Y)));
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        if (ChildCount != 3)
            return base.ArrangeOverride(finalSize);

        var width = Math.Max(0, finalSize.X - IconWidth - ActionWidth - Gap * 2);
        GetChild(0).Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(IconWidth, finalSize.Y)));
        GetChild(1).Arrange(UIBox2.FromDimensions(new Vector2(IconWidth + Gap, 0), new Vector2(width, finalSize.Y)));
        GetChild(2).Arrange(UIBox2.FromDimensions(new Vector2(IconWidth + Gap * 2 + width, 0), new Vector2(ActionWidth, finalSize.Y)));
        return finalSize;
    }
}
