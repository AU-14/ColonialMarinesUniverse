using Content.Shared.CMU14.CorporateWork;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.CorporateWork;

public sealed class CMUNegotiationTerminalBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CMUNegotiationTerminalWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMUNegotiationTerminalWindow>();
        _window.OnStart += tier => SendMessage(new CMUNegotiationStartBuiMsg(tier));
        _window.OnOffer += offer => SendMessage(new CMUNegotiationOfferBuiMsg(offer));
        _window.OnAccept += () => SendMessage(new CMUNegotiationAcceptBuiMsg());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not CMUNegotiationTerminalBuiState s)
            return;

        _window.UpdateState(s);
    }
}
