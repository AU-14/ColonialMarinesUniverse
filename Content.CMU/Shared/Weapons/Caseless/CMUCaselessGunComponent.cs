namespace Content.Shared.CMU14.Weapons.Caseless;

/// <summary>
///     Every cartridge this gun fires is caseless: nothing is ejected and the spent round is deleted, whatever the
///     ammo would do in another gun.
/// </summary>
[RegisterComponent]
public sealed partial class CMUCaselessGunComponent : Component;
