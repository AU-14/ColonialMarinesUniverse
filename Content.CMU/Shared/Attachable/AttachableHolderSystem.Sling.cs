using Content.Shared._RMC14.Attachable.Components;
using Content.Shared.CMU14.Attachable;
using Content.Shared.Whitelist;

namespace Content.Shared._RMC14.Attachable.Systems;

public sealed partial class AttachableHolderSystem
{
    private const string CMUSlingRailSlot = "rmc-aslot-rail";
    private const string CMUSlingUnderbarrelSlot = "rmc-aslot-underbarrel";
    public const string CMUSlingSlot = "cmu-aslot-sling";

    private void EnsureCMUSlingSlot(Entity<AttachableHolderComponent> holder)
    {
        if (!holder.Comp.Slots.TryGetValue(CMUSlingRailSlot, out var rail) ||
            rail.Whitelist?.Tags is not { } tags ||
            (!tags.Contains("RMCAttachmentMagneticHarness") && !tags.Contains("RMCAttachmentRail")))
            return;

        holder.Comp.Slots.TryAdd(CMUSlingUnderbarrelSlot, new AttachableSlot());
        holder.Comp.Slots.TryAdd(CMUSlingSlot, new AttachableSlot
        {
            Whitelist = new EntityWhitelist { Components = ["CMUSling"] },
        });
        Dirty(holder);
    }

    private bool IsCMUSlotCompatible(Entity<AttachableHolderComponent> holder, string slotId, EntityUid attachable)
    {
        if (!holder.Comp.Slots.TryGetValue(slotId, out var slot))
            return false;

        if (!TryComp<CMUSlingComponent>(attachable, out var sling))
            return _whitelist.IsWhitelistPass(slot.Whitelist, attachable);

        if (sling.Mount == CMUSlingMount.Sling)
        {
            return slotId == CMUSlingSlot &&
                   !slot.Locked &&
                   holder.Comp.Slots.TryGetValue(CMUSlingRailSlot, out var railSlot) &&
                   _whitelist.IsWhitelistPass(railSlot.Whitelist, attachable);
        }

        if (sling.Mount == CMUSlingMount.Rail)
            return slotId == CMUSlingRailSlot && _whitelist.IsWhitelistPass(slot.Whitelist, attachable);

        // Reuse rail compatibility so underbarrel mode never broadens which weapons accept a sling.
        return slotId == CMUSlingUnderbarrelSlot &&
               !slot.Locked &&
               holder.Comp.Slots.TryGetValue(CMUSlingRailSlot, out var rail) &&
               !rail.Locked &&
               _whitelist.IsWhitelistPass(rail.Whitelist, attachable);
    }
}
