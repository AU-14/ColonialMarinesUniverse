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
///     A desk, wall or field phone, drawn as the phone itself: a dark bakelite body with a brass name plate and an
///     amber status display, a paper directory with index tabs, and the do-not-disturb and 911 keys underneath.
/// </summary>
public sealed class CMUDeskPhoneWindow : DefaultWindow, ICMUPhoneWindow
{
    private static readonly Color Body = Color.FromHex("#211d19");
    private static readonly Color BodyEdge = Color.FromHex("#3d352c");
    private static readonly Color Brass = Color.FromHex("#7d6538");
    private static readonly Color BrassEdge = Color.FromHex("#4f3e1f");
    private static readonly Color BrassInk = Color.FromHex("#1a1309");
    private static readonly Color Display = Color.FromHex("#140e06");
    private static readonly Color Amber = Color.FromHex("#c98a3a");
    private static readonly Color AmberDim = Color.FromHex("#7a5422");
    private static readonly Color Paper = Color.FromHex("#8f8673");
    private static readonly Color PaperTab = Color.FromHex("#6f6858");
    private static readonly Color PaperLine = Color.FromHex("#5f5849");
    private static readonly Color Ink = Color.FromHex("#18150f");
    private static readonly Color InkFaint = Color.FromHex("#3d382d");
    private static readonly Color KeyFace = Color.FromHex("#35302a");
    private static readonly Color KeyEdge = Color.FromHex("#15120f");
    private static readonly Color KeyInk = Color.FromHex("#b3aa95");
    private static readonly Color Red = Color.FromHex("#7a221d");
    private static readonly Color RedEdge = Color.FromHex("#5e1210");

    public event Action<NetEntity>? OnCall;
    public event Action<bool>? OnDnd;
    public event Action<CMUEmergencyService>? OnEmergency;

    private readonly Font _plate;
    private readonly Font _display;
    private readonly Font _paper;
    private readonly Font _paperBold;
    private readonly Font _key;

    private readonly Label _name;
    private readonly Label _status;
    private readonly BoxContainer _tabs;
    private readonly LineEdit _search;
    private readonly BoxContainer _entries;
    private readonly Button _dnd;
    private readonly BoxContainer _emergencyRow;

    private RMCTelephoneBuiState? _state;
    private string? _tab;

    public CMUDeskPhoneWindow()
    {
        _plate = CMUPhoneStyle.Font(CMUPhoneStyle.SansBold, 13);
        _display = CMUPhoneStyle.Font(CMUPhoneStyle.MonoBold, 12);
        _paper = CMUPhoneStyle.Font(CMUPhoneStyle.Sans, 12);
        _paperBold = CMUPhoneStyle.Font(CMUPhoneStyle.SansBold, 12);
        _key = CMUPhoneStyle.Font(CMUPhoneStyle.SansBold, 11);

        MinSize = new Vector2(500, 520);
        SetSize = new Vector2(540, 600);

        var body = CMUPhoneStyle.Panel(Body, BodyEdge, 2, 12, 12);
        body.HorizontalExpand = true;
        body.VerticalExpand = true;
        var column = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 10 };
        body.AddChild(column);

        // Name plate and status display.
        var top = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10 };
        var plate = CMUPhoneStyle.Panel(Brass, BrassEdge, 1, 10, 5);
        plate.HorizontalExpand = true;
        _name = CMUPhoneStyle.Text(string.Empty, _plate, BrassInk);
        _name.ClipText = true;
        plate.AddChild(_name);
        top.AddChild(plate);
        var display = CMUPhoneStyle.Panel(Display, BodyEdge, 1, 10, 5);
        display.MinWidth = 170;
        _status = CMUPhoneStyle.Text(string.Empty, _display, Amber);
        _status.Align = Label.AlignMode.Center;
        display.AddChild(_status);
        top.AddChild(display);
        column.AddChild(top);

        // The directory card.
        var card = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalExpand = true };
        _tabs = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 2 };
        var tabScroll = new ScrollContainer { HScrollEnabled = true, VScrollEnabled = false, MinHeight = 28 };
        tabScroll.AddChild(_tabs);
        card.AddChild(tabScroll);

        var paper = CMUPhoneStyle.Panel(Paper, PaperLine, 1, 10, 8);
        paper.VerticalExpand = true;
        var paperColumn = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
        var heading = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        var title = CMUPhoneStyle.Text(Loc.GetString("cmu-phone-directory"), _paperBold, Ink);
        title.VerticalAlignment = VAlignment.Center;
        heading.AddChild(title);
        _search = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("cmu-phone-search") };
        _search.OnTextChanged += _ => BuildEntries();
        heading.AddChild(_search);
        paperColumn.AddChild(heading);
        paperColumn.AddChild(new PanelContainer { MinHeight = 2, PanelOverride = CMUPhoneStyle.Box(Ink) });
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        _entries = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        scroll.AddChild(_entries);
        paperColumn.AddChild(scroll);
        paper.AddChild(paperColumn);
        card.AddChild(paper);
        column.AddChild(card);

        // Keys.
        var keys = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        _dnd = CMUPhoneStyle.Key(string.Empty, _key, KeyFace, KeyEdge, KeyInk, 190);
        _dnd.OnPressed += _ =>
        {
            if (_state is { } s && (s.Dnd || s.CanDnd))
                OnDnd?.Invoke(!s.Dnd);
        };
        keys.AddChild(_dnd);
        keys.AddChild(new Control { HorizontalExpand = true });
        var emergency = CMUPhoneStyle.Key(Loc.GetString("cmu-phone-911"), _key, Red, RedEdge, KeyInk, 120);
        emergency.OnPressed += _ => _emergencyRow!.Visible = !_emergencyRow.Visible;
        keys.AddChild(emergency);
        column.AddChild(keys);

        // 911: pick the service.
        _emergencyRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6, Visible = false };
        var prompt = CMUPhoneStyle.Text(Loc.GetString("cmu-911-picker-question"), _paper, Amber);
        prompt.HorizontalExpand = true;
        prompt.ClipText = true;
        prompt.VerticalAlignment = VAlignment.Center;
        _emergencyRow.AddChild(prompt);
        AddService(CMUEmergencyService.FireMedical, "cmu-911-service-fire-medical");
        AddService(CMUEmergencyService.LawEnforcement, "cmu-911-service-law");
        column.AddChild(_emergencyRow);

        Contents.AddChild(body);
    }

    private void AddService(CMUEmergencyService service, string text)
    {
        var button = CMUPhoneStyle.Key(Loc.GetString(text), _key, Red, RedEdge, KeyInk);
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
        _name.Text = Loc.GetString("cmu-phone-extension", ("name", phoneName.ToUpperInvariant()));

        _status.Text = Loc.GetString(state.Dnd ? "cmu-phone-status-dnd" : "cmu-phone-status-ready");
        _status.FontColorOverride = state.Dnd ? Color.FromHex("#c4543a") : Amber;

        _dnd.Text = Loc.GetString(state.Dnd ? "cmu-phone-dnd-on" : "cmu-phone-dnd-off");
        _dnd.Disabled = !state.Dnd && !state.CanDnd;
        _dnd.Label.FontColorOverride = _dnd.Disabled ? AmberDim : state.Dnd ? Amber : KeyInk;
        _dnd.ToolTip = Loc.GetString(state.Dnd
            ? "phone-dnd-tooltip-enabled"
            : state.CanDnd ? "phone-dnd-tooltip-disabled" : "phone-dnd-tooltip-locked");

        BuildTabs();
        BuildEntries();
    }

    private void BuildTabs()
    {
        _tabs.RemoveAllChildren();
        if (_state is not { } state)
            return;

        var groups = CMUPhoneStyle.Group(state.Phones);
        if (_tab == null || groups.All(g => g.Category != _tab))
            _tab = groups.FirstOrDefault().Category;

        foreach (var (category, _) in groups)
        {
            var selected = category == _tab;
            var tab = new Button
            {
                Text = category,
                StyleBoxOverride = CMUPhoneStyle.Box(selected ? Paper : PaperTab, PaperLine, 1, 10, 3),
            };
            tab.Label.FontOverride = selected ? _paperBold : _paper;
            tab.Label.FontColorOverride = selected ? Ink : InkFaint;
            var captured = category;
            tab.OnPressed += _ =>
            {
                _tab = captured;
                BuildTabs();
                BuildEntries();
            };
            _tabs.AddChild(tab);
        }
    }

    private void BuildEntries()
    {
        _entries.RemoveAllChildren();
        if (_state is not { } state)
            return;

        // Searching looks through every tab; otherwise just the open one.
        var filter = _search.Text.Trim();
        var searching = filter.Length > 0;
        var shown = CMUPhoneStyle.Group(state.Phones)
            .Where(g => searching || g.Category == _tab)
            .SelectMany(g => g.Phones.Select(p => (g.Category, Phone: p)))
            .Where(e => !searching || e.Phone.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (shown.Count == 0)
        {
            var none = CMUPhoneStyle.Text(Loc.GetString(searching ? "cmu-phone-no-match" : "cmu-phone-empty"), _paper, InkFaint);
            none.HorizontalAlignment = HAlignment.Center;
            none.Margin = new Thickness(0, 16, 0, 0);
            _entries.AddChild(none);
            return;
        }

        foreach (var (category, phone) in shown)
        {
            var row = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 8,
                Margin = new Thickness(0, 3),
            };
            var name = CMUPhoneStyle.Text(phone.Name, _paper, Ink);
            name.ClipText = true;
            name.HorizontalExpand = true;
            name.VerticalAlignment = VAlignment.Center;
            row.AddChild(name);
            if (searching)
            {
                var where = CMUPhoneStyle.Text(category, _paper, InkFaint);
                where.VerticalAlignment = VAlignment.Center;
                row.AddChild(where);
            }

            var dial = CMUPhoneStyle.Key(Loc.GetString("cmu-phone-dial"), _key, KeyFace, KeyEdge, KeyInk, 70);
            var id = phone.Id;
            dial.OnPressed += _ => OnCall?.Invoke(id);
            row.AddChild(dial);
            _entries.AddChild(row);
            _entries.AddChild(new PanelContainer { MinHeight = 1, PanelOverride = CMUPhoneStyle.Box(PaperLine) });
        }
    }
}
