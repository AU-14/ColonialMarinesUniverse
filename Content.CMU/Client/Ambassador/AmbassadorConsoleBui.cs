using Content.Shared.CMU14.Ambassador;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Ambassador;

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

        _window.OnWithdraw += amount => SendPredictedMessage(new AmbassadorWithdrawBuiMsg(amount));
        _window.OnToggleEmbargo += () => SendPredictedMessage(new AmbassadorToggleEmbargoBuiMsg());
        _window.OnToggleTradePact += () => SendPredictedMessage(new AmbassadorToggleTradePactBuiMsg());
        _window.OnToggleCommsJam += () => SendPredictedMessage(new AmbassadorToggleCommsJamBuiMsg());
        _window.OnToggleSignalBoost += () => SendPredictedMessage(new AmbassadorToggleSignalBoostBuiMsg());
        _window.OnToggleSignalJam += () => SendPredictedMessage(new AmbassadorToggleSignalJamBuiMsg());
        _window.OnBroadcast += msg => SendPredictedMessage(new AmbassadorBroadcastBuiMsg(msg));
        _window.OnOpenThirdParty += () => SendPredictedMessage(new AmbassadorOpenThirdPartyBuiMsg());
        _window.OnScanRadar += () => SendPredictedMessage(new AmbassadorScanRadarBuiMsg());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not AmbassadorConsoleBuiState s)
            return;

        _window.UpdateState(s);
    }
}
