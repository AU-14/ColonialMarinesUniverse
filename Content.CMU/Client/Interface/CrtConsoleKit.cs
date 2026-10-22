using Content.Client.Resources;
using Content.Shared.CMU14.ColonyEconomy;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Interface;

/// <summary>
///     Building blocks for console screens in the CRT terminal style: a dark screen behind a CRT tube, header and
///     footer strips, titled sections, status tags and wrapping text. Build the page into <see cref="Root"/> and add
///     <see cref="Tube"/> to the window.
/// </summary>
public sealed class CrtConsoleKit
{
    private const string MonoRegular = "/Fonts/RobotoMono/RobotoMono-Regular.ttf";
    private const string MonoBold = "/Fonts/RobotoMono/RobotoMono-Bold.ttf";

    public readonly Font Small;
    public readonly Font Body;
    public readonly Font Bold;
    public readonly Font Title;
    public readonly Font Huge;

    /// <summary>The padded column the page is built in.</summary>
    public readonly BoxContainer Root;

    /// <summary>The screen behind the CRT tube; add this to the window's contents.</summary>
    public readonly Control Tube;

    public CrtConsoleKit()
    {
        var cache = IoCManager.Resolve<IResourceCache>();
        Small = cache.GetFont(MonoRegular, 10);
        Body = cache.GetFont(MonoRegular, 12);
        Bold = cache.GetFont(MonoBold, 12);
        Title = cache.GetFont(MonoBold, 15);
        Huge = cache.GetFont(MonoBold, 24);

        var screen = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface0 },
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        Root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            Margin = new Thickness(10),
        };
        screen.AddChild(Root);

        // Reading surface, so no roll bar or grain.
        Tube = new Control { HorizontalExpand = true, VerticalExpand = true };
        Tube.AddChild(screen);
        Tube.AddChild(new CrtScreenControl
        {
            Source = screen,
            Phosphor = CrtTerminalPalette.Accent,
            Roll = false,
            Grain = false,
        });
    }

    /// <summary>The header strip: a heading and a subheading, with room on the right for <paramref name="right"/>.</summary>
    public PanelContainer Header(string heading, out Label subheading, Control? right = null)
    {
        var header = Strip(CrtTerminalPalette.Surface3);
        var row = Row();
        var titles = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        var title = Text(heading, Title, CrtTerminalPalette.TextBright);
        title.ClipText = true;
        titles.AddChild(title);
        subheading = Text(string.Empty, Small, CrtTerminalPalette.TextDim);
        subheading.ClipText = true;
        titles.AddChild(subheading);
        row.AddChild(titles);
        if (right != null)
            row.AddChild(right);
        header.AddChild(row);
        return header;
    }

    /// <summary>A footer strip with a single line of help text.</summary>
    public PanelContainer Footer(out Label text)
    {
        var footer = Strip(CrtTerminalPalette.Surface1);
        text = Text(string.Empty, Small, CrtTerminalPalette.TextDim);
        text.ClipText = true;
        footer.AddChild(text);
        return footer;
    }

    /// <summary>A coloured status tag, like "ACTIVE". Set its colour with <see cref="SetTag"/>.</summary>
    public PanelContainer Tag(out Label text)
    {
        var tag = new PanelContainer { VerticalAlignment = Control.VAlignment.Center };
        text = Text(string.Empty, Bold, CrtTerminalPalette.Void);
        text.Margin = new Thickness(8, 3);
        tag.AddChild(text);
        return tag;
    }

    public static void SetTag(PanelContainer tag, Label text, string value, Color color)
    {
        text.Text = value;
        tag.PanelOverride = new StyleBoxFlat { BackgroundColor = color };
    }

    /// <summary>A small tag for inside a section or row.</summary>
    public PanelContainer Chip(string text, Color color)
    {
        var chip = new PanelContainer
        {
            VerticalAlignment = Control.VAlignment.Center,
            PanelOverride = new StyleBoxFlat { BackgroundColor = color },
        };
        var label = Text(text, Small, CrtTerminalPalette.Void);
        label.Margin = new Thickness(6, 1);
        chip.AddChild(label);
        return chip;
    }

    /// <summary>A titled block; returns the column its contents go in.</summary>
    public PanelContainer Section(string title, out BoxContainer column, Color? accent = null, Control? right = null)
    {
        var panel = new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = CrtTerminalPalette.Surface1,
                ContentMarginLeftOverride = 10,
                ContentMarginRightOverride = 10,
                ContentMarginTopOverride = 6,
                ContentMarginBottomOverride = 8,
            },
        };
        column = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
        var head = Row();
        var heading = Text(title.ToUpperInvariant(), Bold, accent ?? CrtTerminalPalette.Accent);
        heading.ClipText = true;
        heading.HorizontalExpand = true;
        heading.VerticalAlignment = Control.VAlignment.Center;
        head.AddChild(heading);
        if (right != null)
            head.AddChild(right);
        column.AddChild(head);
        column.AddChild(new PanelContainer
        {
            MinHeight = 1,
            PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Line },
        });
        panel.AddChild(column);
        return panel;
    }

    /// <summary>A label and value on one line; the value wraps if it's long.</summary>
    public BoxContainer Field(string label, string value, Color? color = null, int labelWidth = 130)
    {
        var row = Row();
        var key = Text(label.ToUpperInvariant(), Small, CrtTerminalPalette.TextDim);
        key.MinWidth = labelWidth;
        key.VerticalAlignment = Control.VAlignment.Top;
        row.AddChild(key);
        row.AddChild(Wrapping(value, color ?? CrtTerminalPalette.TextBright));
        return row;
    }

    public Button Button(string text, CmuButtonStyles.Variant variant = CmuButtonStyles.Variant.Neutral)
    {
        var button = new Button { Text = text };
        CmuButtonStyles.Apply(button, variant);
        button.Label.FontOverride = Small;
        return button;
    }

    public static PanelContainer Strip(Color color)
    {
        return new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = color,
                ContentMarginLeftOverride = 10,
                ContentMarginRightOverride = 10,
                ContentMarginTopOverride = 6,
                ContentMarginBottomOverride = 6,
            },
        };
    }

    public static BoxContainer Row(int separation = 8)
    {
        return new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = separation };
    }

    public static BoxContainer Column(int separation = 4)
    {
        return new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = separation };
    }

    public static Label Text(string text, Font font, Color color)
    {
        return new Label { Text = text, FontOverride = font, FontColorOverride = color };
    }

    /// <summary>Text that wraps to the available width, shown exactly as written.</summary>
    public static RichTextLabel Wrapping(string text, Color color)
    {
        var message = new FormattedMessage();
        message.PushColor(color);
        message.AddText(text);
        message.Pop();
        var label = new RichTextLabel { HorizontalExpand = true };
        label.SetMessage(message);
        return label;
    }

    /// <summary>A bold heading that wraps onto more lines instead of running past the edge of its panel.</summary>
    public static RichTextLabel BoldWrapping(string text, Color color)
    {
        var message = new FormattedMessage();
        message.PushColor(color);
        message.AddMarkupOrThrow($"[bold]{FormattedMessage.EscapeText(text)}[/bold]");
        message.Pop();
        var label = new RichTextLabel { HorizontalExpand = true };
        label.SetMessage(message);
        return label;
    }

    /// <summary>Wrapping text that keeps any markup it was written with, falling back to plain text if it's broken.</summary>
    public static RichTextLabel WrappingMarkup(string markup, Color color)
    {
        var message = new FormattedMessage();
        message.PushColor(color);
        message.AddMarkupPermissive(markup);
        message.Pop();
        var label = new RichTextLabel { HorizontalExpand = true };
        label.SetMessage(message);
        return label;
    }

    /// <summary>
    ///     Fills <paramref name="column"/> with the colony's economy at a glance: the three rates as readouts, then
    ///     any embargoes and trade pacts.
    /// </summary>
    public void EconomyOverview(BoxContainer column, EconomyStatusState econ)
    {
        column.RemoveAllChildren();
        var rates = new GridContainer { Columns = 3, HSeparationOverride = 6, HorizontalExpand = true };
        rates.AddChild(Readout(Loc.GetString("cmu-economy-sales-tax"), $"{MathF.Round(econ.SalesTaxPercent):0}%"));
        rates.AddChild(Readout(Loc.GetString("cmu-economy-income-tax"), $"{MathF.Round(econ.IncomeTaxPercent):0}%"));
        rates.AddChild(Readout(Loc.GetString("cmu-economy-transit-tariff"), $"{MathF.Round(econ.TransitTariffPercent):0}%"));
        column.AddChild(rates);

        column.AddChild(Field(Loc.GetString("cmu-economy-embargoes"),
            econ.ActiveEmbargoes.Count > 0 ? string.Join(", ", econ.ActiveEmbargoes) : Loc.GetString("cmu-economy-none"),
            econ.ActiveEmbargoes.Count > 0 ? CrtTerminalPalette.Alert : CrtTerminalPalette.TextDim));
        column.AddChild(Field(Loc.GetString("cmu-economy-trade-pacts"),
            econ.ActiveTradePacts.Count > 0 ? string.Join(", ", econ.ActiveTradePacts) : Loc.GetString("cmu-economy-none"),
            econ.ActiveTradePacts.Count > 0 ? CrtTerminalPalette.Accent : CrtTerminalPalette.TextDim));
    }

    /// <summary>A small block with a caption over a large value, like a rate or a total.</summary>
    public PanelContainer Readout(string caption, string value, Color? color = null)
    {
        return Readout(caption, value, out _, color);
    }

    /// <inheritdoc cref="Readout(string, string, Color?)"/>
    public PanelContainer Readout(string caption, string value, out Label valueLabel, Color? color = null)
    {
        var panel = new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = CrtTerminalPalette.Surface2,
                ContentMarginLeftOverride = 8,
                ContentMarginRightOverride = 8,
                ContentMarginTopOverride = 4,
                ContentMarginBottomOverride = 4,
            },
        };
        var column = Column(0);
        var name = Text(caption.ToUpperInvariant(), Small, CrtTerminalPalette.TextDim);
        name.ClipText = true;
        column.AddChild(name);
        valueLabel = Text(value, Title, color ?? CrtTerminalPalette.TextBright);
        valueLabel.ClipText = true;
        column.AddChild(valueLabel);
        panel.AddChild(column);
        return panel;
    }

    /// <summary>A money amount, without decimals.</summary>
    public static string Money(float amount)
    {
        return "$" + MathF.Round(amount).ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
    }
}
