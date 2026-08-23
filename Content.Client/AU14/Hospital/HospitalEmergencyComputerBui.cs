using Content.Shared.AU14.Hospital;
using Robust.Client.UserInterface;

namespace Content.Client.AU14.Hospital;

public sealed class HospitalEmergencyComputerBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private HospitalEmergencyComputerWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<HospitalEmergencyComputerWindow>();

        _window.ApproveLandingButton.OnPressed += _ =>
            SendPredictedMessage(new HospitalEmergencyApproveLandingMsg());

        _window.RequestPickupButton.OnPressed += _ =>
            SendPredictedMessage(new HospitalEmergencyRequestPickupMsg());

        _window.ReleaseShuttleButton.OnPressed += _ =>
            SendPredictedMessage(new HospitalEmergencyReleaseShuttleMsg());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not HospitalEmergencyComputerBuiState hospitalState)
            return;

        _window.UpdateState(hospitalState);
    }
}
