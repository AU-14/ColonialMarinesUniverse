using System.Numerics;
using Content.Client.CMU14.Interface;
using Content.Shared.CMU14.Ambassador;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.Ambassador;

/// <summary>
///     The diplomatic console, in the CRT theme: the embassy's budget, the trade and signal measures it can keep running,
///     a colony broadcast, the economy at a glance, shuttle radar and the third-party support menu.
/// </summary>
public sealed class AmbassadorConsoleWindow : DefaultWindow
{
    public event Action<int>? OnWithdraw;
    public event Action? OnToggleEmbargo;
    public event Action? OnToggleTradePact;
    public event Action? OnToggleCommsJam;
    public event Action? OnToggleSignalBoost;
    public event Action? OnToggleSignalJam;
    public event Action<string>? OnBroadcast;
    public event Action? OnScanRadar;
    public event Action? OnOpenThirdParty;

    private readonly CrtConsoleKit _kit = new();

    private readonly Label _subheading;
    private readonly PanelContainer _budgetTag;
    private readonly Label _budgetText;
    private readonly Label _footer;

    private readonly MeasureRow _embargo;
    private readonly MeasureRow _tradePact;
    private readonly MeasureRow _commsJam;
    private readonly MeasureRow _signalBoost;
    private readonly MeasureRow _signalJam;

    private readonly LineEdit _broadcast;
    private readonly Label _broadcastCost;
    private readonly BoxContainer _economy;
    private readonly Button _scan;
    private readonly BoxContainer _radar;

    public AmbassadorConsoleWindow()
    {
        Title = Loc.GetString("ambassador-console-title");
        MinSize = new Vector2(600, 560);
        SetSize = new Vector2(640, 720);

        _budgetTag = _kit.Tag(out _budgetText);
        _kit.Root.AddChild(_kit.Header(Loc.GetString("cmu-ambassador-heading"), out _subheading, _budgetTag));

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        var body = CrtConsoleKit.Column(8);
        body.HorizontalExpand = true;
        body.Margin = new Thickness(0, 0, 6, 0);
        scroll.AddChild(body);
        _kit.Root.AddChild(scroll);

        // Treasury.
        body.AddChild(_kit.Section(Loc.GetString("cmu-ambassador-treasury"), out var treasury));
        treasury.AddChild(CrtConsoleKit.Wrapping(Loc.GetString("cmu-ambassador-treasury-hint"), CrtTerminalPalette.TextDim));
        var withdraw = CrtConsoleKit.Row(4);
        foreach (var amount in new[] { 100, 250, 500 })
        {
            var button = _kit.Button(Loc.GetString("cmu-ambassador-withdraw", ("amount", amount)));
            button.HorizontalExpand = true;
            button.OnPressed += _ => OnWithdraw?.Invoke(amount);
            withdraw.AddChild(button);
        }

        treasury.AddChild(withdraw);

        // Trade measures.
        body.AddChild(_kit.Section(Loc.GetString("cmu-ambassador-trade"), out var trade));
        _embargo = new MeasureRow(_kit, "cmu-ambassador-embargo", "cmu-ambassador-embargo-desc", () => OnToggleEmbargo?.Invoke(), true);
        _tradePact = new MeasureRow(_kit, "cmu-ambassador-trade-pact", "cmu-ambassador-trade-pact-desc", () => OnToggleTradePact?.Invoke(), false);
        trade.AddChild(_embargo.Root);
        trade.AddChild(_tradePact.Root);

        // Signals and communications.
        body.AddChild(_kit.Section(Loc.GetString("cmu-ambassador-signals"), out var signals));
        _commsJam = new MeasureRow(_kit, "cmu-ambassador-comms-jam", "cmu-ambassador-comms-jam-desc", () => OnToggleCommsJam?.Invoke(), true);
        _signalBoost = new MeasureRow(_kit, "cmu-ambassador-signal-boost", "cmu-ambassador-signal-boost-desc", () => OnToggleSignalBoost?.Invoke(), false);
        _signalJam = new MeasureRow(_kit, "cmu-ambassador-signal-jam", "cmu-ambassador-signal-jam-desc", () => OnToggleSignalJam?.Invoke(), true);
        signals.AddChild(_commsJam.Root);
        signals.AddChild(_signalBoost.Root);
        signals.AddChild(_signalJam.Root);

        // Broadcast.
        body.AddChild(_kit.Section(Loc.GetString("cmu-ambassador-broadcast"), out var broadcast));
        var broadcastRow = CrtConsoleKit.Row(4);
        _broadcast = new LineEdit
        {
            HorizontalExpand = true,
            PlaceHolder = Loc.GetString("ambassador-console-broadcast-placeholder"),
        };
        _broadcast.OnTextEntered += _ => SendBroadcast();
        broadcastRow.AddChild(_broadcast);
        var send = _kit.Button(Loc.GetString("cmu-ambassador-send"), CmuButtonStyles.Variant.Affirm);
        send.MinWidth = 80;
        send.OnPressed += _ => SendBroadcast();
        broadcastRow.AddChild(send);
        broadcast.AddChild(broadcastRow);
        _broadcastCost = CrtConsoleKit.Text(string.Empty, _kit.Small, CrtTerminalPalette.TextDim);
        broadcast.AddChild(_broadcastCost);

        // Economy at a glance.
        body.AddChild(_kit.Section(Loc.GetString("cmu-ambassador-economy"), out _economy));

        // Shuttle radar.
        _scan = _kit.Button(string.Empty);
        _scan.OnPressed += _ => OnScanRadar?.Invoke();
        body.AddChild(_kit.Section(Loc.GetString("cmu-ambassador-radar"), out var radar, right: _scan));
        _radar = CrtConsoleKit.Column(3);
        radar.AddChild(_radar);

        // Third-party support.
        var open = _kit.Button(Loc.GetString("cmu-ambassador-open-third-party"), CmuButtonStyles.Variant.Affirm);
        open.OnPressed += _ => OnOpenThirdParty?.Invoke();
        body.AddChild(_kit.Section(Loc.GetString("cmu-ambassador-third-party"), out var thirdParty, right: open));
        thirdParty.AddChild(CrtConsoleKit.Wrapping(Loc.GetString("cmu-ambassador-third-party-hint"), CrtTerminalPalette.TextDim));

        _kit.Root.AddChild(_kit.Footer(out _footer));
        _footer.Text = Loc.GetString("cmu-ambassador-footer");

        Contents.AddChild(_kit.Tube);
    }

    private void SendBroadcast()
    {
        if (string.IsNullOrWhiteSpace(_broadcast.Text))
            return;

        OnBroadcast?.Invoke(_broadcast.Text);
        _broadcast.Text = string.Empty;
    }

    public void UpdateState(AmbassadorConsoleBuiState s)
    {
        _subheading.Text = Loc.GetString("cmu-ambassador-subheading", ("faction", s.FactionName.ToUpperInvariant()));
        CrtConsoleKit.SetTag(_budgetTag, _budgetText, CrtConsoleKit.Money(s.Budget),
            s.Budget > 0 ? CrtTerminalPalette.Accent : CrtTerminalPalette.Alert);

        _embargo.Update(s.EmbargoActive, s.EmbargoCostPerMinute);
        _tradePact.Update(s.TradePactActive, s.TradePactCostPerMinute);
        _commsJam.Update(s.CommsJamActive, s.CommsJamCostPerMinute);
        _signalBoost.Update(s.SignalBoostActive, s.SignalBoostCostPerMinute);
        _signalJam.Update(s.SignalJamActive, s.SignalJamCostPerMinute);

        _broadcastCost.Text = Loc.GetString("cmu-ambassador-broadcast-cost", ("cost", CrtConsoleKit.Money(s.BroadcastCost)));
        _scan.Text = Loc.GetString("cmu-ambassador-scan", ("cost", CrtConsoleKit.Money(s.RadarScanCost)));

        _kit.EconomyOverview(_economy, s.EconomyStatus);

        _radar.RemoveAllChildren();
        if (s.RadarList.Count == 0)
        {
            _radar.AddChild(CrtConsoleKit.Wrapping(Loc.GetString("cmu-ambassador-radar-none"), CrtTerminalPalette.TextDim));
        }
        else
        {
            foreach (var name in s.RadarList)
            {
                var row = CrtConsoleKit.Row(6);
                row.AddChild(_kit.Chip(Loc.GetString("cmu-ambassador-radar-contact"), CrtTerminalPalette.Caution));
                row.AddChild(CrtConsoleKit.Wrapping(name, CrtTerminalPalette.TextBright));
                _radar.AddChild(row);
            }
        }
    }

    /// <summary>
    ///     A measure the embassy keeps running for a cost per minute: what it does, whether it's on, and the switch.
    /// </summary>
    private sealed class MeasureRow
    {
        public readonly PanelContainer Root;
        private readonly CrtConsoleKit _kit;
        private readonly PanelContainer _marker;
        private readonly PanelContainer _state;
        private readonly Label _stateText;
        private readonly Label _cost;
        private readonly Button _toggle;
        private readonly bool _hostile;

        /// <param name="hostile">Whether turning it on works against someone, so its switch is red.</param>
        public MeasureRow(CrtConsoleKit kit, string name, string description, Action toggle, bool hostile)
        {
            _kit = kit;
            _hostile = hostile;
            Root = new PanelContainer
            {
                HorizontalExpand = true,
                PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface2 },
            };
            var row = CrtConsoleKit.Row();
            _marker = new PanelContainer { MinWidth = 4 };
            row.AddChild(_marker);

            var column = CrtConsoleKit.Column(2);
            column.HorizontalExpand = true;
            column.Margin = new Thickness(0, 6);
            var head = CrtConsoleKit.Row(6);
            var title = CrtConsoleKit.Text(Loc.GetString(name).ToUpperInvariant(), kit.Bold, CrtTerminalPalette.TextBright);
            title.ClipText = true;
            title.HorizontalExpand = true;
            head.AddChild(title);
            _state = kit.Tag(out _stateText);
            _stateText.FontOverride = kit.Small;
            _stateText.Margin = new Thickness(6, 1);
            head.AddChild(_state);
            column.AddChild(head);
            column.AddChild(CrtConsoleKit.Wrapping(Loc.GetString(description), CrtTerminalPalette.TextDim));
            _cost = CrtConsoleKit.Text(string.Empty, kit.Small, CrtTerminalPalette.Caution);
            column.AddChild(_cost);
            row.AddChild(column);

            _toggle = kit.Button(string.Empty);
            _toggle.MinWidth = 90;
            _toggle.VerticalAlignment = VAlignment.Center;
            _toggle.Margin = new Thickness(0, 0, 8, 0);
            _toggle.OnPressed += _ => toggle();
            row.AddChild(_toggle);
            Root.AddChild(row);
        }

        public void Update(bool active, float costPerMinute)
        {
            CrtConsoleKit.SetTag(_state, _stateText,
                Loc.GetString(active ? "cmu-ambassador-active" : "cmu-ambassador-inactive"),
                active ? (_hostile ? CrtTerminalPalette.Alert : CrtTerminalPalette.Accent) : CrtTerminalPalette.Surface4);
            _marker.PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = active ? (_hostile ? CrtTerminalPalette.Alert : CrtTerminalPalette.Accent) : CrtTerminalPalette.Line,
            };
            _cost.Text = Loc.GetString("cmu-ambassador-cost-per-minute", ("cost", CrtConsoleKit.Money(costPerMinute)));
            _toggle.Text = Loc.GetString(active ? "cmu-ambassador-stop" : "cmu-ambassador-start");
            CmuButtonStyles.Apply(_toggle, active
                ? CmuButtonStyles.Variant.Caution
                : _hostile ? CmuButtonStyles.Variant.Danger : CmuButtonStyles.Variant.Affirm);
            _toggle.Label.FontOverride = _kit.Small;
        }
    }
}
