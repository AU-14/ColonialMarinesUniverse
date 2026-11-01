using Content.Shared.Containers.ItemSlots;

namespace Content.Shared._RMC14.Vehicle;

public sealed partial class HardpointSystem
{
    /// <summary>Like <see cref="DamageVehicleHull"/>, but passes what a nearly broken part cannot take to the others. Returns the integrity removed.</summary>
    public float CMUDamageVehicleHullExact(EntityUid vehicle, float amount)
    {
        if (_net.IsClient || amount <= 0f)
            return 0f;

        if (!TryComp(vehicle, out HardpointSlotsComponent? hardpoints) ||
            !TryComp(vehicle, out ItemSlotsComponent? itemSlots))
        {
            return DamageFrameDirectly(vehicle, amount);
        }

        var targets = new List<EntityUid>();
        CollectHullDamageTargets(vehicle, hardpoints, itemSlots, targets, includeWheels: false);
        if (targets.Count == 0)
            CollectHullDamageTargets(vehicle, hardpoints, itemSlots, targets, includeWheels: true);

        if (targets.Count == 0)
            return DamageFrameDirectly(vehicle, amount);

        var parts = new List<(EntityUid Part, float Integrity)>(targets.Count);
        foreach (var target in targets)
        {
            parts.Add((target, Comp<HardpointIntegrityComponent>(target).Integrity));
        }

        parts.Sort((a, b) => a.Integrity.CompareTo(b.Integrity));

        var remaining = amount;
        var dealt = 0f;
        for (var i = 0; i < parts.Count; i++)
        {
            var damage = MathF.Min(remaining / (parts.Count - i), parts[i].Integrity);
            remaining -= damage;
            if (DamageHardpoint(vehicle, parts[i].Part, damage))
                dealt += damage;
        }

        if (dealt > 0f)
        {
            RefreshVehicleFrameIntegrityFromHardpoints(vehicle, hardpoints, itemSlots);
            TryTriggerVehicleStructuralFailure(vehicle, amount);
        }

        return dealt;
    }

    private float DamageFrameDirectly(EntityUid vehicle, float amount)
    {
        var before = TryComp(vehicle, out HardpointIntegrityComponent? frame) ? frame.Integrity : 0f;
        if (!DamageHardpoint(vehicle, vehicle, amount))
            return 0f;

        return before - (frame?.Integrity ?? before);
    }
}
