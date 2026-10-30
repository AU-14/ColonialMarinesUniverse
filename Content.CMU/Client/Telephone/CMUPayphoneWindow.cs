using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Telephone;
using Content.Shared.CMU14.Telephone;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.Telephone;

/// <summary>
///     A payphone booth, drawn as a public payphone: a brushed steel faceplate with a black header plate and a green
///     LCD, a white printed directory placard with every number grouped by section, and a big red 911 key.
/// </summary>
public sealed class CMUPayphoneWindow : DefaultWindow, ICMUPhoneWindow
{
    private static readonly Color Steel = Color.FromHex("#4c5157");
    private static readonly Color SteelEdge = Color.FromHex("#2c3034");
    private static readonly Color SteelDark = Color.FromHex("#383c41");
    private static readonly Color Plate = Color.FromHex("#141619");
    private static readonly Color PlateInk = Color.FromHex("#c9cbce");
    private static readonly Color PlateInkDim = Color.FromHex("#7e838a");
    private static readonly Color Lcd = Color.FromHex("#55663f");
    private static readonly Color LcdEdge = Color.FromHex("#2a321f");
    private static readonly Color LcdInk = Color.FromHex("#141c0b");
    private static readonly Color Placard = Color.FromHex("#a7a9a3");
    private static readonly Color PlacardEdge = Color.FromHex("#7c7e79");
    private static readonly Color Band = Color.FromHex("#1e3a5f");
    private static readonly Color Ink = Color.FromHex("#1b1d21");
    private static readonly Color InkFaint = Color.FromHex("#45484d");
    private static readonly Color KeyFace = Color.FromHex("#8e9298");
    private static readonly Color KeyEdge = Color.FromHex("#4a4e53");
    private static readonly Color CallKey = Color.FromHex("#2d4a6e");
    private static readonly Color Red = Color.FromHex("#8c2520");
    private static readonly Color RedEdge = Color.FromHex("#6e130f");

    public event Action<NetEntity>? OnCall;
    public event Action<bool>? OnDnd;
    public event Action<CMUEmergencyService>? OnEmergency;

    private readonly Font _header;
    private readonly Font _small;
    private readonly Font _lcd;
    private readonly Font _body;
    private readonly Font _bold;
    private readonly Font _key;

    private readonly Label _where;
    private readonly Label _display;
    private readonly LineEdit _search;
    private readonly BoxContainer _entries;
    private readonly BoxContainer _emergencyRow;
    private readonly Button _dnd;

    /// <summary>The payphone, for what each call costs.</summary>
    public Content.Shared.CMU14.Telephone.CMUPayphoneComponent? Payphone { get; set; }

    private int CallCost => Payphone?.CallCost ?? 2;

    private RMCTelephoneBuiState? _state;

    public CMUPayphoneWindow()
    {
        _header = CMUPhoneStyle.Font(CMUPhoneStyle.SansBold, 16);
        _small = CMUPhoneStyle.Font(CMUPhoneStyle.Sans, 10);
        _lcd = CMUPhoneStyle.Font(CMUPhoneStyle.MonoBold, 12);
        _body = CMUPhoneStyle.Font(CMUPhoneStyle.Sans, 12);
        _bold = CMUPhoneStyle.Font(CMUPhoneStyle.SansBold, 11);
        _key = CMUPhoneStyle.Font(CMUPhoneStyle.SansBold, 11);

        MinSize = new Vector2(440, 580);
        SetSize = new Vector2(460, 660);

        var faceplate = CMUPhoneStyle.Panel(Steel, SteelEdge, 3, 12, 12);
        faceplate.HorizontalExpand = true;
        faceplate.VerticalExpand = true;
        var column = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 10 };
        faceplate.AddChild(column);

        // Header plate.
        var plate = CMUPhoneStyle.Panel(Plate, SteelDark, 2, 12, 6);
        var plateColumn = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        var title = CMUPhoneStyle.Text(Loc.GetString("cmu-payphone-header"), _header, PlateInk);
        title.HorizontalAlignment = HAlignment.Center;
        plateColumn.AddChild(title);
        _where = CMUPhoneStyle.Text(string.Empty, _small, PlateInkDim);
        _where.HorizontalAlignment = HAlignment.Center;
        _where.ClipText = true;
        plateColumn.AddChild(_where);
        plate.AddChild(plateColumn);
        column.AddChild(plate);

        // LCD.
        var lcd = CMUPhoneStyle.Panel(Lcd, LcdEdge, 2, 10, 5);
        _display = CMUPhoneStyle.Text(string.Empty, _lcd, LcdInk);
        _display.HorizontalAlignment = HAlignment.Center;
        _display.ClipText = true;
        lcd.AddChild(_display);
        column.AddChild(lcd);

        // The printed directory.
        var placard = CMUPhoneStyle.Panel(Placard, PlacardEdge, 1);
        placard.VerticalExpand = true;
        var placardColumn = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        var band = CMUPhoneStyle.Panel(Band, null, 0, 10, 4);
        band.AddChild(CMUPhoneStyle.Text(Loc.GetString("cmu-payphone-directory"), _bold, PlateInk));
        placardColumn.AddChild(band);
        var placardBody = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            VerticalExpand = true,
            Margin = new Thickness(10, 8),
        };
        _search = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("cmu-phone-search") };
        _search.OnTextChanged += _ => BuildEntries();
        placardBody.AddChild(_search);
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        _entries = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        scroll.AddChild(_entries);
        placardBody.AddChild(scroll);
        placardColumn.AddChild(placardBody);
        placard.AddChild(placardColumn);
        column.AddChild(placard);

        // Emergency key, and the service choice it opens.
        var emergency = CMUPhoneStyle.Key(Loc.GetString("cmu-payphone-911"), _key, Red, RedEdge, PlateInk);
        emergency.MinHeight = 36;
        emergency.OnPressed += _ => _emergencyRow!.Visible = !_emergencyRow.Visible;
        column.AddChild(emergency);

        _emergencyRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6, Visible = false };
        AddService(CMUEmergencyService.FireMedical, "cmu-911-service-fire-medical");
        AddService(CMUEmergencyService.LawEnforcement, "cmu-911-service-law");
        column.AddChild(_emergencyRow);

        // Payphones normally can't go do-not-disturb, but if one can, the switch is here.
        _dnd = CMUPhoneStyle.Key(string.Empty, _key, KeyFace, KeyEdge, Ink);
        _dnd.Visible = false;
        _dnd.OnPressed += _ =>
        {
            if (_state is { } s && (s.Dnd || s.CanDnd))
                OnDnd?.Invoke(!s.Dnd);
        };
        column.AddChild(_dnd);

        var notice = CMUPhoneStyle.Text(Loc.GetString("cmu-payphone-notice"), _small, PlateInkDim);
        notice.HorizontalAlignment = HAlignment.Center;
        notice.ClipText = true;
        column.AddChild(notice);

        Contents.AddChild(faceplate);
    }

    private void AddService(CMUEmergencyService service, string text)
    {
        var button = CMUPhoneStyle.Key(Loc.GetString(text), _key, Red, RedEdge, PlateInk);
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
        _where.Text = phoneName.ToUpperInvariant();
        _display.Text = state.Dnd
            ? Loc.GetString("cmu-payphone-lcd-dnd")
            : Loc.GetString("cmu-payphone-lcd-ready", ("amount", CallCost));


        _dnd.Visible = state.Dnd || state.CanDnd;
        _dnd.Text = Loc.GetString(state.Dnd ? "cmu-phone-dnd-on" : "cmu-phone-dnd-off");

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
            var section = CMUPhoneStyle.Text(category.ToUpperInvariant(), _bold, Band);
            section.Margin = new Thickness(0, 6, 0, 2);
            _entries.AddChild(section);
            _entries.AddChild(new PanelContainer { MinHeight = 1, PanelOverride = CMUPhoneStyle.Box(Band) });

            foreach (var phone in matches)
            {
                var row = new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    SeparationOverride = 8,
                    Margin = new Thickness(0, 2),
                };
                var name = CMUPhoneStyle.Text(phone.Name, _body, Ink);
                name.ClipText = true;
                name.HorizontalExpand = true;
                name.VerticalAlignment = VAlignment.Center;
                row.AddChild(name);
                var cost = Payphone?.CostFor(category) ?? CallCost;
                var call = CMUPhoneStyle.Key(Loc.GetString("cmu-payphone-call-cost", ("amount", cost)), _key, CallKey, Plate, PlateInk, 96);
                var id = phone.Id;
                call.OnPressed += _ => OnCall?.Invoke(id);
                row.AddChild(call);
                _entries.AddChild(row);
            }
        }

        if (!any)
        {
            var none = CMUPhoneStyle.Text(Loc.GetString(filter.Length > 0 ? "cmu-phone-no-match" : "cmu-phone-empty"), _body, InkFaint);
            none.HorizontalAlignment = HAlignment.Center;
            none.Margin = new Thickness(0, 16, 0, 0);
            _entries.AddChild(none);
        }
    }
}
