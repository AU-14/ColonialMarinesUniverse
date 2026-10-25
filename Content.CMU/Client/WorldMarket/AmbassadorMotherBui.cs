// SPDX-License-Identifier: AGPL-3.0-only
using Content.Shared.CMU14.WorldMarket;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.WorldMarket;

// Client side of the window of the exchange with the mother faction, which the console of the ambassador opens: opens the window, gives it the state and sends the orders and the subsidies.
public sealed class AmbassadorMotherBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private MotherExchangeWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<MotherExchangeWindow>();
        _window.OnOrder += (offer, target) => SendMessage(new MotherOrderMessage(offer, target));
        _window.OnSubsidy += points => SendMessage(new MotherSubsidyMessage(points));
        _window.OnReport += () => SendMessage(new MotherReportMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window != null && state is AmbassadorMotherState mother)
            _window.SetState(mother);
    }
}
