using Content.Shared.AU14.ColonyEconomy;
using Robust.Client.UserInterface;

namespace Content.Client.AU14.ColonyEconomy;

public sealed class CorporateConsoleThirdPartyBui : BoundUserInterface
{
    private CorporateConsoleThirdPartyWindow? _window;

    public CorporateConsoleThirdPartyBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CorporateConsoleThirdPartyWindow>();
        _window.OnCallThirdParty += id => SendPredictedMessage(new CorporateConsoleCallThirdPartyBuiMsg(id));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not CorporateConsoleThirdPartyBuiState s)
            return;
        _window.UpdateState(s);
    }
}

