using System.Linq;
using System.Numerics;
using Content.Client.Guidebook;
using Content.Shared.CMU14.CCVar;
using Content.Shared.CMU14.Radio;
using Content.Shared.Guidebook;
using Content.Shared.Radio;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Radio.ANPRC;

/// <summary>
///     The AN/PRC-117G operator's panel.
///
///     Laid out around what an operator needs in the order they need it: the set's screen says
///     what it is talking on, the banner under it says the one thing to do next and does it on
///     a press, and the tabs hold everything else a page at a time so nothing depends on
///     noticing that a long panel scrolls. The expert view swaps all of that for the set's own
///     faceplate - keypad, function switch and screen pages, worked the way the real radio is -
///     driving the same radio through the same actions, so neither view can do what the other
///     cannot. The window keeps no radio state of
///     its own - every line is rebuilt from what the server pushed.
/// </summary>
public sealed class ANPRCRadioWindow : DefaultWindow
{
    private static readonly List<ProtoId<GuideEntryPrototype>> Guides = new()
    {
        "AU14CommsFirstNet",
        "AU14CommsANPRC",
        "AU14CommsANPRCGuided",
        "AU14CommsANPRCExpert",
    };

    public event Action<int>? OnSelectSlot;
    public event Action? OnTogglePower;
    public event Action? OnToggleMonitor;
    public event Action<RadioMode>? OnSetMode;
    public event Action<bool>? OnSetScan;
    public event Action<RadioTxPower>? OnSetTxPower;
    public event Action<int>? OnSetSquelch;
    public event Action<string>? OnSetCallsign;
    public event Action<string>? OnAddSlot;
    public event Action<int>? OnDeleteSlot;
    public event Action<int, string>? OnRenameSlot;
    public event Action<int, ProtoId<RadioChannelPrototype>>? OnSetSlotChannel;
    public event Action<int>? OnClearSlot;
    public event Action? OnQuickSetup;
    public event Action? OnCryptoZeroize;
    public event Action? OnCryptoDestroy;
    public event Action? OnCryptoRecrypto;
    public event Action? OnRadioCheck;
    public event Action? OnOpenDirectory;
    public event Action? OnOpenPhone;
    public event Action<bool>? OnSetBurst;
    public event Action<bool>? OnSetPowerSave;
    public event Action<int>? OnSetPriorityWatch;
    public event Action<bool>? OnSetEmcon;
    public event Action<int, int>? OnSetRetrans;
    public event Action? OnPeakAntenna;
    public event Action? OnOtar;
    public event Action<int>? OnSetDwell;
    public event Action? OnJammerBearing;
    public event Action<string, string>? OnKeyTrial;
    public event Action? OnReturnToAuto;
    public event Action<int, string>? OnManualFrequency;
    public event Action<bool>? OnSetSweep;
    public event Action<int, RadioFrequency>? OnTuneContact;
    public event Action<bool>? OnPrintLog;

    // the BUI rebuilds this window every open. the size the operator dragged it to, the page
    // they were on and the log they had already read are kept for the session
    private static Vector2? _rememberedSize;
    private static ANPRCPage _rememberedPage = ANPRCPage.Nets;
    private static float _seenLogStamp = -1f;

    private readonly IConfigurationManager _config;
    private readonly ANPRCPanelData _data;
    private readonly ANPRCRadioActions _actions;

    private readonly Button _viewToggle;
    private readonly ANPRCStatusDisplay _display;

    private readonly BoxContainer _body;
    private readonly PanelContainer _banner;
    private readonly StyleBoxFlat _bannerStyle;
    private readonly Label _bannerTitle;
    private readonly RichTextLabel _bannerDetail;
    private readonly Button _bannerAction;
    private readonly BoxContainer _bannerMore;

    private readonly Dictionary<ANPRCPage, Button> _tabs = new();
    private readonly ScrollContainer _scroll;
    private readonly BoxContainer _pageHost;
    private readonly ANPRCMoreBelow _moreBelow;

    private readonly ANPRCNetsPage _netsPage;
    private readonly ANPRCLogPage _logPage;
    private readonly ANPRCSecurityPage _securityPage;
    private readonly ANPRCSearchPage _searchPage;
    private readonly ANPRCSettingsPage _settingsPage;

    private readonly ANPRCIntroPanel _intro;

    private readonly Button _powerButton;
    private readonly Button _radioCheckButton;
    private readonly Label _footerStatus;
    private readonly BoxContainer _footer;

    private readonly ANPRCFaceplate _faceplate;

    // the guided panel fits a small screen; the faceplate needs room for the keypad and switch
    private static readonly Vector2 GuidedMinSize = new(460f, 440f);
    private static readonly Vector2 FaceplateMinSize = ANPRCFaceplate.MinimumSize + new Vector2(16f, 60f);

    private ANPRCPage _page;
    private bool _expert;
    private bool _hasState;
    private bool _introOpen;
    private Action? _bannerCallback;
    private string _bannerKey = string.Empty;

    public ANPRCRadioWindow()
    {
        _config = IoCManager.Resolve<IConfigurationManager>();
        _data = new ANPRCPanelData(IoCManager.Resolve<IPrototypeManager>());
        _actions = BuildActions();

        _expert = _config.GetCVar(AU14CCVars.AnprcExpertView);

        Title = Loc.GetString("anprc-window-title");
        MinSize = _expert ? FaceplateMinSize : GuidedMinSize;
        SetSize = _expert ? FaceplateMinSize + new Vector2(40f, 100f) : new Vector2(540f, 720f);

        var chassis = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = ANPRCUi.Chassis },
        };
        Contents.AddChild(chassis);

        var root = ANPRCUi.Column(5, 6);
        chassis.AddChild(root);

        // ----- toolbar ---------------------------------------------------------------------------

        var toolbar = ANPRCUi.Row(4);
        var model = ANPRCUi.Label("AN/PRC-117G", ANPRCUi.HeadingText, ANPRCUi.Mono(11, true));
        model.HorizontalExpand = true;
        model.VerticalAlignment = VAlignment.Center;

        _viewToggle = ANPRCUi.Button(string.Empty, Loc.GetString("anprc-op-view-tooltip"), ToggleView);
        var help = ANPRCUi.Button(Loc.GetString("anprc-op-help"), Loc.GetString("anprc-op-help-tooltip"), () => ShowIntro(true));

        toolbar.AddChild(model);
        toolbar.AddChild(_viewToggle);
        toolbar.AddChild(help);
        root.AddChild(toolbar);

        // ----- the set's screen ------------------------------------------------------------------

        _display = new ANPRCStatusDisplay();
        root.AddChild(_display);

        // ----- body: banner, tabs, page ----------------------------------------------------------

        _body = ANPRCUi.Column(5);
        _body.VerticalExpand = true;
        root.AddChild(_body);

        var banner = ANPRCUi.Column(3, 6);
        _bannerTitle = ANPRCUi.Label(string.Empty, ANPRCUi.Good, ANPRCUi.Mono(12, true));
        _bannerDetail = ANPRCUi.Wrapped(string.Empty, ANPRCUi.Text);
        _bannerAction = ANPRCUi.Button(string.Empty, null, () => _bannerCallback?.Invoke());
        _bannerMore = ANPRCUi.Column(2);

        banner.AddChild(_bannerTitle);
        banner.AddChild(_bannerDetail);
        banner.AddChild(_bannerAction);
        banner.AddChild(_bannerMore);

        _bannerStyle = new StyleBoxFlat
        {
            BackgroundColor = ANPRCUi.Panel,
            BorderColor = ANPRCUi.Good,
            BorderThickness = new Thickness(2, 1, 1, 1),
        };

        _banner = new PanelContainer { PanelOverride = _bannerStyle };
        _banner.AddChild(banner);
        _body.AddChild(_banner);

        var tabs = ANPRCUi.Row(2);
        AddTab(tabs, ANPRCPage.Nets);
        AddTab(tabs, ANPRCPage.Log);
        AddTab(tabs, ANPRCPage.Security);
        // the search receiver is a faceplate technique. the guided panel stops a running search
        // through RETURN TO AUTO on the settings page instead
        AddTab(tabs, ANPRCPage.Settings);
        _body.AddChild(tabs);

        _scroll = new ScrollContainer
        {
            VerticalExpand = true,
            HScrollEnabled = false,
            // a scroll container measures as nothing, so it has to ask for its room
            MinHeight = 100,
        };

        _pageHost = ANPRCUi.Column(0, 6);
        _scroll.AddChild(_pageHost);

        var pageFrame = ANPRCUi.Framed(_scroll, ANPRCUi.PanelRaised, ANPRCUi.PanelEdge);
        pageFrame.VerticalExpand = true;
        _body.AddChild(pageFrame);

        // the scrollbar alone was not enough of a hint: an operator played for hours without
        // finding out the old panel scrolled. while there is more below, the panel says so
        _moreBelow = new ANPRCMoreBelow(_scroll, _pageHost);
        _body.AddChild(_moreBelow);

        _netsPage = new ANPRCNetsPage(_actions);
        _logPage = new ANPRCLogPage(_actions);
        _securityPage = new ANPRCSecurityPage(_actions);
        _searchPage = new ANPRCSearchPage(_actions);
        _settingsPage = new ANPRCSettingsPage(_actions);

        // ----- first-open briefing ---------------------------------------------------------------

        _intro = new ANPRCIntroPanel { VerticalExpand = true, Visible = false };
        _intro.OnDismissed += () =>
        {
            _config.SetCVar(AU14CCVars.AnprcIntroSeen, true);
            _config.SaveToFile();
            ShowIntro(false);
        };
        _intro.OnOpenGuide += () =>
        {
            IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<GuidebookSystem>().OpenHelp(Guides);
        };
        root.AddChild(_intro);

        // ----- footer ----------------------------------------------------------------------------

        _faceplate = new ANPRCFaceplate(_actions) { VerticalExpand = true };
        root.AddChild(_faceplate);

        var footer = _footer = ANPRCUi.Row(4);
        _powerButton = ANPRCUi.Button(string.Empty, null, () => OnTogglePower?.Invoke());
        _powerButton.MinWidth = 100;
        _radioCheckButton = ANPRCUi.Button(Loc.GetString("anprc-op-radio-check"), null, () => OnRadioCheck?.Invoke());
        _footerStatus = ANPRCUi.Label(string.Empty, ANPRCUi.TextDim, ANPRCUi.Mono(10));
        _footerStatus.HorizontalExpand = true;
        _footerStatus.Align = Label.AlignMode.Right;
        _footerStatus.VerticalAlignment = VAlignment.Center;

        footer.AddChild(_powerButton);
        footer.AddChild(_radioCheckButton);
        footer.AddChild(_footerStatus);
        root.AddChild(footer);

        UpdateViewToggle();
        ShowPage(_rememberedPage);
        ShowIntro(!_config.GetCVar(AU14CCVars.AnprcIntroSeen));
    }

    private ANPRCRadioActions BuildActions()
    {
        return new ANPRCRadioActions
        {
            SelectSlot = slot => OnSelectSlot?.Invoke(slot),
            AddSlot = label => OnAddSlot?.Invoke(label),
            DeleteSlot = slot => OnDeleteSlot?.Invoke(slot),
            ClearSlot = slot => OnClearSlot?.Invoke(slot),
            RenameSlot = (slot, label) => OnRenameSlot?.Invoke(slot, label),
            SetSlotChannel = (slot, channel) => OnSetSlotChannel?.Invoke(slot, channel),
            ManualFrequency = (slot, text) => OnManualFrequency?.Invoke(slot, text),
            QuickSetup = () => OnQuickSetup?.Invoke(),

            TogglePower = () => OnTogglePower?.Invoke(),
            ToggleMonitor = () => OnToggleMonitor?.Invoke(),
            SetMode = mode => OnSetMode?.Invoke(mode),
            SetScan = enabled => OnSetScan?.Invoke(enabled),
            SetTxPower = power => OnSetTxPower?.Invoke(power),
            SetSquelch = level => OnSetSquelch?.Invoke(level),
            SetCallsign = callsign => OnSetCallsign?.Invoke(callsign),

            CryptoZeroize = () => OnCryptoZeroize?.Invoke(),
            CryptoDestroy = () => OnCryptoDestroy?.Invoke(),
            CryptoRecrypto = () => OnCryptoRecrypto?.Invoke(),

            RadioCheck = () => OnRadioCheck?.Invoke(),
            OpenDirectory = () => OnOpenDirectory?.Invoke(),
            OpenPhone = () => OnOpenPhone?.Invoke(),
            SetBurst = enabled => OnSetBurst?.Invoke(enabled),
            SetPowerSave = enabled => OnSetPowerSave?.Invoke(enabled),
            SetPriorityWatch = slot => OnSetPriorityWatch?.Invoke(slot),
            SetEmcon = enabled => OnSetEmcon?.Invoke(enabled),
            SetRetrans = (a, b) => OnSetRetrans?.Invoke(a, b),
            PeakAntenna = () => OnPeakAntenna?.Invoke(),
            Otar = () => OnOtar?.Invoke(),
            SetDwell = kilohertz => OnSetDwell?.Invoke(kilohertz),
            JammerBearing = () => OnJammerBearing?.Invoke(),
            KeyTrial = (faction, trial) => OnKeyTrial?.Invoke(faction, trial),
            ReturnToAuto = () => OnReturnToAuto?.Invoke(),
            SetSweep = enabled => OnSetSweep?.Invoke(enabled),
            TuneContact = (slot, frequency) => OnTuneContact?.Invoke(slot, frequency),
            PrintLog = intercepts => OnPrintLog?.Invoke(intercepts),

            ShowPage = page =>
            {
                ShowIntro(false);
                ShowPage(page);
            },
        };
    }

    private void AddTab(BoxContainer row, ANPRCPage page)
    {
        var button = new Button
        {
            HorizontalExpand = true,
            ToolTip = Loc.GetString(TabLoc(page) + "-tooltip"),
        };

        button.OnPressed += _ => ShowPage(page);

        _tabs[page] = button;
        row.AddChild(button);
    }

    private static string TabLoc(ANPRCPage page) => page switch
    {
        ANPRCPage.Log => "anprc-op-tab-log",
        ANPRCPage.Security => "anprc-op-tab-security",
        ANPRCPage.Search => "anprc-op-tab-search",
        ANPRCPage.Settings => "anprc-op-tab-settings",
        _ => "anprc-op-tab-nets",
    };

    private Control PageControl(ANPRCPage page) => page switch
    {
        ANPRCPage.Log => _logPage,
        ANPRCPage.Security => _securityPage,
        ANPRCPage.Search => _searchPage,
        ANPRCPage.Settings => _settingsPage,
        _ => _netsPage,
    };

    private void ShowPage(ANPRCPage page)
    {
        // no search tab on the guided panel: anything sending it there lands where AUTO can end it
        if (page == ANPRCPage.Search)
            page = ANPRCPage.Settings;

        _page = page;
        _rememberedPage = page;

        _pageHost.RemoveAllChildren();
        _pageHost.AddChild(PageControl(page));
        _scroll.SetScrollValue(Vector2.Zero);

        if (page == ANPRCPage.Log && _hasState)
            _seenLogStamp = ANPRCLogPage.Newest(_data.State);

        Refresh();
    }

    /// <summary>Put the panel on a page from outside, e.g. a preview.</summary>
    public void OpenPage(ANPRCPage page) => ShowPage(page);

    private void ShowIntro(bool show)
    {
        _introOpen = show;
        ApplyView();
    }

    private void ToggleView()
    {
        _expert = !_expert;
        _config.SetCVar(AU14CCVars.AnprcExpertView, _expert);
        _config.SaveToFile();

        UpdateViewToggle();
        ApplyView();

        // the faceplate has to fit its keypad and switch, so the window grows to it if needed
        MinSize = _expert ? FaceplateMinSize : GuidedMinSize;

        if (IsOpen && (Size.X < MinSize.X || Size.Y < MinSize.Y))
        {
            var target = Vector2.Max(Size, MinSize);

            if (Root is { } root)
                target = Vector2.Min(target, root.Size * 0.95f);

            SetSize = Vector2.Max(target, MinSize);
        }

        Refresh();
    }

    // which half of the window is up: the guided panel or the faceplate, and the briefing over
    // either of them when it is open
    private void ApplyView()
    {
        _intro.Visible = _introOpen;

        _display.Visible = !_expert;
        _body.Visible = !_expert && !_introOpen;
        _footer.Visible = !_expert && !_introOpen;

        _faceplate.Visible = _expert && !_introOpen;
    }

    private void UpdateViewToggle()
    {
        _viewToggle.Text = Loc.GetString(_expert ? "anprc-op-view-expert" : "anprc-op-view-guided");
    }

    // ----- state -------------------------------------------------------------------------------------

    public void UpdateState(ANPRCRadioState state)
    {
        _data.SetState(state);

        // traffic already in the log when the panel first opens is not news
        if (!_hasState && _seenLogStamp < 0f)
            _seenLogStamp = ANPRCLogPage.Newest(state);

        var first = !_hasState;
        _hasState = true;

        if (_page == ANPRCPage.Log)
            _seenLogStamp = ANPRCLogPage.Newest(state);

        Refresh();

        // the first state fills a page that was built empty, and the scroll it lands on is
        // wherever the layout pass left it. an operator reads from the top
        if (first)
            _scroll.SetScrollValue(Vector2.Zero);
    }

    private void Refresh()
    {
        if (!_hasState)
            return;

        var state = _data.State;

        if (_expert)
        {
            _faceplate.UpdateState(state);
            return;
        }

        _display.Update(_data);
        var setupInBanner = UpdateBanner();
        _netsPage.SetupInBanner = setupInBanner;
        UpdateTabs();

        switch (_page)
        {
            case ANPRCPage.Nets:
                _netsPage.Update(_data);
                break;
            case ANPRCPage.Log:
                _logPage.Update(_data);
                break;
            case ANPRCPage.Security:
                _securityPage.Update(_data);
                break;
            case ANPRCPage.Search:
                _searchPage.Update(_data);
                break;
            case ANPRCPage.Settings:
                _settingsPage.Update(_data);
                break;
        }

        _powerButton.Text = Loc.GetString(state.Enabled ? "anprc-op-power-off" : "anprc-op-power-on");
        _powerButton.Disabled = !state.Enabled && !state.HasBattery;

        _radioCheckButton.Disabled = !_data.Ready || state.MonitorEnabled;
        _radioCheckButton.ToolTip = Loc.GetString(_radioCheckButton.Disabled
            ? "anprc-op-radio-check-unavailable"
            : "anprc-op-radio-check-tooltip");

        _footerStatus.Text = Loc.GetString(state.Planted
                ? "anprc-op-footer-planted"
                : state.IsEquipped
                    ? "anprc-op-footer-worn"
                    : "anprc-op-footer-stowed");
    }

    /// <summary>Returns whether the banner's own button is the quick setup.</summary>
    private bool UpdateBanner()
    {
        var issues = ANPRCDiagnosis.Diagnose(_data, _actions);

        // the secondary lines carry buttons of their own, and a state push lands every second
        // while the link drifts. rebuilding them under a half-finished click would eat it
        var key = string.Join("|", issues.Skip(1).Take(2).Select(issue => issue.Title + issue.ActionText));

        if (key != _bannerKey)
        {
            _bannerKey = key;
            RebuildBannerMore(issues);
        }

        if (issues.Count == 0)
        {
            _bannerStyle.BorderColor = ANPRCUi.Good;
            _bannerTitle.Text = Loc.GetString("anprc-op-ready-title", ("label", _data.SlotLabel(_data.State.ActiveSlot)));
            _bannerTitle.FontColorOverride = ANPRCUi.Good;

            ANPRCUi.SetWrapped(_bannerDetail, Loc.GetString(_data.Relay.Relaying
                    ? "anprc-op-ready-detail-relaying"
                    : "anprc-op-ready-detail",
                ("count", _data.Relay.RelayedNets.Count),
                ("range", (int) _data.Relay.FullRange)), ANPRCUi.Text);

            _bannerDetail.Visible = true;
            _bannerAction.Visible = false;
            _bannerCallback = null;

            return false;
        }

        var first = issues[0];
        var colour = ANPRCUi.Severity(first.Severity);

        _bannerStyle.BorderColor = colour;
        _bannerTitle.Text = first.Title;
        _bannerTitle.FontColorOverride = colour;

        ANPRCUi.SetWrapped(_bannerDetail, first.Detail, ANPRCUi.Text);
        _bannerDetail.Visible = first.Detail.Length > 0;

        _bannerAction.Visible = first.Action != null;
        _bannerAction.Text = first.ActionText ?? string.Empty;
        _bannerCallback = first.Action;

        return first.IsQuickSetup;
    }

    private void RebuildBannerMore(List<ANPRCIssue> issues)
    {
        _bannerMore.RemoveAllChildren();

        // the rest stay one line each, so the banner never pushes the pages off the window
        foreach (var issue in issues.Skip(1).Take(2))
        {
            var row = ANPRCUi.Row(4);
            var line = ANPRCUi.Label(Loc.GetString("anprc-op-also", ("issue", issue.Title)), ANPRCUi.Severity(issue.Severity));
            line.HorizontalExpand = true;
            line.ClipText = true;
            line.ToolTip = issue.Detail;
            line.MouseFilter = MouseFilterMode.Stop;
            row.AddChild(line);

            if (issue.Action is { } action)
            {
                row.AddChild(ANPRCUi.Button(issue.ActionText ?? string.Empty, issue.Detail, action));
            }

            _bannerMore.AddChild(row);
        }
    }

    private void UpdateTabs()
    {
        var state = _data.State;
        var unread = state.NetLog.Count(entry => entry.Timestamp > _seenLogStamp);

        foreach (var (page, button) in _tabs)
        {
            var text = Loc.GetString(TabLoc(page));

            if (page == ANPRCPage.Log && unread > 0 && _page != ANPRCPage.Log)
                text = Loc.GetString("anprc-op-tab-badge", ("tab", text), ("count", unread));
            else if (page == ANPRCPage.Security && !_data.Secured && state.Mode != RadioMode.PlainText)
                text = Loc.GetString("anprc-op-tab-alert", ("tab", text));
            else if (page == ANPRCPage.Search && state.SweepEnabled)
                text = Loc.GetString("anprc-op-tab-alert", ("tab", text));

            button.Text = text;
            button.Pressed = page == _page;
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        _moreBelow.Check();
        _intro.MoreBelow.Check();
    }

    // clamp to the viewport so the panel never opens taller than a small screen, and restore the
    // size the operator last dragged it to
    protected override void EnteredTree()
    {
        base.EnteredTree();

        var target = _rememberedSize ?? SetSize;

        if (Root is { } root)
            target = Vector2.Min(target, root.Size * 0.95f);

        SetSize = Vector2.Max(target, MinSize);
    }

    protected override void Resized()
    {
        base.Resized();

        if (IsInsideTree)
            _rememberedSize = Size;
    }
}
