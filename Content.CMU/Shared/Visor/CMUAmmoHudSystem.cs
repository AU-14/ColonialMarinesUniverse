using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Shared.CMU14.Visor;

public sealed partial class CMUAmmoHudSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;

    public bool TryGetWeapon(EntityUid wearer, out EntityUid weapon)
    {
        weapon = default;
        if (!(TryComp<CMUSquadVisorWearerComponent>(wearer, out var visor) && visor.AmmoHudEnabled) &&
            !(_inventory.TryGetSlotEntity(wearer, "eyes", out var eyes) && HasComp<CMUAmmoHudSightComponent>(eyes)))
        {
            return false;
        }

        // Check both hands so a wielded gun still wins when the active hand holds its virtual item.
        foreach (var held in _hands.EnumerateHeld(wearer))
        {
            if (!IsEligible(held))
                continue;

            weapon = held;
            return true;
        }

        if (!_inventory.TryGetSlotEntity(wearer, "suitstorage", out var stored) || !IsEligible(stored.Value))
            return false;

        weapon = stored.Value;
        return true;
    }

    private bool IsEligible(EntityUid weapon)
    {
        return HasComp<GunComponent>(weapon) &&
               TryComp<CMUAmmoHudWeaponComponent>(weapon, out var hud) && hud.Enabled;
    }
}
