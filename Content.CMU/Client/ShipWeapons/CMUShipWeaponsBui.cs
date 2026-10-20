using System.Linq;
using System.Numerics;
using Content.Client.CMU14.Interface;
using Content.Client.Resources;
using Content.Client.UserInterface.Controls;
using Content.Shared.CMU14.ShipWeapons;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.ShipWeapons;

public sealed class CMUShipWeaponsBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CMUShipWeaponsWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMUShipWeaponsWindow>();
        _window.OnArm += weapons => SendMessage(new CMUShipWeaponsArmBuiMsg(weapons));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is CMUShipWeaponsBuiState weapons)
            _window?.UpdateState(weapons);
    }
}

/// <summary>
///     The arming terminal's screen: a fire-control console drawn in the CRT theme and through the CRT shader. Each
///     fire mission is a card showing what it does; command picks up to the terminal's limit and locks them in.
/// </summary>
public sealed class CMUShipWeaponsWindow : DefaultWindow
{
    private const string MonoRegular = "/Fonts/RobotoMono/RobotoMono-Regular.ttf";
    private const string MonoBold = "/Fonts/RobotoMono/RobotoMono-Bold.ttf";

    public event Action<ProtoId<CMUShipWeaponPrototype>[]>? OnArm;

    private readonly IPrototypeManager _prototypes;
    private readonly Font _small;
    private readonly Font _body;
    private readonly Font _bold;
    private readonly Font _title;

    private readonly Label _statusTag;
    private readonly PanelContainer _statusPanel;
    private readonly Label _instructions;
    private readonly Label _counter;
    private readonly Label _pool;
    private readonly ConfirmButton _arm;
    private readonly Dictionary<ProtoId<CMUShipWeaponPrototype>, WeaponCard> _cards = new();

    private CMUShipWeaponsBuiState? _state;
    private bool _locked;
    private bool _available;
    private int _maxSelections;
    private HashSet<ProtoId<CMUShipWeaponPrototype>> _availableWeapons = new();

    public CMUShipWeaponsWindow()
    {
        _prototypes = IoCManager.Resolve<IPrototypeManager>();
        var cache = IoCManager.Resolve<IResourceCache>();
        _small = cache.GetFont(MonoRegular, 10);
        _body = cache.GetFont(MonoRegular, 12);
        _bold = cache.GetFont(MonoBold, 12);
        _title = cache.GetFont(MonoBold, 15);

        Title = Loc.GetString("cmu-ship-weapons-title");
        MinSize = new Vector2(620, 520);
        SetSize = new Vector2(680, 600);

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

        // Header strip: what this console is, and where the ship's loadout stands.
        var header = Strip(CrtTerminalPalette.Surface3);
        var headerRow = Row();
        var titles = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        titles.AddChild(Text(Loc.GetString("cmu-ship-weapons-crt-heading"), _title, CrtTerminalPalette.TextBright));
        var subheading = Text(Loc.GetString("cmu-ship-weapons-crt-subheading"), _small, CrtTerminalPalette.TextDim);
        subheading.ClipText = true;
        titles.AddChild(subheading);
        headerRow.AddChild(titles);
        _statusPanel = new PanelContainer { VerticalAlignment = VAlignment.Center };
        _statusTag = Text(string.Empty, _bold, CrtTerminalPalette.Void);
        _statusTag.Margin = new Thickness(8, 3);
        _statusPanel.AddChild(_statusTag);
        headerRow.AddChild(_statusPanel);
        header.AddChild(headerRow);
        root.AddChild(header);

        _instructions = Text(string.Empty, _body, CrtTerminalPalette.Text);
        _instructions.Margin = new Thickness(2, 0);
        root.AddChild(_instructions);

        _arm = new ConfirmButton
        {
            Text = Loc.GetString("cmu-ship-weapons-arm"),
            ConfirmationText = Loc.GetString("cmu-ship-weapons-confirm"),
            Disabled = true,
            MinWidth = 220,
            VerticalAlignment = VAlignment.Center,
        };
        CmuButtonStyles.Apply(_arm, CmuButtonStyles.Variant.Danger);
        _arm.Label.FontOverride = _bold;
        _arm.OnPressed += _ =>
        {
            var selected = _cards.Where(p => p.Value.Button.Pressed).Select(p => p.Key).ToArray();
            if (selected.Length >= 1 && selected.Length <= _maxSelections)
                OnArm?.Invoke(selected);
        };

        // The fire missions, two to a row.
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        var grid = new GridContainer { Columns = 2, HSeparationOverride = 6, VSeparationOverride = 6, HorizontalExpand = true };
        scroll.AddChild(grid);
        root.AddChild(scroll);

        foreach (var profile in _prototypes.EnumeratePrototypes<CMUShipWeaponPrototype>().OrderBy(p => Loc.GetString(p.Name)))
        {
            var card = new WeaponCard(profile, this);
            card.Button.OnToggled += _ =>
            {
                _arm.IsConfirming = false;
                UpdateChoices();
            };
            grid.AddChild(card.Button);
            _cards.Add(profile.ID, card);
        }

        // Footer: what's picked, what it's worth, and the button that locks it in.
        var footer = Strip(CrtTerminalPalette.Surface1);
        var footerRow = Row();
        var counts = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        _counter = Text(string.Empty, _bold, CrtTerminalPalette.TextBright);
        _counter.ClipText = true;
        _pool = Text(string.Empty, _small, CrtTerminalPalette.TextDim);
        _pool.ClipText = true;
        counts.AddChild(_counter);
        counts.AddChild(_pool);
        footerRow.AddChild(counts);
        footerRow.AddChild(_arm);
        footer.AddChild(footerRow);
        root.AddChild(footer);

        // The whole screen again, through the tube. Reading surface, so no roll bar or grain.
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

    public void UpdateState(CMUShipWeaponsBuiState state)
    {
        _state = state;
        _available = state.Available;
        _maxSelections = state.MaxSelections;
        _locked = state.Locked;
        _availableWeapons = state.AvailableWeapons.ToHashSet();
        _instructions.Text = Wrap(Loc.GetString("cmu-ship-weapons-instructions", ("max", _maxSelections), ("uses", state.Uses)),
            InstructionColumns);

        var (tag, color) = !_available
            ? ("cmu-ship-weapons-crt-status-offline", CrtTerminalPalette.Alert)
            : _locked
                ? ("cmu-ship-weapons-crt-status-locked", CrtTerminalPalette.Caution)
                : ("cmu-ship-weapons-crt-status-open", CrtTerminalPalette.Accent);
        _statusTag.Text = Loc.GetString(tag);
        _statusPanel.PanelOverride = new StyleBoxFlat { BackgroundColor = color };

        foreach (var (id, card) in _cards)
        {
            if (!_locked && !_availableWeapons.Contains(id))
                card.Button.Pressed = false;
            else if (_locked)
                card.Button.Pressed = state.Weapons.Any(w => w.Weapon == id);
        }

        UpdateChoices();
    }

    private void UpdateChoices()
    {
        var count = _cards.Values.Count(c => c.Button.Pressed);
        foreach (var (id, card) in _cards)
        {
            card.Button.Disabled = !_available || _locked || !_availableWeapons.Contains(id) ||
                                   count >= _maxSelections && !card.Button.Pressed;
            card.Refresh(Status(id));
        }

        _arm.Disabled = !_available || _locked || count < 1 || count > _maxSelections;
        if (_locked && _state != null)
        {
            var armed = _state.Weapons.FirstOrDefault();
            _counter.Text = Loc.GetString("cmu-ship-weapons-crt-armed-count", ("count", _state.Weapons.Length));
            _pool.Text = Loc.GetString("cmu-ship-weapons-crt-pool", ("uses", armed.Remaining), ("max", armed.Max));
        }
        else
        {
            _counter.Text = Loc.GetString("cmu-ship-weapons-crt-selected", ("count", count), ("max", _maxSelections));
            _pool.Text = Loc.GetString("cmu-ship-weapons-crt-pool-pending", ("uses", _state?.Uses ?? 0));
        }
    }

    private CardStatus Status(ProtoId<CMUShipWeaponPrototype> id)
    {
        if (_locked)
        {
            if (_state != null && _state.Weapons.Any(w => w.Weapon == id))
            {
                var armed = _state.Weapons.First(w => w.Weapon == id);
                return new CardStatus(Loc.GetString("cmu-ship-weapons-crt-tag-armed",
                    ("uses", armed.Remaining), ("max", armed.Max)), CrtTerminalPalette.Accent);
            }

            return new CardStatus(Loc.GetString("cmu-ship-weapons-crt-tag-standby"), CrtTerminalPalette.TextDim);
        }

        if (!_availableWeapons.Contains(id))
        {
            return new CardStatus(Loc.GetString(_state?.RequiresGuns == true
                ? "cmu-ship-weapons-crt-tag-no-battery"
                : "cmu-ship-weapons-crt-tag-unavailable"), CrtTerminalPalette.Caution);
        }

        return _cards[id].Button.Pressed
            ? new CardStatus(Loc.GetString("cmu-ship-weapons-crt-tag-selected"), CrtTerminalPalette.Accent)
            : new CardStatus(Loc.GetString("cmu-ship-weapons-crt-tag-ready"), CrtTerminalPalette.TextDim);
    }

    /// <summary>
    ///     How many characters of the body font fit across the window at its minimum width. Labels don't wrap, and the
    ///     font is monospaced, so the instructions are broken into lines this long.
    /// </summary>
    private const int InstructionColumns = 76;

    /// <summary>Breaks <paramref name="text"/> into lines of at most <paramref name="columns"/>, at spaces.</summary>
    private static string Wrap(string text, int columns)
    {
        var lines = new List<string>();
        var line = new System.Text.StringBuilder();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > columns)
            {
                lines.Add(line.ToString());
                line.Clear();
            }

            if (line.Length > 0)
                line.Append(' ');
            line.Append(word);
        }

        if (line.Length > 0)
            lines.Add(line.ToString());

        return string.Join("\n", lines);
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

    private static BoxContainer Row()
    {
        return new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
    }

    private static Label Text(string text, Font font, Color color)
    {
        return new Label { Text = text, FontOverride = font, FontColorOverride = color };
    }

    private readonly record struct CardStatus(string Text, Color Color);

    /// <summary>One fire mission: its name, what it does, and where it stands on this ship.</summary>
    private sealed class WeaponCard
    {
        public readonly ContainerButton Button;
        private readonly PanelContainer _panel;
        private readonly Label _name;
        private readonly Label _tag;
        private readonly PanelContainer _marker;

        public WeaponCard(CMUShipWeaponPrototype profile, CMUShipWeaponsWindow window)
        {
            Button = new ContainerButton { ToggleMode = true, HorizontalExpand = true, MinHeight = 74 };
            _panel = new PanelContainer { HorizontalExpand = true, VerticalExpand = true };
            Button.AddChild(_panel);

            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
            _marker = new PanelContainer { MinWidth = 4 };
            row.AddChild(_marker);

            var column = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                HorizontalExpand = true,
                Margin = new Thickness(0, 6, 8, 6),
            };
            _name = Text(Loc.GetString(profile.Name).ToUpperInvariant(), window._bold, CrtTerminalPalette.TextBright);
            _name.ClipText = true;
            column.AddChild(_name);
            var detail = Text(Describe(profile), window._small, CrtTerminalPalette.TextDim);
            detail.ClipText = true;
            column.AddChild(detail);
            _tag = Text(string.Empty, window._small, CrtTerminalPalette.TextDim);
            _tag.ClipText = true;
            _tag.Margin = new Thickness(0, 4, 0, 0);
            column.AddChild(_tag);
            row.AddChild(column);
            _panel.AddChild(row);

            Button.OnMouseEntered += _ => Repaint();
            Button.OnMouseExited += _ => Repaint();
        }

        private CardStatus _status;

        public void Refresh(CardStatus status)
        {
            _status = status;
            _tag.Text = status.Text;
            _tag.FontColorOverride = status.Color;
            Repaint();
        }

        private void Repaint()
        {
            var fill = Button.Pressed ? CrtTerminalPalette.Surface4
                : Button.IsHovered && !Button.Disabled ? CrtTerminalPalette.Surface3
                : CrtTerminalPalette.Surface2;
            _panel.PanelOverride = new StyleBoxFlat { BackgroundColor = fill };
            _marker.PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Button.Pressed ? CrtTerminalPalette.Accent : _status.Color.WithAlpha(0.45f),
            };
            _name.FontColorOverride = Button.Disabled && !Button.Pressed
                ? CrtTerminalPalette.TextDim
                : CrtTerminalPalette.TextBright;
        }

        private static string Describe(CMUShipWeaponPrototype profile)
        {
            if (profile.RailDamage != null)
                return Loc.GetString("cmu-ship-weapons-crt-detail-rail");

            if (profile.Cloud != null)
            {
                return Loc.GetString(profile.Ems ? "cmu-ship-weapons-crt-detail-ems" : "cmu-ship-weapons-crt-detail-cloud",
                    ("radius", profile.CloudRadius));
            }

            if (profile.WalkingDistance > 0)
            {
                return Loc.GetString("cmu-ship-weapons-crt-detail-walking", ("shots", profile.Shots),
                    ("blast", profile.BlastRadius), ("distance", profile.WalkingDistance));
            }

            return Loc.GetString(profile.Spread > 0 ? "cmu-ship-weapons-crt-detail-barrage" : "cmu-ship-weapons-crt-detail-single",
                ("shots", profile.Shots), ("blast", profile.BlastRadius), ("spread", profile.Spread));
        }
    }
}
