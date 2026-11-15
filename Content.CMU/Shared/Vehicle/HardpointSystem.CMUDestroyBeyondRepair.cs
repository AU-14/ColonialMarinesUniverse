namespace Content.Shared._RMC14.Vehicle;

public sealed partial class HardpointSystem
{
    /// <summary>Wrecks the frame for good: hull at zero, surviving parts do not bring it back.</summary>
    public void CMUDestroyBeyondRepair(EntityUid vehicle)
    {
        if (_net.IsClient || !TryComp(vehicle, out HardpointIntegrityComponent? frame) || frame.DestroyedBeyondRepair)
            return;

        frame.DestroyedBeyondRepair = true;
        frame.Integrity = 0f;
        Dirty(vehicle, frame);
        UpdateFrameDamageAppearance(vehicle, frame);
        RaiseIntegrityChanged(vehicle);
        RaiseFrameIntegrityChanged(vehicle, false);
        RefreshCanRun(vehicle);
        _lock.RefreshForcedOpen(vehicle);
        UpdateHardpointUi(vehicle);
    }
}
