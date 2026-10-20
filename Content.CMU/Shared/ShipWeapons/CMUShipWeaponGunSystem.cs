using Content.Shared.Containers.ItemSlots;
using Content.Shared.Popups;

namespace Content.Shared.CMU14.ShipWeapons;

public sealed partial class CMUShipWeaponGunSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUShipWeaponGunComponent, ItemSlotInsertAttemptEvent>(OnAmmoInsertAttempt);
    }

    private void OnAmmoInsertAttempt(Entity<CMUShipWeaponGunComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Slot.ID != ent.Comp.AmmoSlot)
            return;

        if (Transform(ent).Anchored &&
            TryComp(args.Item, out CMUShipWeaponAmmoComponent? ammo) &&
            ent.Comp.Weapons.Contains(ammo.Weapon))
            return;

        args.Cancelled = true;
        if (args.User is { } user)
            _popup.PopupEntity(Loc.GetString("cmu-ship-weapons-wrong-round"), ent, user, PopupType.LargeCaution);
    }
}
