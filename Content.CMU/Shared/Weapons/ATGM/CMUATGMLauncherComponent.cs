using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using System.Collections.Generic;
using System.Linq;

namespace Content.Shared.CMU14.Weapons.ATGM;

/// <summary>
///     The fixed A.T.G.M. launcher inside the tripod. Holds the finder link and the guidance
///     tunables copied onto every missile it fires.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUATGMSystem))]
public sealed partial class CMUATGMLauncherComponent : Component
{
    /// <summary>
    ///     The finder linked to this launcher.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? LinkedFinder;

    /// <summary>
    ///     Skill required to crew the ground tripod.
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
    ///     The missile detonates once it is this close to the gunner's aim point, or to the bounding box of
    ///     its locked target.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ImpactRadius = 0.25f;

    /// <summary>
    ///     The missile cannot detonate on the aim point until it has flown this long, protecting the crew at the tube.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan ArmingDelay = TimeSpan.FromSeconds(0.3);

    /// <summary>
    ///     Aim points further than this from the gunner are pulled back to this distance.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MaxGuidanceDistance = 30f;

    /// <summary>
    ///     With a lock, the furthest the gunner's cursor can pull the aim point away from the target, in tiles.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MaxSteerOffset = 4f;

    /// <summary>
    ///     With a lock, the cursor's pull is also limited to this fraction of the missile's distance to the target.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SteerOffsetPerDistance = 0.5f;

    /// <summary>
    ///     Within this many tiles of a locked target the gunner's cursor has no effect.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TerminalDistance = 2.5f;

    /// <summary>
    ///     Beyond <see cref="TerminalDistance"/>, the cursor's pull fades in over this many tiles.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float FadeDistance = 3f;

    /// <summary>
    ///     With a lock, a gunner cursor older than this is ignored and the missile homes straight on the target.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan CursorFreshness = TimeSpan.FromSeconds(0.5);

    /// <summary>
    ///     Maximum lead, in tiles, applied ahead of a moving locked target.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MaxLead = 2f;

    /// <summary>
    ///     If the gunner cannot see the locked target for this long, the missile stops tracking it and flies
    ///     to where it was last seen.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan TargetSightTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    ///     For gunship launchers: list of currently marked enemy entities with their mark times.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<(EntityUid Entity, TimeSpan MarkedAt)> MarkedTargets = new();

    /// <summary>
    ///     For gunship launchers: maximum number of targets that can be marked simultaneously.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int MaxMarkedTargets = 3;

    /// <summary>
    ///     For gunship launchers: duration before a mark expires, in seconds. 0 means indefinite.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MarkExpirationSeconds = 30f;

    /// <summary>
    ///     For gunship launchers: requires a marked target to fire.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool RequireMarkedTargetToFire = true;

    /// <summary>
    ///     Returns the currently valid marked targets (removing expired ones).
    /// </summary>
    public IReadOnlyList<EntityUid> GetValidMarkedTargets(TimeSpan currentTime)
    {
        var validTargets = new List<EntityUid>();
        
        // Remove expired marks and collect valid ones
        MarkedTargets = MarkedTargets
            .Where(m =>
            {
                if (MarkExpirationSeconds > 0)
                {
                    var age = (currentTime - m.MarkedAt).TotalSeconds;
                    if (age > MarkExpirationSeconds)
                        return false;
                }
                validTargets.Add(m.Entity);
                return true;
            })
            .ToList();

        return validTargets;
    }
}
