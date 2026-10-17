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
    public Dictionary<ProtoId<CMUShipWeaponPrototype>, int> Weapons = new();

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

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextShot;
}

[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(CMUShipWeaponsSystem))]
public sealed partial class CMUShipWeaponEmsFieldComponent : Component
{
    [DataField]
    public float Radius = 5;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextPulse;
}
