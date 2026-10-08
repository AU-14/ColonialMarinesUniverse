using Content.Shared.Popups;

namespace Content.Shared._RMC14.Vehicle;

public sealed partial class HardpointSystem
{
    public bool IsCookedOff(EntityUid target) => HasOnVehicle<ActiveTankCookOffComponent>(target);

    // kept apart from IsCookedOff on purpose, that one zeroes the hull and the damage sprite follows it
    public bool IsTotaled(EntityUid target) => HasOnVehicle<VehicleTotaledComponent>(target);

    public bool IsWrecked(EntityUid target) => IsCookedOff(target) || IsTotaled(target);

    public string GetWreckedMessage(EntityUid target)
    {
        return Loc.GetString(IsCookedOff(target) ? "cmu-tank-cook-off-unrepairable" : "cmu-vehicle-totaled-unrepairable");
    }

    private bool HasOnVehicle<T>(EntityUid target) where T : IComponent
    {
        return HasComp<T>(target) || (_topology.TryGetVehicle(target, out var vehicle) && HasComp<T>(vehicle));
    }

    private bool CanRepairCookOff(EntityUid target, EntityUid user)
    {
        if (!IsWrecked(target))
            return true;

        _popup.PopupClient(GetWreckedMessage(target), target, user, PopupType.SmallCaution);
        return false;
    }
}
