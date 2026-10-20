using System.Linq;
using System.Numerics;
using Content.Client.CMU14.Interface;
using Content.Client.Resources;
using Content.Shared.CMU14.FactionRoster;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.FactionRoster;

/// <summary>
///     The personnel roster, drawn in the CRT theme: a searchable list of everyone on file. Clicking a card opens that
///     person's dossier.
/// </summary>
public sealed class FactionRosterConsoleWindow : DefaultWindow
{
    private readonly List<FactionRosterEntryWindow> _openEntryWindows = new();
    private readonly RosterFonts _fonts = new();

    private readonly Label _count;
    private readonly LineEdit _search;
    private readonly BoxContainer _list;
    private readonly Label _footer;

    private List<FactionRosterEntry> _entries = new();

    public event Action? OnRefresh;

    public FactionRosterConsoleWindow()
    {
        Title = Loc.GetString("faction-roster-console-window-title");
        MinSize = new Vector2(460, 520);
        SetSize = new Vector2(500, 620);

        var screen = RosterUi.Screen(out var root);

        // Header: what this is, how many are on file, and a refresh.
        var header = RosterUi.Strip(CrtTerminalPalette.Surface3);
        var headerRow = RosterUi.Row();
        var titles = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        titles.AddChild(RosterUi.Text(Loc.GetString("faction-roster-console-heading"), _fonts.Title, CrtTerminalPalette.TextBright));
        _count = RosterUi.Text(string.Empty, _fonts.Small, CrtTerminalPalette.TextDim);
        _count.ClipText = true;
        _count.ToolTip = Loc.GetString("faction-roster-console-count-tooltip");
        titles.AddChild(_count);
        headerRow.AddChild(titles);
        var refresh = new Button
        {
            Text = Loc.GetString("faction-roster-console-refresh-button"),
            ToolTip = Loc.GetString("faction-roster-console-refresh-tooltip"),
            VerticalAlignment = VAlignment.Center,
            MinWidth = 90,
        };
        CmuButtonStyles.Apply(refresh, CmuButtonStyles.Variant.Neutral);
        refresh.Label.FontOverride = _fonts.Small;
        refresh.OnPressed += _ => OnRefresh?.Invoke();
        headerRow.AddChild(refresh);
        header.AddChild(headerRow);
        root.AddChild(header);

        _search = new LineEdit
        {
            PlaceHolder = Loc.GetString("faction-roster-console-search"),
            HorizontalExpand = true,
        };
        _search.OnTextChanged += _ => Rebuild();
        root.AddChild(_search);

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        _list = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };
        scroll.AddChild(_list);
        root.AddChild(scroll);

        var footer = RosterUi.Strip(CrtTerminalPalette.Surface1);
        _footer = RosterUi.Text(Loc.GetString("faction-roster-console-footer"), _fonts.Small, CrtTerminalPalette.TextDim);
        _footer.ClipText = true;
        footer.AddChild(_footer);
        root.AddChild(footer);

        Contents.AddChild(RosterUi.Tube(screen));
        OnClose += CloseEntryWindows;
    }

    public void UpdateState(FactionRosterConsoleBuiState state)
    {
        _entries = state.Entries;
        Rebuild();
    }

    private void Rebuild()
    {
        _list.RemoveAllChildren();

        var filter = _search.Text.Trim();
        var shown = string.IsNullOrEmpty(filter)
            ? _entries
            : _entries.Where(e => e.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                  e.Job.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        _count.Text = string.IsNullOrEmpty(filter)
            ? Loc.GetString("faction-roster-console-count", ("count", _entries.Count))
            : Loc.GetString("faction-roster-console-count-filtered", ("shown", shown.Count), ("count", _entries.Count));

        if (shown.Count == 0)
        {
            var none = RosterUi.Text(Loc.GetString(_entries.Count == 0
                ? "faction-roster-console-empty"
                : "faction-roster-console-no-match"), _fonts.Bold, CrtTerminalPalette.TextDim);
            none.HorizontalAlignment = HAlignment.Center;
            none.Margin = new Thickness(0, 24, 0, 0);
            _list.AddChild(none);
            return;
        }

        foreach (var entry in shown)
            _list.AddChild(Card(entry));
    }

    private Control Card(FactionRosterEntry entry)
    {
        var button = new ContainerButton
        {
            HorizontalExpand = true,
            ToolTip = Loc.GetString("faction-roster-console-entry-tooltip", ("name", entry.Name)),
        };
        var panel = new PanelContainer { PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface2 } };
        var row = RosterUi.Row();
        var marker = new PanelContainer
        {
            MinWidth = 4,
            PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Accent.WithAlpha(0.45f) },
        };
        row.AddChild(marker);

        var column = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(0, 6, 8, 6),
        };
        var name = RosterUi.Text(entry.Name.ToUpperInvariant(), _fonts.Bold, CrtTerminalPalette.TextBright);
        name.ClipText = true;
        column.AddChild(name);
        var job = RosterUi.Text(entry.Job.ToUpperInvariant(), _fonts.Small, CrtTerminalPalette.Accent);
        job.ClipText = true;
        column.AddChild(job);

        // Only the first line of the short examine fits on a card; the dossier has the rest.
        if (!string.IsNullOrWhiteSpace(entry.ShortExamine))
        {
            var preview = RosterUi.Text(RosterUi.FirstLine(entry.ShortExamine), _fonts.Small, CrtTerminalPalette.TextDim);
            preview.ClipText = true;
            preview.Margin = new Thickness(0, 2, 0, 0);
            column.AddChild(preview);
        }

        row.AddChild(column);
        var open = RosterUi.Text(Loc.GetString("faction-roster-console-open"), _fonts.Small, CrtTerminalPalette.TextDim);
        open.VerticalAlignment = VAlignment.Center;
        open.Margin = new Thickness(0, 0, 10, 0);
        row.AddChild(open);
        panel.AddChild(row);
        button.AddChild(panel);

        button.OnMouseEntered += _ =>
        {
            panel.PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface3 };
            marker.PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Accent };
        };
        button.OnMouseExited += _ =>
        {
            panel.PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface2 };
            marker.PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Accent.WithAlpha(0.45f) };
        };
        button.OnPressed += _ => OpenEntry(entry);
        return button;
    }

    private void OpenEntry(FactionRosterEntry entry)
    {
        var window = new FactionRosterEntryWindow(entry);
        _openEntryWindows.Add(window);
        window.OnClose += () => _openEntryWindows.Remove(window);
        window.OpenCentered();
    }

    private void CloseEntryWindows()
    {
        foreach (var window in _openEntryWindows.ToArray())
        {
            window.Close();
        }

        _openEntryWindows.Clear();
    }
}

/// <summary>The monospaced fonts the roster's screens use.</summary>
internal sealed class RosterFonts
{
    private const string MonoRegular = "/Fonts/RobotoMono/RobotoMono-Regular.ttf";
    private const string MonoBold = "/Fonts/RobotoMono/RobotoMono-Bold.ttf";

    public readonly Font Small;
    public readonly Font Body;
    public readonly Font Bold;
    public readonly Font Title;

    public RosterFonts()
    {
        var cache = IoCManager.Resolve<IResourceCache>();
        Small = cache.GetFont(MonoRegular, 10);
        Body = cache.GetFont(MonoRegular, 12);
        Bold = cache.GetFont(MonoBold, 12);
        Title = cache.GetFont(MonoBold, 15);
    }
}

/// <summary>Building blocks shared by the roster and dossier screens.</summary>
internal static class RosterUi
{
    /// <summary>The screen's background and its padded column, which <paramref name="root"/> returns.</summary>
    public static PanelContainer Screen(out BoxContainer root)
    {
        var screen = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface0 },
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            Margin = new Thickness(10),
        };
        screen.AddChild(root);
        return screen;
    }

    /// <summary>The screen again, through the tube. Reading surface, so no roll bar or grain.</summary>
    public static Control Tube(PanelContainer screen)
    {
        var tube = new Control { HorizontalExpand = true, VerticalExpand = true };
        tube.AddChild(screen);
        tube.AddChild(new CrtScreenControl
        {
            Source = screen,
            Phosphor = CrtTerminalPalette.Accent,
            Roll = false,
            Grain = false,
        });
        return tube;
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

    public static BoxContainer Row()
    {
        return new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
    }

    public static Label Text(string text, Font font, Color color)
    {
        return new Label { Text = text, FontOverride = font, FontColorOverride = color };
    }

    /// <summary>The first line of <paramref name="text"/>, marked as cut off if there's more.</summary>
    public static string FirstLine(string text)
    {
        var trimmed = text.Trim();
        var newline = trimmed.IndexOf('\n');
        return newline < 0 ? trimmed : trimmed[..newline].TrimEnd() + " …";
    }
}
