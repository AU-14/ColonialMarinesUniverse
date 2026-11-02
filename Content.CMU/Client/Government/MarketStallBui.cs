// SPDX-License-Identifier: AGPL-3.0-only
using Content.Shared.CMU14.Government;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Government;

// Client side of the market stall: opens the window, gives it the state, and sends what the owner or the buyer does.
public sealed class MarketStallBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private MarketStallWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<MarketStallWindow>();
        _window.OnSetPrice += (item, price) => SendMessage(new SetStallPriceMessage(item, price));
        _window.OnTake += item => SendMessage(new TakeStallItemMessage(item));
        _window.OnBuy += item => SendMessage(new BuyStallItemMessage(item));
        _window.OnRelease += () => SendMessage(new ReleaseStallMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window != null && state is StallState stall)
            _window.SetState(stall);
    }
}
