using Content.Shared._RMC14.Atmos;
using Content.Shared.CMU14.Dropship.DirectFire;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Explosion.Implosion;
using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Dropship.Weapon;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedDropshipWeaponSystem), typeof(GunshipDirectFireSystem), typeof(AmmoHolderSystem))]
public sealed partial class DropshipAmmoComponent : Component
{
    [DataField, AutoNetworkedField]
    public int TargetSpread = 3;

    [DataField, AutoNetworkedField]
    public int BulletSpread = 3;

    [DataField(required: true), AutoNetworkedField]
    public int Rounds = 400;

    [DataField(required: true), AutoNetworkedField]
    public int MaxRounds = 400;

    [DataField(required: true), AutoNetworkedField]
    public int RoundsPerShot = 40;

    [DataField, AutoNetworkedField]
    public int? RoundsPerSprite;

    [DataField, AutoNetworkedField]
    public int ShotsPerVolley = 1;

    /// <summary>Separate impact locations per volley, independent of ammunition consumption.</summary>
    [DataField, AutoNetworkedField]
    public int ImpactsPerVolley = 1;

    [DataField, AutoNetworkedField]
    public bool SelectableLaserPattern;

    [DataField, AutoNetworkedField]
    public bool SelectableAirburstHeight;

    [DataField, AutoNetworkedField]
    public DamageSpecifier? Damage;

    [DataField, AutoNetworkedField]
    public int ArmorPiercing = 10;

    [DataField(required: true), AutoNetworkedField]
    public EntProtoId<DropshipWeaponComponent> Weapon = new(string.Empty);

    [DataField, AutoNetworkedField]
    public TimeSpan TravelTime = TimeSpan.FromSeconds(10);

    [DataField, AutoNetworkedField]
    public TimeSpan SoundTravelTime = TimeSpan.FromSeconds(1.1);

    [DataField, AutoNetworkedField]
    public SoundSpecifier? SoundCockpit;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? SoundMarker;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? SoundGround;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? SoundImpact;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? SoundWarning;

    [DataField, AutoNetworkedField]
    public List<EntProtoId> ImpactEffects = new();

    [DataField, AutoNetworkedField]
    public string? AmmoType;

    /// <summary>Allows distinct ammunition types to share existing depletion artwork.</summary>
    [DataField, AutoNetworkedField]
    public string? AmmoSpriteType;

    [DataField, AutoNetworkedField]
    public RMCExplosion? Explosion;

    [DataField, AutoNetworkedField]
    public RMCImplosion? Implosion;

    [DataField, AutoNetworkedField]
    public RMCFire? Fire;

    [DataField, AutoNetworkedField]
    public int SoundEveryShots = 3;

    [DataField, AutoNetworkedField]
    public int ZLevelPenetration = 0;

    [DataField, AutoNetworkedField]
    public bool ApplyEffectsOnPenetrationLevels = false;

    [DataField, AutoNetworkedField]
    public bool TargetLowestZLevel = false;

    [DataField, AutoNetworkedField]
    public bool DeleteOnEmpty;

    [DataField, AutoNetworkedField]
    public bool MarkerWarning;

    [DataField, AutoNetworkedField]
    public TimeSpan MarkerDuration;

    [DataField, AutoNetworkedField]
    public int? FireMissionDelay;
}

[Serializable, NetSerializable]
public enum DropshipAmmoVisuals
{
    Fill,
}
