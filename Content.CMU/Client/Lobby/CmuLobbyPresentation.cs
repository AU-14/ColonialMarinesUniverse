using System.Numerics;
using System.Linq;
using Content.Client.Stylesheets;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using Robust.Shared.Configuration;

namespace Content.Client.CMU14.Lobby;

/// <summary>Presentation shared by character setup and deployment. Gameplay choices remain in their controllers.</summary>
public static partial class CmuLobbyPresentation
{
    public static void UseVerticalScrolling(Control root)
    {
        if (root is ScrollContainer scroll)
            scroll.HScrollEnabled = false;
        if (root is LineEdit input)
            StyleInput(input);
        foreach (var child in root.Children)
            UseVerticalScrolling(child);
    }

    public static readonly Color Text = Color.FromHex("#E6DCE7");
    public static readonly Color Muted = Color.FromHex("#D1CBD6");

    public static void StyleInput(LineEdit input)
    {
        input.StyleBoxOverride = new CmuInputStyleBox(input)
        {
            ContentMarginLeftOverride = 10, ContentMarginRightOverride = 10,
            ContentMarginTopOverride = 8, ContentMarginBottomOverride = 8,
        };
        input.MinHeight = 38;
        input.HorizontalExpand = true;
        input.AddStyleClass(StyleNano.StyleClassCrtLineEdit);
    }

    public static Control TextArea(TextEdit input)
    {
        input.HorizontalExpand = true;
        var field = new PanelContainer
        {
            PanelOverride = new CmuInputStyleBox(input)
            {
                ContentMarginLeftOverride = 12, ContentMarginRightOverride = 12,
                ContentMarginTopOverride = 12, ContentMarginBottomOverride = 12,
            },
            HorizontalExpand = true,
        };
        input.Orphan();
        field.AddChild(input);
        return field;
    }

    public static Color FactionColor(string faction) => Color.FromHex(faction switch
    {
        "colonists" => "#D3BA91",
        "govfor" => "#A9C4B1",
        "opfor" => "#D5A0A4",
        _ => "#B9B0DB",
    });

    public static StyleBox Box(Color? accent = null, Color? fill = null, int padding = 12) => new CmuLobbyStyleBox(accent, fill, false)
    {
        ContentMarginLeftOverride = padding,
        ContentMarginRightOverride = padding,
        ContentMarginTopOverride = padding,
        ContentMarginBottomOverride = padding,
    };

    public static Label Heading(string text) => new()
    {
        Text = text,
        FontColorOverride = Text,
        StyleClasses = { StyleNano.StyleClassCrtHeading },
        Margin = new Thickness(0, 4, 0, 8),
    };

    public static Button Action(string text, Color? accent = null)
    {
        var button = new Button { Text = text, MinHeight = 38, HorizontalExpand = true };
        button.ToolTip = text;
        Style(button, accent);
        return button;
    }

    public static Button Card(string title, string caption, Color? accent = null)
    {
        var button = Action(string.Empty, accent);
        button.MinHeight = 92;
        var column = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8, MouseFilter = Control.MouseFilterMode.Ignore };
        column.AddChild(new CmuAccentLabel(accent) { Text = title, ClipText = true });
        column.AddChild(new Label { Text = caption, FontColorOverride = Muted, ClipText = true });
        button.AddChild(column);
        return button;
    }

    public static void Style(Button button, Color? accent = null, bool selected = false)
    {
        button.StyleBoxOverride = new CmuLobbyStyleBox(accent, null, selected, button)
        {
            ContentMarginLeftOverride = 9,
            ContentMarginRightOverride = 9,
            ContentMarginTopOverride = 9,
            ContentMarginBottomOverride = 9,
        };
        button.Label.FontColorOverride = selected ? Color.White : Text;
        button.Label.HorizontalExpand = true;
        button.Label.Align = Label.AlignMode.Center;
    }

    public static void SetMarkingCardContent(Button button, Control preview, string title)
    {
        button.Label.Orphan();
        button.MinWidth = 150;
        button.MinHeight = 150;
        button.ToolTip = title;
        var content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            MouseFilter = Control.MouseFilterMode.Ignore,
        };
        content.AddChild(preview);
        var name = new RichTextLabel
        {
            HorizontalExpand = true,
            MaxWidth = 170,
            MouseFilter = Control.MouseFilterMode.Ignore,
        };
        name.SetMessage(title, Text);
        content.AddChild(name);
        button.AddChild(content);
    }
}

/// <summary>Updates only its text tone when the palette changes, without rebuilding the control tree.</summary>
public sealed class CmuAccentLabel : Label
{
    private Color? _accent;
    private Color? _theme;

    public Color? SemanticColor
    {
        get => _accent;
        set
        {
            if (_accent == value) return;
            _accent = value;
            _theme = null;
        }
    }

    public CmuAccentLabel(Color? accent = null)
    {
        _accent = accent;
        FontColorOverride = accent is { } semantic ? CmuLobbyPresentation.ThemedAccent(semantic) : CmuLobbyPresentation.Accent;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        var theme = CmuLobbyPresentation.Accent;
        if (_theme == theme)
            return;
        _theme = theme;
        FontColorOverride = _accent is { } semantic ? CmuLobbyPresentation.ThemedAccent(semantic) : theme;
    }
}

/// <summary>Entrance belongs to opening a surface, never to changes inside that surface.</summary>
public sealed partial class CmuRevealPanel : PanelContainer
{
    [Dependency] private IConfigurationManager _configuration = default!;
    private float _elapsed = 1;
    public CmuRevealPanel() => IoCManager.InjectDependencies(this);
    public void Reveal()
    {
        if (Visible)
            return;
        Visible = true;
        _elapsed = 0;
    }

    public void Enter() => _elapsed = 0;

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_elapsed >= 0.22f)
            return;
        _elapsed = _configuration.GetCVar(CCVars.ReducedMotion) ? 0.22f : Math.Min(0.22f, _elapsed + args.DeltaSeconds);
        var t = _elapsed / 0.22f;
        var eased = 1 - MathF.Pow(1 - t, 3);
        Modulate = Color.White.WithAlpha(eased);
        Margin = new Thickness(0, 10 * (1 - eased), 0, 0);
    }
}

/// <summary>A fact tile opens the original option controls in an inline, searchable choice surface.</summary>
public sealed class CmuFactButton : Button
{
    private readonly OptionButton _source;
    private readonly Label _value;
    private int _selected = int.MinValue;
    private int _count = -1;

    public CmuFactButton(string title, OptionButton source, Action<OptionButton, string> open)
    {
        _source = source;
        MinHeight = 68;
        HorizontalExpand = true;
        CmuLobbyPresentation.Style(this);
        var column = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6, MouseFilter = MouseFilterMode.Ignore };
        column.AddChild(new Label { Text = title, FontColorOverride = CmuLobbyPresentation.Muted, ClipText = true });
        _value = new Label { FontColorOverride = CmuLobbyPresentation.Text, ClipText = true };
        column.AddChild(_value);
        AddChild(column);
        OnPressed += _ => open(source, title);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        Disabled = _source.Disabled || _source.ItemCount == 0;
        if (_selected == _source.SelectedId && _count == _source.ItemCount)
            return;
        _selected = _source.SelectedId;
        _count = _source.ItemCount;
        for (var i = 0; i < _source.ItemCount; i++)
        {
            if (_source.GetItemId(i) == _selected)
                _value.Text = _source.GetChild(0).Children.OfType<Label>().First().Text;
        }
    }
}
