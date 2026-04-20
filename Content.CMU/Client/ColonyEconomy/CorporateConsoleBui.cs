using Content.Shared.AU14.ColonyEconomy;
using Robust.Client.UserInterface;

namespace Content.Client.AU14.ColonyEconomy;

public sealed class CorporateConsoleBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CorporateConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CorporateConsoleWindow>();

        _window.SetTariffButton.OnPressed += _ =>
        {
            if (_window.TariffInput.Text is { } text && float.TryParse(text, out var pct))
                SendPredictedMessage(new CorporateConsoleSetTariffBuiMsg(pct));
        };

        _window.Withdraw100.OnPressed += _ => SendPredictedMessage(new CorporateConsoleWithdrawBuiMsg(100));
        _window.Withdraw250.OnPressed += _ => SendPredictedMessage(new CorporateConsoleWithdrawBuiMsg(250));
        _window.Withdraw500.OnPressed += _ => SendPredictedMessage(new CorporateConsoleWithdrawBuiMsg(500));

        _window.OpenThirdPartyBtn.OnPressed += _ =>
        {
            SendPredictedMessage(new CorporateConsoleOpenThirdPartyBuiMsg());
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not CorporateConsoleBuiState s)
            return;
        _window.UpdateState(s);
    }
}

