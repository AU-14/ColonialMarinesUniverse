using Content.Shared.CMU14.CorporateWork;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.CorporateWork;

public sealed class CMUAuditTerminalBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CMUAuditTerminalWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMUAuditTerminalWindow>();
        _window.OnStart += tier => SendMessage(new CMUAuditStartBuiMsg(tier));
        _window.OnSubmit += flagged => SendMessage(new CMUAuditSubmitBuiMsg(flagged));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not CMUAuditTerminalBuiState s)
            return;

        _window.UpdateState(s);
    }
}
