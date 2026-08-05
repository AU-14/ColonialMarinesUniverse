using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Weapons.Melee;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedRMCMeleeWeaponSystem))]
public sealed partial class MeleeReceivedMultiplierComponent : Component
{
    /// <summary>
    /// When set, xeno melee damage is replaced by this spec. Otherwise, incoming xeno
    /// damage is scaled by <see cref="XenoMultiplier"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public DamageSpecifier? XenoDamage; // TODO RMC14 other hives

    [DataField, AutoNetworkedField]
    public FixedPoint2 XenoMultiplier = FixedPoint2.New(1);

    [DataField, AutoNetworkedField]
    public FixedPoint2 OtherMultiplier = FixedPoint2.New(1);
}
