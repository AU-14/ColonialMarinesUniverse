using Content.Shared.CMU14.Vehicle;

namespace Content.Shared.Vehicle;

public sealed partial class GridVehicleMoverSystem
{
    [Dependency] private CMUVehicleTyreSystem _cmuTyres = default!;

    private float CMUTyreSpeed(EntityUid vehicle)
    {
        return _cmuTyres.SpeedMultiplier(vehicle);
    }

    private float CMUTyreWaterDrag(EntityUid vehicle)
    {
        return _cmuTyres.WaterDrag(vehicle);
    }
}
