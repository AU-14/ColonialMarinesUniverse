using Content.Shared.AU14.Ambassador;
using Robust.Client.UserInterface;

namespace Content.Client.AU14.Ambassador;

public sealed class AmbassadorThirdPartyBui : BoundUserInterface
{
    private AmbassadorThirdPartyWindow? _window;

    public AmbassadorThirdPartyBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<AmbassadorThirdPartyWindow>();
        _window.OnCallThirdParty += id => SendPredictedMessage(new AmbassadorCallThirdPartyBuiMsg(id));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not AmbassadorThirdPartyBuiState s)
            return;

        _window.UpdateState(s);
    }
}

