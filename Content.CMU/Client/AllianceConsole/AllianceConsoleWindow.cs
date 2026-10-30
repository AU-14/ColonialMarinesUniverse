using System.Linq;
using System.Numerics;
using Content.Client.CMU14.Interface;
using Content.Client.Resources;
using Content.Shared.CMU14.AllianceConsole;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.AllianceConsole;

/// <summary>
///     The alliance console's screen, drawn in the CRT theme: one row per third-party faction, each with a
///     neutral / friendly / hostile switch.
/// </summary>
public sealed class AllianceConsoleWindow : DefaultWindow
{
    private const string MonoRegular = "/Fonts/RobotoMono/RobotoMono-Regular.ttf";
    private const string MonoBold = "/Fonts/RobotoMono/RobotoMono-Bold.ttf";

    private static readonly AllianceStatus[] Statuses = { AllianceStatus.Hostile, AllianceStatus.Neutral, AllianceStatus.Friendly };

    public event Action<string, AllianceStatus>? OnSetStatus;

    private readonly IPrototypeManager _prototypes;
    private readonly Font _small;
    private readonly Font _bold;
    private readonly Font _title;

    private readonly Label _subheading;
    private readonly Label _summary;
    private readonly BoxContainer _list;
    private readonly Label _empty;
    private readonly Dictionary<string, FactionRow> _rows = new();

    public AllianceConsoleWindow()
    {
        _prototypes = IoCManager.Resolve<IPrototypeManager>();
        var cache = IoCManager.Resolve<IResourceCache>();
        _small = cache.GetFont(MonoRegular, 10);
        _bold = cache.GetFont(MonoBold, 12);
        _title = cache.GetFont(MonoBold, 15);

        Title = Loc.GetString("alliance-console-title");
        MinSize = new Vector2(540, 380);
        SetSize = new Vector2(560, 460);

        var screen = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface0 },
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            Margin = new Thickness(10),
        };
        screen.AddChild(root);

        var header = Strip(CrtTerminalPalette.Surface3);
        var titles = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        titles.AddChild(Text(Loc.GetString("alliance-console-crt-heading"), _title, CrtTerminalPalette.TextBright));
        // The console's own faction goes on the small line: the longest names wouldn't fit beside the heading.
        _subheading = Text(Loc.GetString("alliance-console-crt-subheading"), _small, CrtTerminalPalette.TextDim);
        _subheading.ClipText = true;
        titles.AddChild(_subheading);
        header.AddChild(titles);
        root.AddChild(header);

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        _list = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };
        scroll.AddChild(_list);
        root.AddChild(scroll);

        _empty = Text(Loc.GetString("alliance-console-crt-empty"), _small, CrtTerminalPalette.TextDim);
        _empty.HorizontalAlignment = HAlignment.Center;
        _empty.Visible = false;
        _list.AddChild(_empty);

        var footer = Strip(CrtTerminalPalette.Surface1);
        _summary = Text(string.Empty, _small, CrtTerminalPalette.TextDim);
        _summary.ClipText = true;
        footer.AddChild(_summary);
        root.AddChild(footer);

        // Reading surface, so no roll bar or grain.
        var tube = new Control { HorizontalExpand = true, VerticalExpand = true };
        tube.AddChild(screen);
        tube.AddChild(new CrtScreenControl
        {
            Source = screen,
            Phosphor = CrtTerminalPalette.Accent,
            Roll = false,
            Grain = false,
        });
        Contents.AddChild(tube);
    }

    public void UpdateState(AllianceConsoleBuiState state)
    {
        var side = AllianceConsoleComponent.ResolveFaction(_prototypes, state.Faction); // CMU14
        _subheading.Text = Loc.GetString("alliance-console-crt-subheading-faction",
            ("faction", (side?.Name ?? state.Faction).ToUpperInvariant()));

        // Rebuild the rows if the set of factions changed.
        if (!_rows.Keys.ToHashSet().SetEquals(state.ControllableFactions))
        {
            foreach (var row in _rows.Values)
                _list.RemoveChild(row.Root);
            _rows.Clear();

            foreach (var faction in state.ControllableFactions.OrderBy(Name))
            {
                var row = new FactionRow(this, faction, Name(faction));
                _list.AddChild(row.Root);
                _rows[faction] = row;
            }
        }

        _empty.Visible = _rows.Count == 0;

        var counts = new Dictionary<AllianceStatus, int>();
        foreach (var (faction, row) in _rows)
        {
            var status = state.FactionStatuses.TryGetValue(faction, out var s) ? s : AllianceStatus.Neutral;
            row.Set(status);
            counts[status] = counts.GetValueOrDefault(status) + 1;
        }

        _summary.Text = Loc.GetString("alliance-console-crt-summary",
            ("friendly", counts.GetValueOrDefault(AllianceStatus.Friendly)),
            ("neutral", counts.GetValueOrDefault(AllianceStatus.Neutral)),
            ("hostile", counts.GetValueOrDefault(AllianceStatus.Hostile)));
    }

    private string Name(string faction)
    {
        return AllianceConsoleComponent.ResolveFaction(_prototypes, faction)?.Name ?? faction; // CMU14
    }

    private static Color StatusColor(AllianceStatus status)
    {
        return status switch
        {
            AllianceStatus.Friendly => CrtTerminalPalette.Accent,
            AllianceStatus.Hostile => CrtTerminalPalette.Alert,
            _ => CrtTerminalPalette.Caution,
        };
    }

    private static string StatusText(AllianceStatus status)
    {
        return Loc.GetString(status switch
        {
            AllianceStatus.Friendly => "alliance-console-status-friendly",
            AllianceStatus.Hostile => "alliance-console-status-hostile",
            _ => "alliance-console-status-neutral",
        });
    }

    private static PanelContainer Strip(Color color)
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

    private static Label Text(string text, Font font, Color color)
    {
        return new Label { Text = text, FontOverride = font, FontColorOverride = color };
    }

    /// <summary>One faction: a coloured marker, its name and current standing, and a three-way switch.</summary>
    private sealed class FactionRow
    {
        public readonly PanelContainer Root;
        private readonly PanelContainer _marker;
        private readonly Label _standing;
        private readonly Dictionary<AllianceStatus, Button> _buttons = new();
        private Font _font = default!;

        public FactionRow(AllianceConsoleWindow window, string faction, string name)
        {
            Root = new PanelContainer
            {
                HorizontalExpand = true,
                PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface2 },
            };

            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
            _marker = new PanelContainer { MinWidth = 4 };
            row.AddChild(_marker);

            var column = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                HorizontalExpand = true,
                SeparationOverride = 4,
                Margin = new Thickness(0, 6, 8, 6),
            };

            // The name gets the whole width: faction names run to over thirty characters.
            var label = Text(name.ToUpperInvariant(), window._bold, CrtTerminalPalette.TextBright);
            label.ClipText = true;
            column.AddChild(label);

            var controls = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
            _standing = Text(string.Empty, window._small, CrtTerminalPalette.TextDim);
            _standing.ClipText = true;
            _standing.HorizontalExpand = true;
            _standing.VerticalAlignment = VAlignment.Center;
            controls.AddChild(_standing);

            var buttons = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 4,
                VerticalAlignment = VAlignment.Center,
            };
            foreach (var status in Statuses)
            {
                var button = new Button
                {
                    Text = StatusText(status).ToUpperInvariant(),
                    ToggleMode = true,
                    MinWidth = 84,
                };
                _font = window._small;
                var captured = status;
                button.OnPressed += _ =>
                {
                    Set(captured);
                    window.OnSetStatus?.Invoke(faction, captured);
                };
                buttons.AddChild(button);
                _buttons[status] = button;
            }

            controls.AddChild(buttons);
            column.AddChild(controls);
            row.AddChild(column);
            Root.AddChild(row);
        }

        public void Set(AllianceStatus status)
        {
            // The button style replaces the pressed look, so only the current standing is lit in its colour.
            foreach (var (s, button) in _buttons)
            {
                button.Pressed = s == status;
                CmuButtonStyles.Apply(button, s != status
                    ? CmuButtonStyles.Variant.Neutral
                    : s switch
                    {
                        AllianceStatus.Friendly => CmuButtonStyles.Variant.Affirm,
                        AllianceStatus.Hostile => CmuButtonStyles.Variant.Danger,
                        _ => CmuButtonStyles.Variant.Caution,
                    });
                button.Label.FontOverride = _font;
            }

            var color = StatusColor(status);
            _marker.PanelOverride = new StyleBoxFlat { BackgroundColor = color };
            _standing.Text = Loc.GetString("alliance-console-crt-standing", ("status", StatusText(status).ToUpperInvariant()));
            _standing.FontColorOverride = color;
        }
    }
}
