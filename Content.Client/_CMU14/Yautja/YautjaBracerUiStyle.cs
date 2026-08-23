using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._CMU14.Yautja;

internal static class YautjaBracerUiStyle
{
    public static readonly Color WindowBg = Color.FromHex("#070101");
    public static readonly Color Surface = Color.FromHex("#100303");
    public static readonly Color Card = Color.FromHex("#180505");
    public static readonly Color DeepCard = Color.FromHex("#090101");
    public static readonly Color Row = Color.FromHex("#220707");
    public static readonly Color Border = Color.FromHex("#6B1616");
    public static readonly Color MutedBorder = Color.FromHex("#3C0B0B");
    public static readonly Color Text = Color.FromHex("#F8E5E1");
    public static readonly Color Muted = Color.FromHex("#C6948E");
    public static readonly Color Dim = Color.FromHex("#8C5752");
    public static readonly Color Red = Color.FromHex("#C62D2D");
    public static readonly Color HotRed = Color.FromHex("#FF4B3F");
    public static readonly Color Amber = Color.FromHex("#D14A34");
    public static readonly Color Green = Color.FromHex("#B73131");
    public static readonly Color Purple = Color.FromHex("#8E2424");

    public static StyleBoxFlat Flat(Color background, Color border, Thickness? borderThickness = null)
    {
        return new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border,
            BorderThickness = borderThickness ?? new Thickness(1),
        };
    }

    public static PanelContainer Panel(Color background, Color border, Thickness? borderThickness = null)
    {
        return new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = Flat(background, border, borderThickness),
        };
    }

    public static Label Label(string text, Color? color = null, string? styleClass = null)
    {
        var label = new Label
        {
            Text = text,
            FontColorOverride = color ?? Text,
            ClipText = true,
        };

        if (styleClass != null)
            label.StyleClasses.Add(styleClass);

        return label;
    }

    public static PanelContainer Section(string title, out BoxContainer body, Color? accent = null)
    {
        var panel = Panel(Card, accent ?? Border, new Thickness(1));
        body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 7,
            Margin = new Thickness(9),
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        body.AddChild(Label(title, accent ?? HotRed, "LabelHeading"));
        panel.AddChild(body);
        return panel;
    }

    public static Control Metric(string title, string value, Color accent, string? detail = null)
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 7,
            HorizontalExpand = true,
        };

        row.AddChild(new PanelContainer
        {
            MinSize = new Vector2(5, 36),
            PanelOverride = Flat(accent, accent),
        });

        var text = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalAlignment = Control.VAlignment.Center,
            HorizontalExpand = true,
        };
        row.AddChild(text);

        text.AddChild(Label(title, Muted, "LabelSubText"));
        text.AddChild(Label(value, Text, "LabelKeyText"));

        if (!string.IsNullOrWhiteSpace(detail))
            text.AddChild(Label(detail, Dim, "LabelSubText"));

        return Wrap(row, DeepCard, MutedBorder, new Thickness(7, 5));
    }

    public static Button ActionButton(string title, string detail, Color accent, out Label titleLabel, out Label detailLabel)
    {
        var button = new Button
        {
            HorizontalExpand = true,
            MinHeight = 46,
            StyleBoxOverride = Flat(Color.Transparent, Color.Transparent, new Thickness(0)),
        };

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 7,
            Margin = new Thickness(7, 6),
            HorizontalExpand = true,
        };

        row.AddChild(new PanelContainer
        {
            MinSize = new Vector2(5, 32),
            PanelOverride = Flat(accent, accent),
        });

        var text = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalAlignment = Control.VAlignment.Center,
        };
        row.AddChild(text);

        titleLabel = Label(title, Text, "LabelKeyText");
        detailLabel = Label(detail, Muted, "LabelSubText");
        text.AddChild(titleLabel);
        text.AddChild(detailLabel);

        var panel = Panel(DeepCard, accent);
        panel.AddChild(row);
        button.AddChild(panel);
        return button;
    }

    public static Button CloseButton()
    {
        return new Button
        {
            Text = "X",
            HorizontalExpand = false,
            VerticalAlignment = Control.VAlignment.Top,
            MinWidth = 32,
            MinHeight = 30,
            SetHeight = 30,
            StyleBoxOverride = Flat(DeepCard, HotRed),
        };
    }

    public static PanelContainer Wrap(
        Control child,
        Color background,
        Color border,
        Thickness? margin = null,
        Thickness? borderThickness = null)
    {
        var panel = Panel(background, border, borderThickness);
        child.Margin = margin ?? new Thickness(7, 5);
        panel.AddChild(child);
        return panel;
    }

    public static Control Empty(string text)
    {
        return Wrap(Label(text, Dim), DeepCard, MutedBorder);
    }
}
