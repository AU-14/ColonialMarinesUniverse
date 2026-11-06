using Content.Shared.CMU14.Vehicle;

namespace Content.Shared._RMC14.Vehicle;

public sealed partial class HardpointSystem
{
    private void CMUOnHardpointDamaged(EntityUid vehicle, EntityUid hardpoint, float amount)
    {
        if (amount <= 0f || TerminatingOrDeleted(vehicle))
            return;

        var ev = new CMUVehicleHardpointDamagedEvent(hardpoint, amount);
        RaiseLocalEvent(vehicle, ref ev);
    }
}
