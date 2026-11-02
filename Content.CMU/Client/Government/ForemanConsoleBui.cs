// SPDX-License-Identifier: AGPL-3.0-only
using Content.Shared.CMU14.Government;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Government;

// Client side of the console of the foreman: opens the window, gives it the state, and sends what the head does.
public sealed class ForemanConsoleBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private ForemanConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<ForemanConsoleWindow>();
        _window.OnSelectZone += zone => SendMessage(new SelectZoneMessage(zone));
        _window.OnSelectCity += city => SendMessage(new SelectCityMessage(city));
        _window.OnOrder += (good, lots) => SendMessage(new OrderWorldGoodMessage(good, lots));
        _window.OnCancelOrder += order => SendMessage(new CancelWorldOrderMessage(order));
        _window.OnCall += () => SendMessage(new CallShuttleMessage());
        _window.OnDepart += () => SendMessage(new DepartShuttleMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window != null && state is ForemanState foreman)
            _window.SetState(foreman);
    }
}
