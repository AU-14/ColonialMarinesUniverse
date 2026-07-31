using Content.Shared.AU14.ColonyEconomy;
using Robust.Client.UserInterface;

namespace Content.Client.AU14.ColonyEconomy;

public sealed class DepartmentConsoleBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private DepartmentConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DepartmentConsoleWindow>();


        _window.WithdrawButton.OnPressed += _ =>
        {
            if (_window.WithdrawAmountInput.Text is { } text && float.TryParse(text, out var amount) && amount > 0)
            {
                SendPredictedMessage(new DepartmentConsoleWithdrawBuiMsg(amount));
                _window.WithdrawAmountInput.Text = string.Empty;
            }
        };

        _window.SetDefaultSalaryButton.OnPressed += _ =>
        {
            if (_window.DefaultSalaryInput.Text is { } text && int.TryParse(text, out var salary))
                SendPredictedMessage(new DepartmentConsoleSetDefaultSalaryBuiMsg(salary));
        };

        _window.SendAnnouncementButton.OnPressed += _ =>
        {
            var message = _window.AnnouncementInput.Text;
            if (!string.IsNullOrWhiteSpace(message))
            {
                SendPredictedMessage(new DepartmentConsoleAnnounceBuiMsg(message));
                _window.AnnouncementInput.Text = string.Empty;
            }
        };

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
