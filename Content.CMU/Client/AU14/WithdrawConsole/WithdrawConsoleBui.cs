using Content.Shared.AU14.WithdrawConsole;
using Robust.Client.UserInterface;

namespace Content.Client.AU14.WithdrawConsole;

public sealed class WithdrawConsoleBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private WithdrawConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<WithdrawConsoleWindow>();

        _window.ToggleWithdrawButton.OnPressed += _ => SendPredictedMessage(new WithdrawConsoleToggleWithdrawMsg());
        _window.CancelButton.OnPressed += _ => SendPredictedMessage(new WithdrawConsoleCancelMsg());
        _window.StalemateButton.OnPressed += _ => SendPredictedMessage(new WithdrawConsoleToggleStalemateMsg());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not WithdrawConsoleBuiState s || _window == null)
            return;

        _window.UpdateState(s);
    }
}
