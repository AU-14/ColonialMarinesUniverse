namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// The Vessel Strike: a timed orbital strike on the Bad Blood clan vessel, fired from a GOVFOR overwatch console once
/// the uplink is done. It pauses while the lock site is down and wins Marine Major when it runs out.
/// Lives on the rule entity. docs/GraphAudit-BadBlood-Objectives.md §5.3.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodVesselStrikeComponent : Component
{
    // ---- tunables ----

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(1080);

    /// <summary>
    /// Added back to the remaining time once per sabotage of the lock site, never above <see cref="Duration"/>.
    /// </summary>
    [DataField]
    public TimeSpan SabotagePenalty = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The do-after to move the lock to another working uplink site.
    /// </summary>
    [DataField]
    public TimeSpan RerouteTime = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Sabotage immunity of a site after it is repaired or the lock is re-routed to it.
    /// </summary>
    [DataField]
    public TimeSpan RepairImmunity = TimeSpan.FromSeconds(180);

    /// <summary>
    /// After a shipside cancel, the strike cannot be fired again for this long.
    /// </summary>
    [DataField]
    public TimeSpan CancelLockout = TimeSpan.FromSeconds(300);

    /// <summary>
    /// Job prototype ids allowed to fire the strike. Jobs do not inherit, so every faction variant is listed.
    /// </summary>
    [DataField]
    public List<string> AuthorizedJobs = new();

    [DataField]
    public TimeSpan Tick = TimeSpan.FromSeconds(1);

    // ---- state ----

    [ViewVariables]
    public TimeSpan NextTick;

    [ViewVariables]
    public bool Running;

    [ViewVariables]
    public bool Paused;

    [ViewVariables]
    public bool Completed;

    [ViewVariables]
    public TimeSpan Remaining;

    [ViewVariables]
    public TimeSpan PausedTotal;

    [ViewVariables]
    public TimeSpan RunTotal;

    [ViewVariables]
    public TimeSpan LockoutUntil;

    /// <summary>
    /// The lock site was sabotaged during the current pause and the penalty was already applied.
    /// </summary>
    [ViewVariables]
    public bool PenaltyApplied;

    [ViewVariables]
    public EntityUid? Console;

    /// <summary>
    /// The ship (z-network owner) the strike was fired from.
    /// </summary>
    [ViewVariables]
    public EntityUid? Ship;
}

/// <summary>
/// Marks the overwatch console a running Vessel Strike was fired from. Shipside sabotage targets it.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodVesselStrikeConsoleComponent : Component;
