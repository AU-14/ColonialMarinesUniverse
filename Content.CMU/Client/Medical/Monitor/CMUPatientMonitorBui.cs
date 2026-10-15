using Content.Shared.CMU14.Medical.Monitor;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Medical.Monitor;

public sealed class CMUPatientMonitorBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CMUPatientMonitorWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMUPatientMonitorWindow>();
        _window.SetMonitor(Owner);

        _window.OnEnergy += raise => SendMessage(new CMUPatientMonitorEnergyMsg(raise));
        _window.OnNibp += () => SendMessage(new CMUPatientMonitorNibpMsg());
        _window.OnSilence += () => SendMessage(new CMUPatientMonitorSilenceMsg());
        _window.OnDisconnect += () => SendMessage(new CMUPatientMonitorDisconnectMsg());
        _window.OnAnalyze += () => SendMessage(new CMUPatientMonitorAnalyzeMsg());
        _window.OnCharge += () => SendMessage(new CMUPatientMonitorChargeMsg());
        _window.OnShock += () => SendMessage(new CMUPatientMonitorShockMsg());

        if (State is CMUPatientMonitorBuiState state)
            _window.UpdateState(state);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window != null && state is CMUPatientMonitorBuiState s)
            _window.UpdateState(s);
    }
}
