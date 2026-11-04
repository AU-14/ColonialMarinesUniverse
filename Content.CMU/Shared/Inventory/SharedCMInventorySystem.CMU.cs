using Content.Shared.Inventory;
using Content.Shared.Light.Components;
using Content.Shared.Storage;
using Content.Shared.Weapons.Melee;

// ReSharper disable CheckNamespace
namespace Content.Shared._RMC14.Inventory;

public abstract partial class SharedCMInventorySystem
{
    // flashlights carry a 0 damage MeleeWeapon so they can be swung, which made the holster keys grab them off the belt
    private void OnMeleeIsUnholsterable(Entity<MeleeWeaponComponent> ent, ref IsUnholsterableEvent args)
    {
        if (HasComp<HandheldLightComponent>(ent) || !ent.Comp.Damage.AnyPositive())
            return;

        args.Unholsterable = true;
    }

    // secondary used to start its search at the belt, so with nothing in suit storage it drew the exact
    // same pistol primary would. now it skips whatever primary would draw from and takes the next one, like cmss13
    public void OnHolsterSecondary(EntityUid user)
    {
        if (_hands.TryGetActiveItem(user, out var active))
        {
            Holster(user, active.Value);
            return;
        }

        var primary = GetPrimaryUnholsterSource(user);
        foreach (var flag in _order)
        {
            var slots = _inventory.GetSlotEnumerator(user, flag);
            while (slots.NextItem(out var item))
            {
                if (item == primary)
                    continue;

                if (Unholster(user, item, out var stop) || stop)
                    return;
            }
        }

        // only one thing to draw, so secondary still draws it
        Unholster(user, 0, CMHolsterChoose.First);
    }

    // dry run of the primary bind, gives the worn item it would draw from (or stop on)
    private EntityUid? GetPrimaryUnholsterSource(EntityUid user)
    {
        foreach (var flag in _order)
        {
            var slots = _inventory.GetSlotEnumerator(user, flag);
            while (slots.NextItem(out var item))
            {
                if (WouldUnholsterFrom(user, item))
                    return item;
            }
        }

        return null;
    }

    private bool WouldUnholsterFrom(EntityUid user, EntityUid item)
    {
        if (TryComp(item, out CMHolsterComponent? holster))
        {
            if (holster.Cooldown is { } cooldown && _timing.CurTime < holster.LastEjectAt + cooldown)
                return true;

            if (HasComp<StorageComponent>(item) && TryGetLastInserted((item, holster), out _))
                return true;

            if (SlotCanInteract(user, item, out var itemSlots))
            {
                foreach (var slot in itemSlots.Slots.Values)
                {
                    if (slot.ContainerSlot?.ContainedEntity is { } contained &&
                        !_whitelist.IsWhitelistFail(holster.Whitelist, contained))
                    {
                        return true;
                    }
                }
            }
        }

        var ev = new IsUnholsterableEvent();
        RaiseLocalEvent(item, ref ev);
        return ev.Unholsterable;
    }
}
