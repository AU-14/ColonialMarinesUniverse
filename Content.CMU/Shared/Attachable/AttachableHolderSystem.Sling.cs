using Content.Shared._RMC14.Attachable.Components;
using Content.Shared.CMU14.Attachable;

namespace Content.Shared._RMC14.Attachable.Systems;

public sealed partial class AttachableHolderSystem
{
    private const string CMUSlingRailSlot = "rmc-aslot-rail";
    private const string CMUSlingUnderbarrelSlot = "rmc-aslot-underbarrel";

    private void EnsureCMUSlingSlot(Entity<AttachableHolderComponent> holder)
    {
        if (holder.Comp.Slots.ContainsKey(CMUSlingUnderbarrelSlot) ||
            !holder.Comp.Slots.TryGetValue(CMUSlingRailSlot, out var rail) ||
            rail.Whitelist?.Tags is not { } tags ||
            (!tags.Contains("RMCAttachmentMagneticHarness") && !tags.Contains("RMCAttachmentRail")))
            return;

        holder.Comp.Slots.Add(CMUSlingUnderbarrelSlot, new AttachableSlot());
        Dirty(holder);
    }

    private bool IsCMUSlotCompatible(Entity<AttachableHolderComponent> holder, string slotId, EntityUid attachable)
    {
        if (!holder.Comp.Slots.TryGetValue(slotId, out var slot))
            return false;

        if (!TryComp<CMUSlingComponent>(attachable, out var sling))
            return _whitelist.IsWhitelistPass(slot.Whitelist, attachable);

        if (!sling.Underbarrel)
            return slotId == CMUSlingRailSlot && _whitelist.IsWhitelistPass(slot.Whitelist, attachable);

        // Reuse rail compatibility so underbarrel mode never broadens which weapons accept a sling.
        return slotId == CMUSlingUnderbarrelSlot &&
               !slot.Locked &&
               holder.Comp.Slots.TryGetValue(CMUSlingRailSlot, out var rail) &&
               !rail.Locked &&
               _whitelist.IsWhitelistPass(rail.Whitelist, attachable);
    }
}
