using Content.Shared.Popups;

namespace Content.Shared._RMC14.Vehicle;

public sealed partial class HardpointSystem
{
    public bool IsCookedOff(EntityUid target)
    {
        return HasComp<ActiveTankCookOffComponent>(target)
            || (_topology.TryGetVehicle(target, out var vehicle) && HasComp<ActiveTankCookOffComponent>(vehicle));
    }

    public bool IsDestroyedBeyondRepair(EntityUid target)
    {
        return IsCookedOff(target)
            || (TryComp<HardpointIntegrityComponent>(target, out var integrity) && integrity.DestroyedBeyondRepair)
            || (_topology.TryGetVehicle(target, out var vehicle)
                && TryComp<HardpointIntegrityComponent>(vehicle, out var frame) && frame.DestroyedBeyondRepair);
    }

    public string GetWreckMessage(EntityUid target)
    {
        return Loc.GetString(IsCookedOff(target) ? "cmu-tank-cook-off-unrepairable" : "cmu-vehicle-wreck-unrepairable");
    }

    private bool CanRepairCookOff(EntityUid target, EntityUid user)
    {
        if (!IsDestroyedBeyondRepair(target))
            return true;

        _popup.PopupClient(GetWreckMessage(target), target, user, PopupType.SmallCaution);
        return false;
    }
}
