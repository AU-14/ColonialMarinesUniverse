using Content.Shared.CMU14.CorporateWork;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.CorporateWork;

public sealed class CMULogisticsTerminalBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CMULogisticsTerminalWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMULogisticsTerminalWindow>();
        _window.OnStart += tier => SendMessage(new CMULogisticsStartBuiMsg(tier));
        _window.OnSubmit += loaded => SendMessage(new CMULogisticsSubmitBuiMsg(loaded));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not CMULogisticsTerminalBuiState s)
            return;

        _window.UpdateState(s);
    }
}
