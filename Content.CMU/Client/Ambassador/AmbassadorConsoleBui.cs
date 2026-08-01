using Content.Shared.AU14.Ambassador;
using Robust.Client.UserInterface;

namespace Content.Client.AU14.Ambassador;

public sealed class AmbassadorConsoleBui : BoundUserInterface
{
    private AmbassadorConsoleWindow? _window;

    public AmbassadorConsoleBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<AmbassadorConsoleWindow>();

        _window.Withdraw100.OnPressed += _ => SendPredictedMessage(new AmbassadorWithdrawBuiMsg(100));
        _window.Withdraw250.OnPressed += _ => SendPredictedMessage(new AmbassadorWithdrawBuiMsg(250));
        _window.Withdraw500.OnPressed += _ => SendPredictedMessage(new AmbassadorWithdrawBuiMsg(500));
        _window.ToggleEmbargo.OnPressed += _ => SendPredictedMessage(new AmbassadorToggleEmbargoBuiMsg());
        _window.ToggleTradePact.OnPressed += _ => SendPredictedMessage(new AmbassadorToggleTradePactBuiMsg());
        _window.ToggleCommsJam.OnPressed += _ => SendPredictedMessage(new AmbassadorToggleCommsJamBuiMsg());
        _window.ToggleSignalBoost.OnPressed += _ => SendPredictedMessage(new AmbassadorToggleSignalBoostBuiMsg());
        _window.ToggleSignalJam.OnPressed += _ => SendPredictedMessage(new AmbassadorToggleSignalJamBuiMsg());
        _window.SendBroadcast.OnPressed += _ =>
        {
            var msg = _window.BroadcastInput.Text;
            if (!string.IsNullOrWhiteSpace(msg))
            {
                SendPredictedMessage(new AmbassadorBroadcastBuiMsg(msg));
                _window.BroadcastInput.Text = "";
            }
        };
        _window.OpenThirdPartyBtn.OnPressed += _ =>
        {
            SendPredictedMessage(new AmbassadorOpenThirdPartyBuiMsg());
        };
        _window.ScanRadarBtn.OnPressed += _ =>
        {
            SendPredictedMessage(new AmbassadorScanRadarBuiMsg());
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not AmbassadorConsoleBuiState s)
            return;

        _window.UpdateState(s);
    }
}

