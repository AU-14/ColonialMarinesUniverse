namespace Content.Shared.CMU14.Threats;

/// <summary>
///     Per-body opt-outs from the generic threat stamps applied when a threat body spawns or is claimed.
///     Lives in Shared so body prototypes validate on both sides.
/// </summary>
[RegisterComponent]
public sealed partial class CMUThreatBodyOptionsComponent : Component
{
    /// <summary>
    ///     When true, the body never receives <see cref="ThreatComponent"/>, the THREAT NPC faction or the
    ///     objective watch that the threat pipeline normally adds.
    /// </summary>
    [DataField]
    public bool SkipThreatFaction;

    /// <summary>
    ///     When true, a mind placed in this body by the threat pipeline (seat assignment or ghost role)
    ///     does not receive the generic <c>MindRoleThreat</c> role.
    /// </summary>
    [DataField]
    public bool SkipThreatMindRole;

    /// <summary>
    ///     When true, an unclaimed body keeps its own ghost role (job, name, description, rules) instead of
    ///     the generic threat ghost role, so its job's whitelist, requirements and playtime tracker apply.
    /// </summary>
    [DataField]
    public bool KeepGhostRole;
}
