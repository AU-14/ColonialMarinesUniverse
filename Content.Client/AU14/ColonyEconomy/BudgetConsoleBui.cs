using Content.Shared.AU14.ColonyEconomy;
using Robust.Client.UserInterface;

namespace Content.Client.AU14.ColonyEconomy;

public sealed class BudgetConsoleBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{

    private BudgetConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<BudgetConsoleWindow>();

        _window.Withdraw100.OnPressed += _ =>  SendPredictedMessage(new BudgetConsoleWithdrawBuiMsg(100));
        _window.Withdraw250.OnPressed += _ => SendPredictedMessage(new BudgetConsoleWithdrawBuiMsg(250));
        _window.Withdraw500.OnPressed += _ => SendPredictedMessage(new BudgetConsoleWithdrawBuiMsg(500));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not BudgetConsoleBuiState s)
            return;

        _window.BudgetLabel.Text = $"Current Budget: {s.Budget:C}";
        var withdrawString = s.lastWithdrawals;
        _window.withdrawbox.Text = withdrawString;

    }
}
