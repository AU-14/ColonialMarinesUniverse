using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Weapons.ATGM;

/// <summary>
///     The fixed A.T.G.M. launcher inside the tripod. Holds the spotter link and the guidance
///     tunables copied onto every missile it fires.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUATGMSystem))]
public sealed partial class CMUATGMLauncherComponent : Component
{
    /// <summary>
    ///     The finder whose holder steers missiles fired from this launcher.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? LinkedFinder;

    /// <summary>
    ///     Skill required to load, link and spot for this launcher.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId<SkillDefinitionComponent> Skill = "CMUSkillATGM";

    /// <summary>
    ///     Missile cruise speed in tiles per second.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MissileSpeed = 9f;

    /// <summary>
    ///     Maximum rate the missile can change heading, in degrees per second.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TurnRateDegPerSec = 90f;

    /// <summary>
    ///     How far, in degrees, the missile may swing away from its launch heading. Wide enough that a
    ///     locked missile can keep homing on a target crossing its path.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MaxOffBoresightDeg = 120f;

    /// <summary>
    ///     The missile detonates after flying this long.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan MaxFlightTime = TimeSpan.FromSeconds(8);

    /// <summary>
    ///     The missile detonates once it is this close to the spotter's aim point, or to the bounding box of
    ///     its locked target.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ImpactRadius = 0.25f;

    /// <summary>
    ///     The missile cannot detonate on the aim point until it has flown this long, so a careless
    ///     spotter cannot blow up the crew at the tube.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan ArmingDelay = TimeSpan.FromSeconds(0.3);

    /// <summary>
    ///     Aim points further than this from the spotter are pulled back to this distance.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MaxGuidanceDistance = 30f;

    /// <summary>
    ///     With a lock, the furthest the spotter's cursor can pull the aim point away from the target, in tiles.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MaxSteerOffset = 4f;

    /// <summary>
    ///     With a lock, the cursor's pull is also limited to this fraction of the missile's distance to the target.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SteerOffsetPerDistance = 0.5f;

    /// <summary>
    ///     Within this many tiles of a locked target the spotter's cursor has no effect.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TerminalDistance = 2.5f;

    /// <summary>
    ///     Beyond <see cref="TerminalDistance"/>, the cursor's pull fades in over this many tiles.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float FadeDistance = 3f;

    /// <summary>
    ///     With a lock, a spotter cursor older than this is ignored and the missile homes straight on the target.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan CursorFreshness = TimeSpan.FromSeconds(0.5);

    /// <summary>
    ///     Maximum lead, in tiles, applied ahead of a moving locked target.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MaxLead = 2f;

    /// <summary>
    ///     If the spotter cannot see the locked target for this long, the missile stops tracking it and flies
    ///     to where it was last seen.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan TargetSightTimeout = TimeSpan.FromSeconds(2);
}
