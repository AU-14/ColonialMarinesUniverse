using System.Collections.Generic;
using Content.Shared.Containers.ItemSlots;

namespace Content.Shared._RMC14.Vehicle;

public sealed partial class HardpointSystem
{
    private void PopulateHardpointUiFailures(
        List<HardpointUiEntry> entries,
        EntityUid uid,
        HardpointSlotsComponent component,
        ItemSlotsComponent itemSlots)
    {
        if (entries.Count == 0)
            return;

        var bySlot = new Dictionary<string, HardpointUiEntry>(entries.Count);
        foreach (var entry in entries)
        {
            bySlot[entry.SlotId] = entry;
        }

        foreach (var slot in component.Slots)
        {
            if (string.IsNullOrWhiteSpace(slot.Id))
                continue;

            if (!_itemSlots.TryGetSlot(uid, slot.Id, out var itemSlot, itemSlots) ||
                itemSlot.Item is not { } item)
            {
                continue;
            }

            if (bySlot.TryGetValue(slot.Id, out var entry))
                entry.Failures.AddRange(GetFailureStatuses(item, includeRepairStep: false));

            if (!TryComp(item, out HardpointSlotsComponent? turretSlots) ||
                !TryComp(item, out ItemSlotsComponent? turretItemSlots))
            {
                continue;
            }

            foreach (var turretSlot in turretSlots.Slots)
            {
                if (string.IsNullOrWhiteSpace(turretSlot.Id))
                    continue;

                if (!_itemSlots.TryGetSlot(item, turretSlot.Id, out var turretItemSlot, turretItemSlots) ||
                    turretItemSlot.Item is not { } installed)
                {
                    continue;
                }

                var compositeId = VehicleTurretSlotIds.Compose(slot.Id, turretSlot.Id);
                if (bySlot.TryGetValue(compositeId, out var turretEntry))
                    turretEntry.Failures.AddRange(GetFailureStatuses(installed, includeRepairStep: false));
            }
        }
    }
}
