namespace Content.Shared.CMU14.Round.Antags.EarlyWarning;

/// <summary>Raised on the Joe after its level changes and the new level's grants are applied.</summary>
[ByRefEvent]
public readonly record struct EWJoeLevelChangedEvent(int OldLevel, int NewLevel);

/// <summary>
/// Raised on the Joe when its antag component shuts down (stand-down or admin removal), before the
/// level grants are cleared. A second ComponentShutdown subscription on EWJoeComponent made this
/// engine version throw "Duplicate Subscriptions" at startup (ComponentShutdown is a [ComponentEvent]),
/// so sibling systems listen to this instead.
/// </summary>
[ByRefEvent]
public readonly record struct EWJoeRemovedEvent;
