using Content.Shared.Popups;

namespace Content.Shared._RMC14.Vehicle;

public sealed partial class HardpointSystem
{
    public bool IsCookedOff(EntityUid target)
    {
        return HasComp<ActiveTankCookOffComponent>(target)
            || (_topology.TryGetVehicle(target, out var vehicle) && HasComp<ActiveTankCookOffComponent>(vehicle));
    }

    private bool CanRepairCookOff(EntityUid target, EntityUid user)
    {
        if (!IsCookedOff(target))
            return true;

        _popup.PopupClient(Loc.GetString("cmu-tank-cook-off-unrepairable"), target, user, PopupType.SmallCaution);
        return false;
    }
}
