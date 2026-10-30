using Content.Client.CMU14.Interface;
using Content.Shared.CMU14.Ambassador;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Ambassador;

public sealed class AmbassadorThirdPartyBui : BoundUserInterface
{
    private CrtThirdPartyWindow? _window;

    public AmbassadorThirdPartyBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CrtThirdPartyWindow>();
        _window.OnCallThirdParty += id => SendPredictedMessage(new AmbassadorCallThirdPartyBuiMsg(id));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not AmbassadorThirdPartyBuiState s)
            return;

        _window.UpdateState(s.Budget, s.CallableParties, s.CalledParties, s.CallsLeft, s.NextCallInAt);
    }
}
