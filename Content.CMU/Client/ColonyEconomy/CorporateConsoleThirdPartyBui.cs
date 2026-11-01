using Content.Client.CMU14.Interface;
using Content.Shared.CMU14.ColonyEconomy;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.ColonyEconomy;

public sealed class CorporateConsoleThirdPartyBui : BoundUserInterface
{
    private CrtThirdPartyWindow? _window;

    public CorporateConsoleThirdPartyBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CrtThirdPartyWindow>();
        _window.OnCallThirdParty += id => SendPredictedMessage(new CorporateConsoleCallThirdPartyBuiMsg(id));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not CorporateConsoleThirdPartyBuiState s)
            return;

        _window.UpdateState(s.Budget, s.CallableParties, s.CalledParties, s.CallsLeft, s.NextCallInAt);
    }
}
