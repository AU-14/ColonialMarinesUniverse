using Content.Shared.Lock;

namespace Content.Shared.Containers.ItemSlots;

public sealed partial class ItemSlotsSystem
{
    [SubscribeLocalEvent]
    private void OnLockMapInit(Entity<ItemSlotsLockComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp(ent.Owner, out LockComponent? lockComp))
            return;

        UpdateLocks(ent, lockComp.Locked);
    }

    [SubscribeLocalEvent]
    private void OnLockToggled(Entity<ItemSlotsLockComponent> ent, ref LockToggledEvent args)
    {
        UpdateLocks(ent, args.Locked);
    }

    private void UpdateLocks(Entity<ItemSlotsLockComponent> ent, bool locked)
    {
        foreach (var slot in ent.Comp.Slots)
        {
            if (!TryGetSlot(ent.Owner, slot, out var itemSlot))
                continue;

            SetLock(ent.Owner, itemSlot, locked);
        }
    }

    /// <summary>
    /// Sets whether an item slot is locked, preventing checked insertion and ejection while locked.
    /// </summary>
    public void SetLock(Entity<ItemSlotsComponent?> ent, string id, bool locked)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        if (!ent.Comp.Slots.TryGetValue(id, out var slot))
            return;

        SetLock(ent, slot, locked);
    }

    /// <summary>
    /// Sets whether an item slot is locked, preventing checked insertion and ejection while locked.
    /// </summary>
    public void SetLock(Entity<ItemSlotsComponent?> ent, ItemSlot slot, bool locked)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        slot.Locked = locked;
        Dirty(ent);
    }

    /// <summary>
    /// Toggles whether a slot contributes a context-menu eject verb and may be ejected through BUI.
    /// </summary>
    public void SetDisableEject(EntityUid uid, string id, bool disabled, ItemSlotsComponent? itemSlots = null)
    {
        if (!Resolve(uid, ref itemSlots) ||
            !itemSlots.Slots.TryGetValue(id, out var slot))
        {
            return;
        }

        SetDisableEject(uid, slot, disabled, itemSlots);
    }

    /// <summary>
    /// Toggles whether a slot contributes a context-menu eject verb and may be ejected through BUI.
    /// </summary>
    public void SetDisableEject(EntityUid uid, ItemSlot slot, bool disabled, ItemSlotsComponent? itemSlots = null)
    {
        if (!Resolve(uid, ref itemSlots))
            return;

        slot.DisableEject = disabled;
        Dirty(uid, itemSlots);
    }

    /// <summary>
    /// Toggles whether normal interaction attempts to insert a held item into a slot.
    /// </summary>
    public void SetInsertOnInteract(EntityUid uid, string id, bool insertOnInteract, ItemSlotsComponent? itemSlots = null)
    {
        if (!Resolve(uid, ref itemSlots) ||
            !itemSlots.Slots.TryGetValue(id, out var slot))
        {
            return;
        }

        SetInsertOnInteract(uid, slot, insertOnInteract, itemSlots);
    }

    /// <summary>
    /// Toggles whether normal interaction attempts to insert a held item into a slot.
    /// </summary>
    public void SetInsertOnInteract(EntityUid uid, ItemSlot slot, bool insertOnInteract, ItemSlotsComponent? itemSlots = null)
    {
        if (!Resolve(uid, ref itemSlots))
            return;

        slot.InsertOnInteract = insertOnInteract;
        Dirty(uid, itemSlots);
    }
}
