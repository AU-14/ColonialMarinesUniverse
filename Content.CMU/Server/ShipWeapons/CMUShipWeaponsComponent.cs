using Content.Shared.CMU14.ShipWeapons;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.CMU14.ShipWeapons;

/// <summary>
/// Lives on the ship's grid or multi-deck network, never on a replaceable console.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(CMUShipWeaponsSystem))]
public sealed partial class CMUShipWeaponsComponent : Component
{
    [DataField]
    public HashSet<ProtoId<CMUShipWeaponPrototype>> Weapons = new();

    /// <summary>Whether an arming terminal has locked in this ship's loadout. Bonus disks don't lock it.</summary>
    [DataField]
    public bool Locked;

    /// <summary>
    /// Fire missions shared by every armed weapon, copied from the confirming terminal when the loadout locks.
    /// </summary>
    [DataField]
    public int RemainingUses;

    [DataField]
    public int MaxUses;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextLaunch;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(60);
}

[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(CMUShipWeaponsSystem))]
public sealed partial class CMUShipWeaponStrikeComponent : Component
{
    [DataField(required: true)]
    public ProtoId<CMUShipWeaponPrototype> Weapon;

    [DataField]
    public CMUShipWeaponBearing Bearing;

    [DataField]
    public int Shot;

    [DataField]
    public bool Warned;

    [DataField]
    public bool TravelPlayed;

    [DataField]
    public bool DirectionalWarned;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan DirectionalWarningAt;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextShot;
}

[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(CMUShipWeaponsSystem))]
public sealed partial class CMUShipWeaponEmsFieldComponent : Component
{
    [DataField]
    public float Radius = 30;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan EndsAt;

    [DataField]
    public TimeSpan StunAfterField = TimeSpan.FromSeconds(25);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextPulse;
}
