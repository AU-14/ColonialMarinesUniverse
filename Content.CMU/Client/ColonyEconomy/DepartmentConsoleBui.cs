using Content.Shared.CMU14.ColonyEconomy;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.ColonyEconomy;

public sealed class DepartmentConsoleBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private DepartmentConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DepartmentConsoleWindow>();

        _window.OnWithdraw += amount => SendPredictedMessage(new DepartmentConsoleWithdrawBuiMsg(amount));
        _window.OnSetDefaultSalary += salary => SendPredictedMessage(new DepartmentConsoleSetDefaultSalaryBuiMsg(salary));
        _window.OnAnnounce += message => SendPredictedMessage(new DepartmentConsoleAnnounceBuiMsg(message));
        _window.OnFirePressed += netEntity =>
            SendPredictedMessage(new DepartmentConsoleFireBuiMsg(netEntity));
        _window.OnSetIndividualSalaryPressed += (netEntity, salary) =>
            SendPredictedMessage(new DepartmentConsoleSetIndividualSalaryBuiMsg(netEntity, salary));
        _window.OnRemoveOverridePressed += netEntity =>
            SendPredictedMessage(new DepartmentConsoleRemoveOverrideBuiMsg(netEntity));
        _window.OnOrderPressed += (catIdx, entIdx, reason, deliverTo) =>
            SendPredictedMessage(new DepartmentConsoleOrderBuiMsg(catIdx, entIdx, reason, deliverTo));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not DepartmentConsoleBuiState s)
            return;

        _window.UpdateState(s);
    }
}
