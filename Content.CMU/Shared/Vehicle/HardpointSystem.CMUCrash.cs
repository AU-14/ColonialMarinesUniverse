using Content.Shared.Containers.ItemSlots;

namespace Content.Shared._RMC14.Vehicle;

public sealed partial class HardpointSystem
{
    /// <summary>
    /// Wears only a vehicle's wheels by a crash's hull damage, split between them, leaving its guns and
    /// other hardpoints alone. With no wheels left to wear it does nothing.
    /// </summary>
    public float CMUIncomingDamageMultiplier(EntityUid? origin, EntityUid? tool)
    {
        return GetVehicleIncomingDamageMultiplier(origin, tool);
    }

    public bool CMUDamageHullWheels(EntityUid vehicle, float amount)
    {
        if (_net.IsClient || amount <= 0f)
            return false;

        if (!TryComp(vehicle, out HardpointSlotsComponent? hardpoints) ||
            !TryComp(vehicle, out ItemSlotsComponent? itemSlots))
        {
            return DamageVehicleHull(vehicle, amount);
        }

        var targets = new List<EntityUid>();
        CollectHullDamageTargets(vehicle, hardpoints, itemSlots, targets, includeWheels: true);
        targets.RemoveAll(target => !HasComp<VehicleWheelItemComponent>(target));
        if (targets.Count == 0)
            return false;

        var changed = false;
        foreach (var target in targets)
        {
            if (DamageHardpoint(vehicle, target, amount / targets.Count))
                changed = true;
        }

        if (changed)
        {
            RefreshVehicleFrameIntegrityFromHardpoints(vehicle, hardpoints, itemSlots);
            TryTriggerVehicleStructuralFailure(vehicle, amount);
        }

        return changed;
    }
}
