using Content.Shared.CMU14.Vehicle.Jeep;

namespace Content.Shared._RMC14.Vehicle;

public sealed partial class VehicleWeaponsSystem
{
    /// <summary>
    /// CMU14: the vehicle a weapons seat belongs to, from its interior or, for open vehicles, the seat
    /// strapped onto the vehicle itself.
    /// </summary>
    private bool CMUTryGetSeatVehicle(EntityUid seat, out EntityUid? vehicle)
    {
        if (_vehicleSystem.TryGetVehicleFromInterior(seat, out vehicle) && vehicle != null)
            return true;

        vehicle = CompOrNull<CMUVehicleSeatComponent>(seat)?.Vehicle;
        return vehicle != null;
    }
}
