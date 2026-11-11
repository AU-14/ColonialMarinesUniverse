using Content.Shared.Containers.ItemSlots;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Vehicle;

public sealed partial class HardpointSystem
{
    private static readonly ProtoId<TagPrototype> MaintenancePlowTag = "VehiclePlow";
    [Dependency] private TagSystem _maintenanceTags = default!;

    private bool IsFaultImmuneHardpoint(EntityUid hardpoint) =>
        HasComp<VehiclePlowComponent>(hardpoint) || _maintenanceTags.HasTag(hardpoint, MaintenancePlowTag);

    private static float GetFactoryMaxIntegrity(HardpointIntegrityComponent integrity) =>
        integrity.NativeMaxIntegrity > 0f ? integrity.NativeMaxIntegrity : integrity.MaxIntegrity + integrity.RepairWear;

    // A mounted hull derives its capacity from its current loadout. Sum the unworn
    // capacities of that same loadout so repairing a module cannot shrink the display scale.
    private float GetFactoryMaxIntegrity(EntityUid uid, HardpointIntegrityComponent integrity)
    {
        if (!IsVehicleFrame(uid) ||
            !TryComp(uid, out HardpointSlotsComponent? slots) ||
            !TryComp(uid, out ItemSlotsComponent? itemSlots))
            return GetFactoryMaxIntegrity(integrity);

        var total = 0f;
        var visited = new HashSet<EntityUid>();
        foreach (var mounted in _topology.GetMountedSlots(uid, slots, itemSlots))
        {
            if (mounted.Item is { } item && visited.Add(item) &&
                TryComp(item, out HardpointIntegrityComponent? part))
                total += GetFactoryMaxIntegrity(part);
        }

        return total > 0f ? total : GetFactoryMaxIntegrity(integrity);
    }
}
