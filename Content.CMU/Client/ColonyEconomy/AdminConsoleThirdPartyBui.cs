using Content.Shared.AU14.ColonyEconomy;
using Robust.Client.UserInterface;

namespace Content.Client.AU14.ColonyEconomy;

public sealed class AdminConsoleThirdPartyBui : BoundUserInterface
{
    private AdminConsoleThirdPartyWindow? _window;

    public AdminConsoleThirdPartyBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<AdminConsoleThirdPartyWindow>();
        _window.OnCallThirdParty += id => SendPredictedMessage(new AdminConsoleCallThirdPartyBuiMsg(id));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not AdminConsoleThirdPartyBuiState s)
            return;
        _window.UpdateState(s);
    }
}

