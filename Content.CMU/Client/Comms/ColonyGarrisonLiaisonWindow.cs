using System.Numerics;
using Content.Client.CMU14.Interface;
using Content.Shared.CMU14;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Comms;

/// <summary>
///     The Colony Garrison Liaison Office's screen, in the CRT theme: one message box that is sent out as a colony
///     announcement, and a readout of when the office can send again.
/// </summary>
public sealed class ColonyGarrisonLiaisonWindow : DefaultWindow
{
    public event Action<string>? OnSend;
    public event Action<int>? OnTransfer;

    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();
    private readonly CrtConsoleKit _kit = new();

    private readonly PanelContainer _statusTag;
    private readonly Label _statusText;
    private readonly LineEdit _message;
    private readonly Label _counter;
    private readonly Button _send;
    private readonly Label _garrisonFunds;
    private readonly Label _colonyFunds;
    private readonly LineEdit _amount;

    private int _maxLength;
    private TimeSpan _nextSendAt;

    public ColonyGarrisonLiaisonWindow()
    {
        Title = Loc.GetString("colony-garrison-liaison-title");
        MinSize = new Vector2(460, 420);
        SetSize = new Vector2(520, 520);

        _statusTag = _kit.Tag(out _statusText);
        _kit.Root.AddChild(_kit.Header(Loc.GetString("colony-garrison-liaison-heading"), out var subheading, _statusTag));
        subheading.Text = Loc.GetString("colony-garrison-liaison-subheading");

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        var body = CrtConsoleKit.Column(8);
        body.HorizontalExpand = true;
        scroll.AddChild(body);
        _kit.Root.AddChild(scroll);

        body.AddChild(_kit.Section(Loc.GetString("colony-garrison-liaison-compose"), out var compose));
        compose.AddChild(CrtConsoleKit.Wrapping(Loc.GetString("colony-garrison-liaison-hint"), CrtTerminalPalette.TextDim));

        _message = new LineEdit
        {
            HorizontalExpand = true,
            PlaceHolder = Loc.GetString("colony-garrison-liaison-placeholder"),
        };
        _message.OnTextChanged += _ => UpdateCounter();
        _message.OnTextEntered += _ => Send();
        compose.AddChild(_message);

        var row = CrtConsoleKit.Row(8);
        _counter = CrtConsoleKit.Text(string.Empty, _kit.Small, CrtTerminalPalette.TextDim);
        _counter.HorizontalExpand = true;
        _counter.VerticalAlignment = VAlignment.Center;
        row.AddChild(_counter);
        _send = _kit.Button(Loc.GetString("colony-garrison-liaison-send"), CmuButtonStyles.Variant.Affirm);
        _send.MinWidth = 110;
        _send.OnPressed += _ => Send();
        row.AddChild(_send);
        compose.AddChild(row);

        body.AddChild(_kit.Section(Loc.GetString("colony-garrison-liaison-funds"), out var funds));
        funds.AddChild(CrtConsoleKit.Wrapping(Loc.GetString("colony-garrison-liaison-funds-hint"), CrtTerminalPalette.TextDim));

        var balances = CrtConsoleKit.Row(8);
        var garrison = _kit.Readout(Loc.GetString("colony-garrison-liaison-garrison-funds"), Loc.GetString("colony-garrison-liaison-no-account"), out _garrisonFunds);
        var colony = _kit.Readout(Loc.GetString("colony-garrison-liaison-colony-funds"), "-", out _colonyFunds);
        garrison.HorizontalExpand = true;
        colony.HorizontalExpand = true;
        balances.AddChild(garrison);
        balances.AddChild(colony);
        funds.AddChild(balances);

        _amount = new LineEdit
        {
            HorizontalExpand = true,
            PlaceHolder = Loc.GetString("colony-garrison-liaison-amount-placeholder"),
            IsValid = text => text.Length == 0 || int.TryParse(text, out var value) && value > 0,
        };
        funds.AddChild(_amount);

        var toColony = _kit.Button(Loc.GetString("colony-garrison-liaison-to-colony"));
        toColony.HorizontalExpand = true;
        toColony.OnPressed += _ => Transfer();
        funds.AddChild(toColony);

        _kit.Root.AddChild(_kit.Footer(out var footer));
        footer.Text = Loc.GetString("colony-garrison-liaison-footer");

        Contents.AddChild(_kit.Tube);
        UpdateCounter();
        UpdateStatus();
    }

    public void UpdateState(ColonyGarrisonLiaisonBuiState state)
    {
        _maxLength = state.MaxLength;
        _nextSendAt = state.NextSendAt;
        _message.IsValid = text => _maxLength <= 0 || text.Length <= _maxLength;
        UpdateCounter();
        UpdateStatus();
    }

    public void UpdateFunds(ColonyGarrisonLiaisonFundsBuiMsg funds)
    {
        _garrisonFunds.Text = funds.HasGarrison
            ? CrtConsoleKit.Money(funds.GarrisonFunds)
            : Loc.GetString("colony-garrison-liaison-no-account");
        _colonyFunds.Text = CrtConsoleKit.Money(funds.ColonyFunds);
    }

    private void Transfer()
    {
        if (!int.TryParse(_amount.Text, out var amount) || amount <= 0)
            return;

        OnTransfer?.Invoke(amount);
        _amount.Text = string.Empty;
    }


    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        UpdateStatus();
    }

    private void Send()
    {
        var text = _message.Text.Trim();
        if (text.Length == 0 || _timing.CurTime < _nextSendAt)
            return;

        OnSend?.Invoke(text);
        _message.Text = string.Empty;
    }

    private void UpdateCounter()
    {
        _counter.Text = _maxLength > 0
            ? Loc.GetString("colony-garrison-liaison-counter", ("count", _message.Text.Length), ("max", _maxLength))
            : string.Empty;
    }

    private void UpdateStatus()
    {
        var remaining = _nextSendAt - _timing.CurTime;
        if (remaining > TimeSpan.Zero)
        {
            CrtConsoleKit.SetTag(_statusTag, _statusText, Loc.GetString("colony-garrison-liaison-status-wait",
                ("seconds", (int) Math.Ceiling(remaining.TotalSeconds))), CrtTerminalPalette.Caution);
            _send.Disabled = true;
            return;
        }

        CrtConsoleKit.SetTag(_statusTag, _statusText, Loc.GetString("colony-garrison-liaison-status-ready"), CrtTerminalPalette.Accent);
        _send.Disabled = false;
    }
}
