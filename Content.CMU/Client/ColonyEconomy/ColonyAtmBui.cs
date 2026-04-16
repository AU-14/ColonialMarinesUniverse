using Content.Shared.AU14.ColonyEconomy;
using Robust.Client.UserInterface;

namespace Content.Client.AU14.ColonyEconomy;

public sealed class ColonyAtmBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private ColonyAtmWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ColonyAtmWindow>();

        _window.Withdraw50.OnPressed += _ => SendPredictedMessage(new ColonyAtmWithdrawBuiMsg(10));
        _window.Withdraw100.OnPressed += _ => SendPredictedMessage(new ColonyAtmWithdrawBuiMsg(25));
        _window.Withdraw250.OnPressed += _ => SendPredictedMessage(new ColonyAtmWithdrawBuiMsg(100));
        _window.Withdraw500.OnPressed += _ => SendPredictedMessage(new ColonyAtmWithdrawBuiMsg(250));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not ColonyAtmBuiState s)
            return;

        _window.OwnerLabel.Text = $"Account: {s.OwnerName}";
        _window.BalanceLabel.Text = $"Balance: ${s.Balance}";
        _window.IncomeTaxLabel.Text = s.IncomeTaxPercent > 0
            ? $"Income Tax: {s.IncomeTaxPercent:F0}% (applied on withdrawal)"
            : "No income tax.";
    }
}

