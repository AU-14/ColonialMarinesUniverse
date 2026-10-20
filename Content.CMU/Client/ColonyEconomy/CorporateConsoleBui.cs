using Content.Shared.CMU14.ColonyEconomy;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.ColonyEconomy;

public sealed class CorporateConsoleBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CorporateConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CorporateConsoleWindow>();
        _window.OnWithdraw += amount => SendPredictedMessage(new CorporateConsoleWithdrawBuiMsg(amount));
        _window.OnSetTariff += percent => SendPredictedMessage(new CorporateConsoleSetTariffBuiMsg(percent));
        _window.OnOpenThirdParty += () => SendPredictedMessage(new CorporateConsoleOpenThirdPartyBuiMsg());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not CorporateConsoleBuiState s)
            return;

        _window.UpdateState(s);
    }
}
