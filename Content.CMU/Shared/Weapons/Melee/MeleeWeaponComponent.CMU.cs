// ReSharper disable once CheckNamespace
namespace Content.Shared.Weapons.Melee;

public sealed partial class MeleeWeaponComponent
{
    // right click with this held does a disarm/shove instead of a wide swing, for weapons that
    // run their own shove-intent moves (chain gauntlet throw finisher). AltDisarm alone only
    // covers unarmed attacks
    [DataField]
    public bool HeldAltDisarm;
}
