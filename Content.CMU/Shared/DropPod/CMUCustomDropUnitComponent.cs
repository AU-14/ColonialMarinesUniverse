using Content.Shared.Damage;
using Content.Shared.Maps;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.DropPod;

/// <summary>
/// The core of a drop unit built from ordinary walls and doors. Like an elevator car, the unit is whatever room the core
/// sits in: the room's floor, walls, doors and everything inside are cut out of the grid and dropped onto the planet.
/// It can be mapped or built mid-round anywhere on a ship; it cannot heal its occupants or store weapons.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUCustomDropUnitComponent : Component
{
    [DataField, AutoNetworkedField]
    public string Group = "default";

    [DataField, AutoNetworkedField]
    public CMUDropPodState State = CMUDropPodState.Ready;

    /// <summary>
    /// Full-tile pieces that seal the room. They form part of the unit and drop with it.
    /// </summary>
    [DataField]
    public EntityWhitelist? WallWhitelist;

    /// <summary>
    /// Doors seal the room like walls, drop with it, and are shut and bolted for the flight.
    /// </summary>
    [DataField]
    public EntityWhitelist? DoorWhitelist;

    [DataField]
    public bool RequireDoor = true;

    /// <summary>
    /// Maximum tiles in the unit, including its walls and doors.
    /// </summary>
    [DataField]
    public int MaxTiles = 64;

    /// <summary>
    /// Tile left on the ship where the unit's floor was. Null leaves the tiles empty.
    /// </summary>
    [DataField]
    public ProtoId<ContentTileDefinition>? LeftBehindTile = "Plating";

    /// <summary>
    /// Shown to everyone aboard in order. The unit stays docked with its doors bolted until the last phase ends.
    /// Parallax and velocity are unused because the unit does not leave the ship until it lands.
    /// </summary>
    [DataField]
    public List<CMUDropPodTravelPhase> Phases = new();

    [DataField]
    public SoundSpecifier? LaunchSound;

    [DataField]
    public SoundSpecifier? LandingSound;

    [DataField]
    public float ScatterRadius = 60;

    /// <summary>
    /// Landing points to try. Each one must fit the whole unit.
    /// </summary>
    [DataField]
    public int LandingAttempts = 64;

    [DataField]
    public int TargetDepth;

    [DataField]
    public EntityWhitelist? LandingBlockers;

    [DataField]
    public bool BlockersMustBeIndestructible = true;

    [DataField]
    public bool RespectParadropAreas = true;

    /// <summary>
    /// Dealt to mobs standing where the unit lands.
    /// </summary>
    [DataField]
    public DamageSpecifier? CrushDamage;

    [DataField]
    public float InjuryChance;

    [DataField]
    public DamageSpecifier? InjuryDamage;

    [DataField]
    public TimeSpan InjuryStun;

    [DataField]
    public float AtmosphereBreakupChance;

    [DataField]
    public float ImpactBreakupChance;

    /// <summary>
    /// Added to the scatter radius when the unit breaks apart in the atmosphere.
    /// </summary>
    [DataField]
    public float BreakupExtraScatter;

    [DataField]
    public DamageSpecifier? BreakupDamage;

    [DataField]
    public TimeSpan BreakupStun;

    [DataField]
    public EntProtoId? BreakupDebris;

    [DataField]
    public SoundSpecifier? BreakupSound;

    [DataField]
    public int LandingShakes = 3;

    [DataField]
    public int LandingShakeStrength = 5;
}
