using System.Linq;
using Content.Client.Resources;
using Content.Shared._RMC14.Telephone;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.Telephone;

/// <summary>
///     Shared pieces for the in-world phone screens: fonts, flat boxes with borders, buttons that look like physical
///     keys, and the directory's category order.
/// </summary>
public static class CMUPhoneStyle
{
    /// <summary>The order directory tabs and sections appear in; anything else follows alphabetically.</summary>
    public static readonly string[] CategoryOrder =
        ["MP Dept.", "Warship", "USS George W. Bush", "Command", "Offices", "Apollo", "Dropship", "Marine", "Payphones"];

    public static Font Font(string path, int size)
    {
        return IoCManager.Resolve<IResourceCache>().GetFont(path, size);
    }

    public const string Sans = "/Fonts/NotoSans/NotoSans-Regular.ttf";
    public const string SansBold = "/Fonts/NotoSans/NotoSans-Bold.ttf";
    public const string Mono = "/Fonts/RobotoMono/RobotoMono-Regular.ttf";
    public const string MonoBold = "/Fonts/RobotoMono/RobotoMono-Bold.ttf";

    public static StyleBoxFlat Box(Color background, Color? border = null, int borderThickness = 0, int padH = 0, int padV = 0)
    {
        return new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border ?? background,
            BorderThickness = new Thickness(borderThickness),
            ContentMarginLeftOverride = padH,
            ContentMarginRightOverride = padH,
            ContentMarginTopOverride = padV,
            ContentMarginBottomOverride = padV,
        };
    }

    public static PanelContainer Panel(Color background, Color? border = null, int borderThickness = 0, int padH = 0, int padV = 0)
    {
        return new PanelContainer { PanelOverride = Box(background, border, borderThickness, padH, padV) };
    }

    public static Label Text(string text, Font font, Color color)
    {
        return new Label { Text = text, FontOverride = font, FontColorOverride = color };
    }

    /// <summary>
    ///     A button drawn as a physical key: a solid face with an edge, which lightens while the mouse is over it.
    /// </summary>
    public static Button Key(string text, Font font, Color face, Color edge, Color ink, int minWidth = 0)
    {
        var button = new Button { Text = text, MinWidth = minWidth };
        var normal = Box(face, edge, 1, 10, 4);
        var hover = Box(Color.InterpolateBetween(face, Color.White, 0.12f), edge, 1, 10, 4);
        var disabled = Box(face.WithAlpha(0.45f), edge.WithAlpha(0.45f), 1, 10, 4);
        button.StyleBoxOverride = normal;
        button.Label.FontOverride = font;
        button.Label.FontColorOverride = ink;
        button.Label.HorizontalExpand = true;
        button.Label.Align = Label.AlignMode.Center;
        button.OnMouseEntered += _ =>
        {
            if (!button.Disabled)
                button.StyleBoxOverride = hover;
        };
        button.OnMouseExited += _ => button.StyleBoxOverride = button.Disabled ? disabled : normal;
        return button;
    }

    /// <summary>The directory's phones grouped by category, in <see cref="CategoryOrder"/>, names sorted.</summary>
    public static List<(string Category, List<RMCPhone> Phones)> Group(IEnumerable<RMCPhone> phones)
    {
        return phones
            .GroupBy(p => p.Category)
            .OrderBy(g => Array.IndexOf(CategoryOrder, g.Key) is var i && i >= 0 ? i : CategoryOrder.Length)
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => (g.Key, g.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList()))
            .ToList();
    }
}
