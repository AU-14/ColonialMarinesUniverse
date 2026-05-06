using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._CMU14.Medical.Surgery;

internal static class CMUMedicalMachineStyle
{
    public static readonly Color WindowBg = Color.FromHex("#0B1117");
    public static readonly Color Surface = Color.FromHex("#121318");
    public static readonly Color CardBg = Color.FromHex("#181A20");
    public static readonly Color DeepCardBg = Color.FromHex("#10191E");
    public static readonly Color RowBg = Color.FromHex("#121D23");
    public static readonly Color Border = Color.FromHex("#333642");
    public static readonly Color MutedBorder = Color.FromHex("#263A42");
    public static readonly Color Text = Color.FromHex("#F2EEE7");
    public static readonly Color Muted = Color.FromHex("#B7B0A5");
    public static readonly Color Dim = Color.FromHex("#6F7D89");
    public static readonly Color Warning = Color.FromHex("#D2A95D");
    public static readonly Color Blue = Color.FromHex("#5B88B0");
    public static readonly Color Cyan = Color.FromHex("#62B6A8");
    public static readonly Color Purple = Color.FromHex("#6A5B82");
    public static readonly Color Red = Color.FromHex("#B64646");

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

    public static Button ActionButton(string text, Color accent)
    {
        var button = new Button
        {
            HorizontalExpand = true,
            MinHeight = 34,
        };

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            Margin = new Thickness(7, 5),
            HorizontalExpand = true,
        };

        row.AddChild(new PanelContainer
        {
            MinSize = new Vector2(5, 24),
            PanelOverride = Flat(accent, accent),
        });

        row.AddChild(new Label
        {
            Text = text,
            FontColorOverride = Text,
            ClipText = true,
            HorizontalExpand = true,
            VerticalAlignment = Control.VAlignment.Center,
        });

        var panel = Panel(DeepCardBg, accent);
        panel.AddChild(row);
        button.AddChild(panel);
        return button;
    }

    public static BoxContainer MakeTitledList(
        BoxContainer parent,
        string title,
        float width,
        bool expand = false,
        string? subtitle = null)
    {
        var panel = Panel(CardBg, Border);
        panel.MinWidth = width;
        panel.HorizontalExpand = expand;
        panel.VerticalExpand = true;
        parent.AddChild(panel);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8),
            SeparationOverride = 6,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        panel.AddChild(root);

        var header = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };
        header.AddChild(new Label
        {
            Text = title,
            StyleClasses = { "LabelHeading" },
            FontColorOverride = Text,
            ClipText = true,
        });

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            header.AddChild(new Label
            {
                Text = subtitle,
                StyleClasses = { "LabelSubText" },
                FontColorOverride = Muted,
                ClipText = true,
            });
        }

        root.AddChild(header);

        var scroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        root.AddChild(scroll);

        var list = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 5,
            HorizontalExpand = true,
        };
        scroll.AddChild(list);
        return list;
    }

    public static Control Empty(string text)
    {
        return Wrap(new Label
        {
            Text = text,
            ClipText = true,
            FontColorOverride = Dim,
        }, DeepCardBg, MutedBorder);
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
            MinSize = new Vector2(5, 34),
            PanelOverride = Flat(accent, accent),
        });

        var text = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalAlignment = Control.VAlignment.Center,
        };
        row.AddChild(text);

        text.AddChild(new Label
        {
            Text = title,
            StyleClasses = { "LabelSubText" },
            FontColorOverride = Muted,
            ClipText = true,
        });

        text.AddChild(new Label
        {
            Text = value,
            StyleClasses = { "LabelKeyText" },
            FontColorOverride = Text,
            ClipText = true,
        });

        if (!string.IsNullOrWhiteSpace(detail))
        {
            text.AddChild(new Label
            {
                Text = detail,
                StyleClasses = { "LabelSubText" },
                FontColorOverride = Dim,
                ClipText = true,
            });
        }

        return Wrap(row, DeepCardBg, MutedBorder, new Thickness(7, 5));
    }
}
