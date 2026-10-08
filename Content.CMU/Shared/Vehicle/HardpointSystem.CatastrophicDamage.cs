namespace Content.Shared._RMC14.Vehicle;

public sealed partial class HardpointSystem
{
    private void TryDestroyVehicleFrame(EntityUid vehicle, float damage)
    {
        if (!IsVehicleFrame(vehicle) || !TryComp<HardpointIntegrityComponent>(vehicle, out var frame) ||
            frame.DestroyedBeyondRepair || frame.MaxIntegrity <= 0f || damage < frame.MaxIntegrity)
            return;

        // Compare the hit after hull armor, before splitting it among resistant modules.
        // A surviving turret or a replacement part cannot restore a catastrophically damaged frame.
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
