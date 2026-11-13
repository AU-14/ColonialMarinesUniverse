// ReSharper disable once CheckNamespace
namespace Content.Shared.Weapons.Melee;

/// <summary>
/// Raised on the wielder when a weapon that forbids wide swings is asked for one. Set
/// <see cref="Allowed"/> to let this wielder swing it wide anyway.
/// </summary>
[ByRefEvent]
public record struct CMUWideAttackOverrideEvent(EntityUid Weapon, bool Allowed = false);

public abstract partial class SharedMeleeWeaponSystem
{
    // the one answer for both the client's input path and the shared attack path
    public bool CMUWideAttackAllowed(EntityUid user, EntityUid weaponUid, MeleeWeaponComponent weapon)
    {
        if (weapon.WideAttackAllowed)
            return true;

        var ev = new CMUWideAttackOverrideEvent(weaponUid);
        RaiseLocalEvent(user, ref ev);
        return ev.Allowed;
    }
}
