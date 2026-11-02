namespace Content.Shared.CMU14.Weapons.Jamming;

/// <summary>
///     Put on a cartridge or projectile prototype to set its wear multiplier directly, instead of matching the
///     profile's ammo keywords and tags.
/// </summary>
[RegisterComponent]
public sealed partial class CMUGunWearAmmoComponent : Component
{
    [DataField(required: true)]
    public float Multiplier = 1f;
}
