using Content.Shared._CMU14.FactionRoster;
using Robust.Client.UserInterface;

namespace Content.Client._CMU14.FactionRoster;

public sealed class FactionRosterConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private FactionRosterConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<FactionRosterConsoleWindow>();
        _window.OnRefresh += () => SendMessage(new FactionRosterConsoleRefreshBuiMsg());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not FactionRosterConsoleBuiState rosterState)
            return;

        _window.UpdateState(rosterState);
    }
}
