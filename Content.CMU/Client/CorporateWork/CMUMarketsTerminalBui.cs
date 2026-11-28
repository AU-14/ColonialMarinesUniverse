using Content.Shared.CMU14.CorporateWork;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.CorporateWork;

public sealed class CMUMarketsTerminalBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CMUMarketsTerminalWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMUMarketsTerminalWindow>();
        _window.OnTrade += (commodity, units, buy) => SendMessage(new CMUMarketsTradeBuiMsg(commodity, units, buy));
    }

    // Each viewer gets their own view of the market, so it comes as a message rather than state.
    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (_window != null && message is CMUMarketsTerminalBuiState state)
            _window.UpdateState(state);
    }
}
