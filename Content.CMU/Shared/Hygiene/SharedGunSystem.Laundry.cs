// ReSharper disable CheckNamespace
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    public void RestoreLaundryAmmo(Entity<BallisticAmmoProviderComponent> magazine, EntityUid ammo)
    {
        if (!Containers.Insert(ammo, magazine.Comp.Container))
            return;

        // TakeAmmo removes from the end; returned rounds must wait behind unexamined ammunition.
        magazine.Comp.Entities.Insert(0, ammo);
        DirtyField(magazine.AsNullable(), nameof(BallisticAmmoProviderComponent.Entities));
        UpdateBallisticAppearance(magazine);
        UpdateAmmoCount(magazine);
    }
}
