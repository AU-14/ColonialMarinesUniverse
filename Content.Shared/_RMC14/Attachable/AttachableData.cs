using Content.Shared._RMC14.Attachable.Components;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Attachable;

[DataDefinition]
[Serializable, NetSerializable]
public partial struct AttachableSlot()
{
    [DataField]
    public bool Locked;

    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public EntProtoId<AttachableComponent>? StartingAttachable;

    [DataField]
    public List<EntProtoId<AttachableComponent>>? Random;

    [DataField]
    public float RandomChance = 1f;
}

[DataRecord, Serializable, NetSerializable]
public partial record struct AttachableModifierConditions
{
    public AttachableModifierConditions()
    {
    }

    public bool UnwieldedOnly { get; set; }
    public bool WieldedOnly { get; set; }
    public bool ActiveOnly { get; set; }
    public bool InactiveOnly { get; set; }
    public EntityWhitelist? Whitelist { get; set; }
    public EntityWhitelist? Blacklist { get; set; }
}

[DataRecord, Serializable, NetSerializable]
public partial record struct AttachableWeaponMeleeModifierSet
{
    public AttachableWeaponMeleeModifierSet()
    {
    }

    public AttachableModifierConditions? Conditions { get; set; }
    public DamageSpecifier? BonusDamage { get; set; }
    public DamageImpactProfile? Impact { get; set; }
}

[DataRecord, Serializable, NetSerializable]
public partial record struct AttachableWeaponRangedModifierSet
{
    public AttachableWeaponRangedModifierSet()
    {
    }

    public AttachableModifierConditions? Conditions { get; set; }

    // Affects the accuracy of all shots fired by the weapon. Conversion from 13: accuracy_mod or accuracy_unwielded_mod.
    public FixedPoint2 AccuracyAddMult { get; set; }

    // This affects the damage falloff of all shots fired by the weapon. Conversion to RMC: damage_falloff_mod.
    public FixedPoint2 DamageFalloffAddMult { get; set; }

    // This affects scatter during burst and full-auto fire. Conversion to RMC: burst_scatter_mod.
    public double BurstScatterAddMult { get; set; }

    // Modifies the maximum number of shots in a burst.
    public int ShotsPerBurstFlat { get; set; }

    // Additive multiplier to damage.
    public FixedPoint2 DamageAddMult { get; set; }

    // How much the camera shakes when you shoot.
    public float RecoilFlat { get; set; }

    // Scatter in degrees. This is how far bullets go from where you aim. Conversion to RMC: CM_SCATTER * 2.
    public double ScatterFlat { get; set; }

    // The delay between each shot. Conversion to RMC: CM_FIRE_DELAY / 10.
    public float FireDelayFlat { get; set; }

    // How fast the projectiles move. Conversion to RMC: CM_PROJECTILE_SPEED * 10.
    public float ProjectileSpeedFlat { get; set; }

    // The distance in tiles at which the damage of the projectiles starts to drop off. Conversion to RMC: projectile_max_range_mod.
    public float RangeFlat { get; set; }
}

[DataRecord, Serializable, NetSerializable]
public partial record struct AttachableWeaponFireModesModifierSet
{
    public AttachableWeaponFireModesModifierSet()
    {
    }

    public AttachableModifierConditions? Conditions { get; set; }
    public SelectiveFire ExtraFireModes { get; set; }
    public SelectiveFire SetFireMode { get; set; }
}

// SS13 has move delay instead of speed. Move delay isn't implemented here, and approximating it through maths like fire delay is scuffed because of how the events used to change speed work.
// So instead we take the default speed values and use them to convert it to a multiplier beforehand.
// Converting from move delay to additive multiplier: 1 / (1 / SS14_SPEED + SS13_MOVE_DELAY / 10) / SS14_SPEED - 1
// Speed and move delay are inversely proportional. So 1 divided by speed is move delay and vice versa.
// We then add the ss13 move delay, and divide 1 by the result to convert it back into speed.
// Then we divide it by the original speed and subtract 1 from the result to get the additive multiplier.
[DataRecord, Serializable, NetSerializable]
public partial record struct AttachableSpeedModifierSet
{
    public AttachableSpeedModifierSet()
    {
    }

    public AttachableModifierConditions? Conditions { get; set; }

    // Default human walk speed: 2.5f.
    public float Walk { get; set; }

    // Default human sprint speed: 4.5f.
    public float Sprint { get; set; }
}

[DataRecord, Serializable, NetSerializable]
public partial record struct AttachableSizeModifierSet
{
    public AttachableSizeModifierSet()
    {
    }

    public AttachableModifierConditions? Conditions { get; set; }
    public int Size { get; set; }
}

[DataRecord, Serializable, NetSerializable]
public partial record struct AttachableWieldDelayModifierSet
{
    public AttachableWieldDelayModifierSet()
    {
    }

    public AttachableModifierConditions? Conditions { get; set; }
    public TimeSpan Delay { get; set; }
}
