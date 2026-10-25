using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Telephone;
using Content.Shared.CMU14.Telephone;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Telephone;

/// <summary>
///     A mobile phone's screen, drawn as a 90s handset: a grey plastic body with an antenna stub and speaker grille,
///     a small green monochrome LCD showing the phonebook, and chunky rubber keys underneath.
/// </summary>
public sealed class CMUMobilePhoneWindow : DefaultWindow, ICMUPhoneWindow
{
    private static readonly Color Body = Color.FromHex("#3c4045");
    private static readonly Color BodyEdge = Color.FromHex("#1e2124");
    private static readonly Color BodyLight = Color.FromHex("#4d5258");
    private static readonly Color Lcd = Color.FromHex("#8a9a5b");
    private static readonly Color LcdEdge = Color.FromHex("#2b3220");
    private static readonly Color Pixel = Color.FromHex("#1f2a12");
    private static readonly Color PixelDim = Color.FromHex("#56653a");
    private static readonly Color Key = Color.FromHex("#5a5f66");
    private static readonly Color KeyEdge = Color.FromHex("#26292d");
    private static readonly Color KeyInk = Color.FromHex("#e3e5e8");
    private static readonly Color Green = Color.FromHex("#3f7d45");
    private static readonly Color Red = Color.FromHex("#8e2f2a");

    public event Action<NetEntity>? OnCall;
    public event Action<bool>? OnDnd;
    public event Action<CMUEmergencyService>? OnEmergency;

    [Dependency] private IGameTiming _timing = default!;

    private readonly Font _lcd;
    private readonly Font _lcdBold;
    private readonly Font _key;

    private readonly Label _clock;
    private readonly Label _mode;
    private readonly Label _title;
    private readonly LineEdit _search;
    private readonly BoxContainer _entries;
    private readonly Button _dnd;
    private readonly BoxContainer _emergencyRow;

    private RMCTelephoneBuiState? _state;
    private string _name = string.Empty;

    public CMUMobilePhoneWindow()
    {
        IoCManager.InjectDependencies(this);
        _lcd = CMUPhoneStyle.Font(CMUPhoneStyle.Mono, 12);
        _lcdBold = CMUPhoneStyle.Font(CMUPhoneStyle.MonoBold, 12);
        _key = CMUPhoneStyle.Font(CMUPhoneStyle.SansBold, 11);

        MinSize = new Vector2(320, 560);
        SetSize = new Vector2(330, 600);

        var column = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };

        // Antenna stub on the top right corner.
        var antennaRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        antennaRow.AddChild(new Control { HorizontalExpand = true });
        antennaRow.AddChild(new PanelContainer
        {
            MinSize = new Vector2(16, 26),
            Margin = new Thickness(0, 0, 30, 0),
            PanelOverride = CMUPhoneStyle.Box(BodyEdge, Color.Black, 1),
        });
        column.AddChild(antennaRow);

        var body = CMUPhoneStyle.Panel(Body, BodyEdge, 3, 14, 10);
        body.VerticalExpand = true;
        var face = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8 };

        // Speaker grille.
        var grille = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4, HorizontalAlignment = HAlignment.Center };
        for (var i = 0; i < 7; i++)
            grille.AddChild(new PanelContainer { MinSize = new Vector2(4, 4), PanelOverride = CMUPhoneStyle.Box(BodyEdge) });
        face.AddChild(grille);

        var brand = CMUPhoneStyle.Text(Loc.GetString("cmu-mobile-brand"), _key, BodyLight);
        brand.HorizontalAlignment = HAlignment.Center;
        face.AddChild(brand);

        // The LCD.
        var lcd = CMUPhoneStyle.Panel(Lcd, LcdEdge, 4, 8, 6);
        lcd.VerticalExpand = true;
        var screen = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 3 };

        var status = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        status.AddChild(Bars(false));
        _mode = CMUPhoneStyle.Text(string.Empty, _lcdBold, Pixel);
        _mode.HorizontalExpand = true;
        _mode.Align = Label.AlignMode.Center;
        status.AddChild(_mode);
        status.AddChild(Bars(true));
        screen.AddChild(status);

        _title = CMUPhoneStyle.Text(Loc.GetString("cmu-mobile-phonebook"), _lcdBold, Pixel);
        _title.HorizontalAlignment = HAlignment.Center;
        screen.AddChild(_title);
        screen.AddChild(new PanelContainer { MinHeight = 2, PanelOverride = CMUPhoneStyle.Box(Pixel) });

        _search = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("cmu-mobile-search") };
        _search.OnTextChanged += _ => BuildEntries();
        screen.AddChild(_search);

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        _entries = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        scroll.AddChild(_entries);
        screen.AddChild(scroll);

        screen.AddChild(new PanelContainer { MinHeight = 2, PanelOverride = CMUPhoneStyle.Box(Pixel) });
        var softkeys = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        var select = CMUPhoneStyle.Text(Loc.GetString("cmu-mobile-softkey-call"), _lcdBold, Pixel);
        select.HorizontalExpand = true;
        softkeys.AddChild(select);
        _clock = CMUPhoneStyle.Text("00:00", _lcd, Pixel);
        softkeys.AddChild(_clock);
        screen.AddChild(softkeys);

        lcd.AddChild(screen);
        face.AddChild(lcd);

        // Keys: do-not-disturb, then the green and red keys.
        _dnd = CMUPhoneStyle.Key(string.Empty, _key, Key, KeyEdge, KeyInk);
        _dnd.OnPressed += _ =>
        {
            if (_state is { } s && (s.Dnd || s.CanDnd))
                OnDnd?.Invoke(!s.Dnd);
        };
        face.AddChild(_dnd);

        var callRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        var clear = CMUPhoneStyle.Key(Loc.GetString("cmu-mobile-clear"), _key, Green, KeyEdge, KeyInk);
        clear.HorizontalExpand = true;
        clear.OnPressed += _ =>
        {
            _search.Text = string.Empty;
            BuildEntries();
        };
        callRow.AddChild(clear);
        var sos = CMUPhoneStyle.Key(Loc.GetString("cmu-mobile-sos"), _key, Red, KeyEdge, KeyInk);
        sos.HorizontalExpand = true;
        sos.OnPressed += _ => _emergencyRow!.Visible = !_emergencyRow.Visible;
        callRow.AddChild(sos);
        face.AddChild(callRow);

        _emergencyRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6, Visible = false };
        AddService(CMUEmergencyService.FireMedical, "cmu-911-service-fire-medical");
        AddService(CMUEmergencyService.LawEnforcement, "cmu-911-service-law");
        face.AddChild(_emergencyRow);

        body.AddChild(face);
        column.AddChild(body);
        Contents.AddChild(column);
    }

    /// <summary>Signal bars on the left of the LCD, battery bars on the right.</summary>
    private Control Bars(bool battery)
    {
        var bars = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 1, VerticalAlignment = VAlignment.Bottom };
        for (var i = 1; i <= 4; i++)
        {
            bars.AddChild(new PanelContainer
            {
                MinSize = battery ? new Vector2(4, 8) : new Vector2(3, 2 + i * 2),
                VerticalAlignment = VAlignment.Bottom,
                PanelOverride = CMUPhoneStyle.Box(Pixel),
            });
        }

        return bars;
    }

    private void AddService(CMUEmergencyService service, string text)
    {
        var button = CMUPhoneStyle.Key(Loc.GetString(text), _key, Red, KeyEdge, KeyInk);
        button.HorizontalExpand = true;
        button.OnPressed += _ =>
        {
            _emergencyRow.Visible = false;
            OnEmergency?.Invoke(service);
        };
        _emergencyRow.AddChild(button);
    }

    public void UpdateState(RMCTelephoneBuiState state, string phoneName)
    {
        _state = state;
        _name = phoneName;
        _mode.Text = Loc.GetString(state.Dnd ? "cmu-mobile-lcd-silent" : "cmu-mobile-lcd-ready");
        _dnd.Text = Loc.GetString(state.Dnd ? "cmu-mobile-dnd-on" : "cmu-mobile-dnd-off");
        _dnd.Disabled = !state.Dnd && !state.CanDnd;
        Title = phoneName;
        BuildEntries();
    }

    private void BuildEntries()
    {
        _entries.RemoveAllChildren();
        if (_state is not { } state)
            return;

        var filter = _search.Text.Trim();
        var any = false;
        foreach (var (category, phones) in CMUPhoneStyle.Group(state.Phones))
        {
            var matches = phones.Where(p => filter.Length == 0 || p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count == 0)
                continue;

            any = true;
            var section = CMUPhoneStyle.Text($"-{category.ToUpperInvariant()}-", _lcdBold, PixelDim);
            section.ClipText = true;
            section.Margin = new Thickness(0, 4, 0, 0);
            _entries.AddChild(section);

            foreach (var phone in matches)
                _entries.AddChild(Entry(phone));
        }

        if (!any)
        {
            var none = CMUPhoneStyle.Text(Loc.GetString("cmu-mobile-no-entries"), _lcd, Pixel);
            none.HorizontalAlignment = HAlignment.Center;
            none.Margin = new Thickness(0, 12, 0, 0);
            _entries.AddChild(none);
        }
    }

    /// <summary>A phonebook line; hovering it inverts it, like the cursor on an old LCD.</summary>
    private Control Entry(RMCPhone phone)
    {
        var button = new ContainerButton { HorizontalExpand = true };
        var panel = CMUPhoneStyle.Panel(Lcd, null, 0, 2, 1);
        var label = CMUPhoneStyle.Text(phone.Name.ToUpperInvariant(), _lcd, Pixel);
        label.ClipText = true;
        panel.AddChild(label);
        button.AddChild(panel);
        button.OnMouseEntered += _ =>
        {
            panel.PanelOverride = CMUPhoneStyle.Box(Pixel, null, 0, 2, 1);
            label.FontColorOverride = Lcd;
            label.Text = "> " + phone.Name.ToUpperInvariant();
        };
        button.OnMouseExited += _ =>
        {
            panel.PanelOverride = CMUPhoneStyle.Box(Lcd, null, 0, 2, 1);
            label.FontColorOverride = Pixel;
            label.Text = phone.Name.ToUpperInvariant();
        };
        var id = phone.Id;
        button.OnPressed += _ => OnCall?.Invoke(id);
        return button;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        var time = _timing.CurTime;
        _clock.Text = $"{(int) time.TotalHours % 24:00}:{time.Minutes:00}";
    }
}
