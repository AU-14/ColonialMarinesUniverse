using Content.Shared._RMC14.Visor;
using Content.Shared._RMC14.Weapons.Ranged.SmartGun;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;

namespace Content.Shared.CMU14.Visor;

/// <summary>
/// Squad optics don't work alongside a smartgun: they can't be switched down while the wearer holds or wears one,
/// and switch back up when the wearer picks one up.
/// </summary>
public sealed partial class CMUSquadVisorSystem
{
    private void InitializeSmartgun()
    {
        SubscribeLocalEvent<CMUSquadOpticComponent, ActivateVisorAttemptEvent>(OnOpticActivateAttempt);
        SubscribeLocalEvent<CMUSquadVisorWearerComponent, DidEquipEvent>(OnWearerEquipped);
    }

    private void OnOpticActivateAttempt(Entity<CMUSquadOpticComponent> optic, ref ActivateVisorAttemptEvent args)
    {
        if (args.Cancelled || !HasSmartgun(args.User))
            return;

        args.Cancel();
        _popup.PopupEntity(Loc.GetString("cmu-squad-visor-smartgun-blocked", ("optic", optic)),
            args.User, args.User, PopupType.SmallCaution);
    }

    private void OnWearerEquipped(Entity<CMUSquadVisorWearerComponent> ent, ref DidEquipEvent args)
    {
        SwitchUpIfSmartgun(ent);
    }

    private void SwitchUpIfSmartgun(EntityUid wearer)
    {
        if (_timing.ApplyingState || !HasSmartgun(wearer))
            return;

        if (!_inventory.TryGetSlotEntity(wearer, "head", out var helmet) ||
            !HasComp<CMUSquadVisorActiveComponent>(helmet) ||
            !TryComp<CycleableVisorComponent>(helmet, out var cycleable))
        {
            return;
        }

        _visor.CMUSwitchVisorUp((helmet.Value, cycleable), wearer);
        _popup.PopupEntity(Loc.GetString("cmu-squad-visor-smartgun-switched-up"), wearer, wearer, PopupType.SmallCaution);
    }

    private bool HasSmartgun(EntityUid user)
    {
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (HasComp<SmartGunComponent>(held))
                return true;
        }

        if (_inventory.TryGetContainerSlotEnumerator(user, out var slots))
        {
            while (slots.NextItem(out var item))
            {
                if (HasComp<SmartGunComponent>(item))
                    return true;
            }
        }

        return false;
    }
}
