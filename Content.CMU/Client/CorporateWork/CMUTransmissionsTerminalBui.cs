using Content.Shared.CMU14.CorporateWork;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.CorporateWork;

public sealed class CMUTransmissionsTerminalBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CMUTransmissionsTerminalWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMUTransmissionsTerminalWindow>();
        _window.OnStart += tier => SendMessage(new CMUTransmissionsStartBuiMsg(tier));
        _window.OnGuess += guess => SendMessage(new CMUTransmissionsGuessBuiMsg(guess));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not CMUTransmissionsTerminalBuiState s)
            return;

        _window.UpdateState(s);
    }
}
