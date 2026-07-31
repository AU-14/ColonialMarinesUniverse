using Content.Shared.AU14.AllianceConsole;
using Robust.Client.UserInterface;

namespace Content.Client.AU14.AllianceConsole;

public sealed class AllianceConsoleBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private AllianceConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<AllianceConsoleWindow>();

        _window.OnSetStatus += (faction, status) =>
            SendPredictedMessage(new AllianceConsoleSetFactionStatusMsg(faction, status));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not AllianceConsoleBuiState s || _window == null)
            return;

        _window.UpdateState(s);
    }
}
