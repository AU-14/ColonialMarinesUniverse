using Robust.Shared.Map;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning;

/// <summary>
/// Raised on the Joe (directed, for the objective ladder) and broadcast (for everyone else) after its
/// level changes and the new level's grants are applied. Directed slots on EWJoeComponent are unique
/// per event across all systems, so later phases listen broadcast and read <see cref="Joe"/>.
/// </summary>
[ByRefEvent]
public readonly record struct EWJoeLevelChangedEvent(EntityUid Joe, int OldLevel, int NewLevel);

/// <summary>
/// Raised on the Joe when its antag component shuts down (stand-down or admin removal), before the
/// level grants are cleared. A second ComponentShutdown subscription on EWJoeComponent made this
/// engine version throw "Duplicate Subscriptions" at startup (ComponentShutdown is a [ComponentEvent]),
/// so sibling systems listen to this instead. Directed subscriptions are unique per component and
/// event pair across all systems too, so it is raised directed (for the objective ladder) and
/// broadcast (for everyone else), and carries the Joe for the broadcast listeners.
/// </summary>
[ByRefEvent]
public readonly record struct EWJoeRemovedEvent(EntityUid Joe);

/// <summary>
/// Raised on the Joe (directed: the melding scheduler) and broadcast after a console was assimilated
/// and its effect applied (phase E2).
/// </summary>
[ByRefEvent]
public readonly record struct EWJoeConsoleAssimilatedEvent(EntityUid Joe, EntityUid Console);

/// <summary>
/// Raised on the console when its repair chain completes and the marker is removed. The Joe may be
/// dead by then, so the event carries who assimilated it.
/// </summary>
[ByRefEvent]
public readonly record struct EWJoeConsoleRepairedEvent(EntityUid Repairer, EntityUid? AssimilatedBy);

/// <summary>Raised on the Joe and broadcast after a victim became its River-Melded thrall (phase E3).</summary>
[ByRefEvent]
public readonly record struct EWJoeThrallMadeEvent(EntityUid Joe, EntityUid Thrall);

/// <summary>Raised on a thrall when it is freed (Weyland key, master dead or stood down).</summary>
[ByRefEvent]
public readonly record struct EWJoeThrallFreedEvent(EntityUid? Master);

/// <summary>
/// Broadcast on the server after a Disrupt pulse (phase E5, A-2). The Disrupt handler is shared and
/// its directed slot is taken; the server-only tier-two effects (doors, sapper traps, sentries) listen
/// to this and read <see cref="Level"/>.
/// </summary>
[ByRefEvent]
public readonly record struct EWJoeDisruptPulseEvent(EntityUid Joe, EntityCoordinates Coordinates, float Range, int Level);

/// <summary>
/// Broadcast when the ewjoemechanics switch changes. Off: systems end what a Joe has running (a choke, a drawn
/// machete) and grey out actions they granted themselves (the scoured cloak).
/// </summary>
[ByRefEvent]
public readonly record struct EWJoeMechanicsChangedEvent(bool Enabled);

