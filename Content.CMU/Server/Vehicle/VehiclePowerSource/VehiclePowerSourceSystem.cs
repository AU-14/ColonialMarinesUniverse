using Content.Shared.CMU14.Vehicle;

namespace Content.Server.CMU14.Vehicle;

public sealed partial class VehiclePowerSourceServerSystem : EntitySystem
{
    [Dependency] private VehiclePowerSourceSharedSystem _vehiclePowerSourceShared = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var our_vehicle = EntityQueryEnumerator<VehiclePowerSourceTankComponent>();
        while (our_vehicle.MoveNext(out var uid, out var motion))
        {
            _vehiclePowerSourceShared.ProcessFuel((uid, motion), frameTime);
        }
    }

}
