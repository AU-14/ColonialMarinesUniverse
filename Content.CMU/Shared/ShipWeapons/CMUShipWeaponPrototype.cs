using Content.Shared.Damage;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ShipWeapons;

[Prototype("cmuShipWeapon")]
public sealed partial class CMUShipWeaponPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public int Shots = 1;

    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(1);

    [DataField]
    public float Spread;

    [DataField]
    public float WalkingDistance;

    [DataField]
    public float BlastRadius;

    [DataField]
    public float Slope = 10;

    [DataField]
    public float MaxIntensity = 90;

    [DataField]
    public bool Airburst;

    [DataField]
    public EntProtoId? Cloud;

    [DataField]
    public int CloudRadius;

    [DataField]
    public bool Ems;

    [DataField]
    public DamageSpecifier? RailDamage;

    [DataField]
    public TimeSpan FieldDuration = TimeSpan.FromSeconds(15);

    [DataField]
    public TimeSpan StunAfterField = TimeSpan.FromSeconds(25);

    /// <summary>
    /// Includes the complete barrage pattern, not just an individual shell's blast.
    /// </summary>
    public float FootprintRadius => Math.Max(
        RailDamage != null ? 2.5f * MathF.Sqrt(2) : Spread + WalkingDistance / 2 + BlastRadius,
        CloudRadius + 0.75f);
}

[Serializable, NetSerializable]
public readonly record struct CMUShipWeaponStatus(ProtoId<CMUShipWeaponPrototype> Weapon, int Remaining, int Max);

[Serializable, NetSerializable]
public enum CMUShipWeaponBearing : byte
{
    North,
    East,
    South,
    West,
}
