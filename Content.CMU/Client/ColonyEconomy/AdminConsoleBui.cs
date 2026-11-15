using Content.Shared.CMU14.ColonyEconomy;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.ColonyEconomy;

public sealed class AdminConsoleBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private AdminConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<AdminConsoleWindow>();

        _window.SetTaxButton.OnPressed += _ =>
        {
            if (_window.TaxInput.Text is { } text && float.TryParse(text, out var pct))
                SendPredictedMessage(new AdminConsoleSetTaxBuiMsg(pct));
        };

        _window.SetIncomeTaxButton.OnPressed += _ =>
        {
            if (_window.IncomeTaxInput.Text is { } text && float.TryParse(text, out var pct))
                SendPredictedMessage(new AdminConsoleSetIncomeTaxBuiMsg(pct));
        };

        // RUMC: the button that sets the tax on the parcels sold through the trade platforms
        _window.SetTradeTaxButton.OnPressed += _ =>
        {
            if (_window.TradeTaxInput.Text is { } text && float.TryParse(text, out var pct))
                SendPredictedMessage(new AdminConsoleSetTradeTaxBuiMsg(pct));
        };

        _window.OpenThirdPartyBtn.OnPressed += _ =>
        {
            SendPredictedMessage(new AdminConsoleOpenThirdPartyBuiMsg());
        };

        // RUMC: the window of the government of the colony (the reforms and the foreign policy)
        _window.OpenGovernmentBtn.OnPressed += _ =>
        {
            SendPredictedMessage(new AdminConsoleOpenGovernmentBuiMsg());
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not AdminConsoleBuiState s)
            return;
        _window.UpdateState(s);
    }
}
