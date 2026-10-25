// SPDX-License-Identifier: AGPL-3.0-only
using Content.Shared.CMU14.WorldMarket;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.WorldMarket;

// Client side of the console of the colony administration: opens the window, gives it the state and takes what it asks the server to do.
public sealed class ColonyConsoleBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private ColonyConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<ColonyConsoleWindow>();
        _window.OnSend += message => SendMessage(message);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window != null && state is ColonyConsoleState console)
            _window.SetState(console);
    }
}
