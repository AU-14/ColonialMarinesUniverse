using Content.Shared.CMU14.Weapons.AmmoCounter;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    /// <summary>
    ///     Whether you can tell how many rounds are left by looking at this. A loose magazine always shows it; a gun only
    ///     when it has an ammo counter, otherwise you have to take the magazine out.
    /// </summary>
    public bool CMUShowsAmmoCount(EntityUid uid)
    {
        return !HasComp<GunComponent>(uid) || HasComp<CMUAmmoCounterComponent>(uid);
    }
}
