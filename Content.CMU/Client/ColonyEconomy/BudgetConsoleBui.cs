using Content.Shared.CMU14.ColonyEconomy;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.ColonyEconomy;

public sealed class BudgetConsoleBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private BudgetConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<BudgetConsoleWindow>();
        _window.OnWithdraw += amount => SendPredictedMessage(new BudgetConsoleWithdrawBuiMsg(amount));
        _window.OnDispenseSalaries += () => SendPredictedMessage(new BudgetConsoleDispenseSalariesBuiMsg());
        _window.OnTransfer += (department, amount) =>
            SendPredictedMessage(new BudgetConsoleTransferToDeptBuiMsg(department, amount));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not BudgetConsoleBuiState s)
            return;

        _window.UpdateState(s);
    }
}
