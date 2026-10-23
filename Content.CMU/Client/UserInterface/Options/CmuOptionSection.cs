using Content.Client.CMU14.Interface;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client.CMU14.UserInterface.Options;

/// <summary>
///     A collapsible group of options under a light, clickable heading.
/// </summary>
public sealed class CmuOptionSection : Control
{
    private const string ArrowExpanded = "▼";
    private const string ArrowCollapsed = "►";

    private const int HeadingCrtSize = 8;
    private const int HeadingNotoSize = 10;

    private readonly ContainerButton _header;
    private readonly Label _arrow;
    private readonly Label _title;
    private readonly BoxContainer _content;
    private readonly IResourceCache _resourceCache = IoCManager.Resolve<IResourceCache>();

    private string? _titleText;
    private bool _hovered;

    public string? Title
    {
        get => _titleText;
        set
        {
            _titleText = value;
            // Uppercased here, not in the loc strings, so CmuOptionsFilter still matches the original.
            _title.Text = value?.ToUpperInvariant();
        }
    }

    public bool Expanded
    {
        get => _content.Visible;
        set
        {
            _content.Visible = value;
            _arrow.Text = value ? ArrowExpanded : ArrowCollapsed;
        }
    }

    /// <summary>
    ///     The options inside the collapsing body, in order. Read by <see cref="CmuOptionsFilter"/>.
    /// </summary>
    public IEnumerable<Control> Options => _content.Children;

    /// <summary>
    ///     Adds an option to the collapsing body. XAML children route there automatically, but tabs
    ///     that build their rows in code (the keybind tab) need this.
    /// </summary>
    public void AddOption(Control child)
    {
        _content.AddChild(child);
    }

    public CmuOptionSection()
    {
        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 0,
            HorizontalExpand = true,
        };
        AddChild(root);

        _header = new ContainerButton
        {
            HorizontalExpand = true,
            ToggleMode = false,
        };
        _header.AddStyleClass(StyleNano.StyleClassCrtSectionHeader);
        root.AddChild(_header);

        var headerRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };
        _header.AddChild(headerRow);

        _arrow = new Label { Text = ArrowExpanded, VerticalAlignment = VAlignment.Center };
        headerRow.AddChild(_arrow);

        _title = new Label { VerticalAlignment = VAlignment.Center };
        headerRow.AddChild(_title);

        _content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 5,
            HorizontalExpand = true,
            // The bottom margin is the gap before the next heading.
            Margin = new Thickness(0, 5, 0, 12),
        };
        root.AddChild(_content);

        _header.OnPressed += _ => Expanded = !Expanded;
        _header.OnMouseEntered += _ => SetHovered(true);
        _header.OnMouseExited += _ => SetHovered(false);

        // Everything nested in XAML goes into the body rather than becoming a sibling of the heading.
        XamlChildren = _content.Children;

        // Open to start.
        Expanded = true;

        Restyle();
    }

    /// <summary>
    ///     Re-reads every heading's colours and face under <paramref name="root"/>. Call after a
    ///     theme change - see the class remarks.
    /// </summary>
    public static void RestyleAll(Control root)
    {
        if (root is CmuOptionSection section)
            section.Restyle();

        foreach (var child in root.Children)
        {
            RestyleAll(child);
        }
    }

    private void Restyle()
    {
        _header.StyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = Color.Transparent,
            BorderColor = CrtTerminalPalette.Line,
            BorderThickness = new Thickness(0, 0, 0, 1),
            ContentMarginLeftOverride = 1,
            ContentMarginRightOverride = 0,
            ContentMarginTopOverride = 2,
            ContentMarginBottomOverride = 3,
        };

        // The OSD face under the CRT theme, bold Noto without it.
        var font = StyleNano.CrtUiEnabled
            ? StyleNano.GetCrtFont(_resourceCache, HeadingCrtSize)
            : _resourceCache.NotoStack(variation: "Bold", size: HeadingNotoSize);

        _title.FontOverride = font;
        _arrow.FontOverride = font;
        ApplyHoverColour();
    }

    private void SetHovered(bool hovered)
    {
        _hovered = hovered;
        ApplyHoverColour();
    }

    private void ApplyHoverColour()
    {
        var colour = _hovered ? CrtTerminalPalette.Text : CrtTerminalPalette.TextDim;
        _title.FontColorOverride = colour;
        _arrow.FontColorOverride = colour;
    }
}
