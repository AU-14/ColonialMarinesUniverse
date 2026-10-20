using System.Numerics;
using Content.Client.CMU14.Interface;
using Content.Shared.CMU14.FactionRoster;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.FactionRoster;

/// <summary>
///     One person's dossier from the roster, in the CRT theme. Everything players write themselves goes in wrapping
///     text inside one scrolling column, so a description of any length fits.
/// </summary>
public sealed class FactionRosterEntryWindow : DefaultWindow
{
    private readonly RosterFonts _fonts = new();
    private readonly BoxContainer _body;

    public FactionRosterEntryWindow(FactionRosterEntry entry)
    {
        Title = entry.Name;
        MinSize = new Vector2(440, 480);
        SetSize = new Vector2(540, 720);

        var screen = RosterUi.Screen(out var root);

        var header = RosterUi.Strip(CrtTerminalPalette.Surface3);
        var titles = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        var name = RosterUi.Text(entry.Name.ToUpperInvariant(), _fonts.Title, CrtTerminalPalette.TextBright);
        name.ClipText = true;
        titles.AddChild(name);
        var job = RosterUi.Text(Loc.GetString("faction-roster-dossier-subheading", ("job", entry.Job.ToUpperInvariant())),
            _fonts.Small, CrtTerminalPalette.Accent);
        job.ClipText = true;
        titles.AddChild(job);
        header.AddChild(titles);
        root.AddChild(header);

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        _body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 6, 0),
        };
        scroll.AddChild(_body);
        root.AddChild(scroll);

        if (!string.IsNullOrWhiteSpace(entry.ShortExamine))
            Paragraph("faction-roster-dossier-short-examine", entry.ShortExamine, null);

        // Vital statistics: name on the left, value wrapping on the right.
        var stats = Section("faction-roster-dossier-statistics");
        Stat(stats, "detailed-examine-age", entry.Age > 0 ? entry.Age.ToString() : null, "detailed-examine-age-tooltip");
        Stat(stats, "detailed-examine-height", entry.Height, "detailed-examine-height-tooltip");
        Stat(stats, "detailed-examine-weight",
            entry.Weight > 0 ? Loc.GetString("detailed-examine-weight-value", ("weight", entry.Weight)) : null,
            "detailed-examine-weight-tooltip");
        Stat(stats, "detailed-examine-build", entry.Build, "detailed-examine-build-tooltip");
        Stat(stats, "detailed-examine-skin-tone", entry.SkinToneName, "detailed-examine-skin-tone-tooltip");
        Stat(stats, "detailed-examine-hair-color", entry.HairColorName, "detailed-examine-hair-color-tooltip");
        Stat(stats, "detailed-examine-eye-color", entry.EyeColorName, "detailed-examine-eye-color-tooltip");
        Stat(stats, "detailed-examine-allegiance", entry.Allegiance, "detailed-examine-allegiance-tooltip");
        Stat(stats, "detailed-examine-origin", entry.Origin, "detailed-examine-origin-tooltip");
        Stat(stats, "detailed-examine-religion", entry.Religion, "detailed-examine-religion-tooltip");

        Paragraph("detailed-examine-full-description", entry.FullDescription, "detailed-examine-full-description-tooltip");
        Paragraph("detailed-examine-medical-record", entry.MedicalRecord, "detailed-examine-medical-record-tooltip");
        Paragraph("detailed-examine-criminal-record", entry.CriminalRecord, "detailed-examine-criminal-record-tooltip");
        Paragraph("detailed-examine-general-record", entry.GeneralRecord, "detailed-examine-general-record-tooltip");

        var footer = RosterUi.Strip(CrtTerminalPalette.Surface1);
        var hint = RosterUi.Text(Loc.GetString("faction-roster-dossier-footer"), _fonts.Small, CrtTerminalPalette.TextDim);
        hint.ClipText = true;
        footer.AddChild(hint);
        root.AddChild(footer);

        Contents.AddChild(RosterUi.Tube(screen));
    }

    /// <summary>A titled block in the scrolling body; returns the column its contents go in.</summary>
    private BoxContainer Section(string title, string? tooltip = null)
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
        var column = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        var heading = RosterUi.Text(Loc.GetString(title).ToUpperInvariant(), _fonts.Bold, CrtTerminalPalette.Accent);
        heading.ClipText = true;
        if (tooltip != null)
            heading.ToolTip = Loc.GetString(tooltip);
        column.AddChild(heading);
        column.AddChild(new PanelContainer
        {
            MinHeight = 1,
            Margin = new Thickness(0, 0, 0, 2),
            PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Line },
        });
        panel.AddChild(column);
        _body.AddChild(panel);
        return column;
    }

    private void Stat(BoxContainer section, string label, string? value, string tooltip)
    {
        var row = RosterUi.Row();
        row.ToolTip = Loc.GetString(tooltip);
        var key = RosterUi.Text(Loc.GetString(label).ToUpperInvariant(), _fonts.Small, CrtTerminalPalette.TextDim);
        key.MinWidth = 120;
        key.VerticalAlignment = VAlignment.Top;
        row.AddChild(key);

        var empty = string.IsNullOrWhiteSpace(value);
        var text = Wrapping(empty ? Loc.GetString("faction-roster-dossier-not-recorded") : value!,
            empty ? CrtTerminalPalette.TextDim : CrtTerminalPalette.TextBright);
        text.HorizontalExpand = true;
        row.AddChild(text);
        section.AddChild(row);
    }

    private void Paragraph(string title, string? text, string? tooltip)
    {
        var section = Section(title, tooltip);
        var empty = string.IsNullOrWhiteSpace(text);
        section.AddChild(Wrapping(empty ? Loc.GetString("faction-roster-dossier-none") : text!.Trim(),
            empty ? CrtTerminalPalette.TextDim : CrtTerminalPalette.Text));
    }

    /// <summary>
    ///     Text that wraps to the window's width. Player text is shown exactly as written, so brackets in it can't
    ///     turn into markup.
    /// </summary>
    private static RichTextLabel Wrapping(string text, Color color)
    {
        var message = new FormattedMessage();
        message.PushColor(color);
        message.AddText(text);
        message.Pop();

        var label = new RichTextLabel { HorizontalExpand = true };
        label.SetMessage(message);
        return label;
    }
}
