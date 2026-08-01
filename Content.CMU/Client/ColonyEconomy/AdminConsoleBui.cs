using Content.Shared.AU14.ColonyEconomy;
using Robust.Client.UserInterface;

namespace Content.Client.AU14.ColonyEconomy;

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

        _window.OpenThirdPartyBtn.OnPressed += _ =>
        {
            SendPredictedMessage(new AdminConsoleOpenThirdPartyBuiMsg());
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not AdminConsoleBuiState s)
            return;
        _window.UpdateState(s);
    }
}
