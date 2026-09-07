using Content.Client.Stylesheets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client._CMU14.UserInterface.Options;

/// <summary>
///     A collapsible group of options with a banded, clickable heading.
/// </summary>
/// <remarks>
///     <para>
///     A long settings tab reads as one undifferentiated column of text; giving each group a heading
///     you can fold makes the structure visible at a glance and lets a player collapse the parts they
///     don't care about.
///     </para>
///     <para>
///     Children written inside this control in XAML land in the collapsing body, not next to the
///     heading - see <see cref="XamlChildren"/> below, the same trick DefaultWindow uses to route its
///     children into Contents.
///     </para>
/// </remarks>
public sealed class CmuOptionSection : Control
{
    private const string ArrowExpanded = "▼";
    private const string ArrowCollapsed = "►";

    private readonly ContainerButton _header;
    private readonly Label _arrow;
    private readonly Label _title;
    private readonly BoxContainer _content;

    public string? Title
    {
        get => _title.Text;
        set => _title.Text = value;
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
            SeparationOverride = 2,
            HorizontalExpand = true,
        };
        AddChild(root);

        _header = new ContainerButton
        {
            HorizontalExpand = true,
            ToggleMode = false,
        };
        // Rules top and bottom, none at the sides. CrtLobbyTheme skips handing this the ordinary
        // CRT button box because it carries this class - see ApplyControl.
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
        _title.AddStyleClass(StyleNano.StyleClassLabelKeyText);
        headerRow.AddChild(_title);

        _content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 5,
            HorizontalExpand = true,
            Margin = new Thickness(0, 4, 0, 8),
        };
        root.AddChild(_content);

        _header.OnPressed += _ => Expanded = !Expanded;

        // Everything nested in XAML goes into the body rather than becoming a sibling of the heading.
        XamlChildren = _content.Children;

        // Closed to start: a tab of a dozen groups is legible as a list of headings, and you open
        // the one you came for. Set after XamlChildren so the arrow matches from the first frame.
        Expanded = false;
    }
}
