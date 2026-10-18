// SPDX-License-Identifier: AGPL-3.0-only
using Content.Shared.CMU14.Trade;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Trade;

// Client side of the trade platform: opens its window, gives it the state and the messages the server sends.
public sealed class TradePlatformBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private TradePlatformWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<TradePlatformWindow>();
        _window.OnSell += () => SendMessage(new TradeSellMessage());
        _window.OnBuy += offer => SendMessage(new TradeBuyMessage(offer));
        _window.OnEject += contract => SendMessage(new TradeEjectContractMessage(contract));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window != null && state is TradePlatformBuiState s)
            _window.SetState(s);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (_window != null && message is TradeBalanceMessage balance)
            _window.SetBalance(balance.Balance);
    }
}
