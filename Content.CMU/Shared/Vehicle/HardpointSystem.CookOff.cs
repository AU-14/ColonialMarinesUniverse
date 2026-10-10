using Content.Shared.Popups;

namespace Content.Shared._RMC14.Vehicle;

public sealed partial class HardpointSystem
{
    public bool IsCookedOff(EntityUid target) => HasOnVehicle<ActiveTankCookOffComponent>(target);

    // kept apart from IsDestroyedBeyondRepair on purpose, that one zeroes the hull and the damage sprite follows it
    public bool IsTotaled(EntityUid target) => HasOnVehicle<VehicleTotaledComponent>(target);

    public bool IsDestroyedBeyondRepair(EntityUid target)
    {
        return IsCookedOff(target)
            || (TryComp<HardpointIntegrityComponent>(target, out var integrity) && integrity.DestroyedBeyondRepair)
            || (_topology.TryGetVehicle(target, out var vehicle)
                && TryComp<HardpointIntegrityComponent>(vehicle, out var frame) && frame.DestroyedBeyondRepair);
    }

    // cook-off, dead frame or OB totaled, for the repair/install/reset gates
    public bool IsWrecked(EntityUid target) => IsDestroyedBeyondRepair(target) || IsTotaled(target);

    public string GetWreckMessage(EntityUid target)
    {
        if (IsCookedOff(target))
            return Loc.GetString("cmu-tank-cook-off-unrepairable");

        return Loc.GetString(IsTotaled(target) ? "cmu-vehicle-totaled-unrepairable" : "cmu-vehicle-wreck-unrepairable");
    }

    private bool HasOnVehicle<T>(EntityUid target) where T : IComponent
    {
        return HasComp<T>(target) || (_topology.TryGetVehicle(target, out var vehicle) && HasComp<T>(vehicle));
    }

    private bool CanRepairCookOff(EntityUid target, EntityUid user)
    {
        if (!IsWrecked(target))
            return true;

        _popup.PopupEntity(GetWreckMessage(target), target, user, PopupType.SmallCaution);
        return false;
    }
}
