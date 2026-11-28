using Content.Shared.CMU14.Threats;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.CMU14.Threats.Shuttle;

/// <summary>Where a threat ship is in its arrival lifecycle.</summary>
public enum CMUThreatShuttleState : byte
{
    /// <summary>Loaded and parked at its deep-space return pad, waiting to launch.</summary>
    Loaded,

    /// <summary>Flying to <see cref="CMUThreatShuttleComponent.Destination"/>.</summary>
    InFlight,

    /// <summary>On its landing zone.</summary>
    Landed,

    /// <summary>Left its landing zone again (auto-return or a manual flight); unclaimed bodies were removed.</summary>
    Departed,
}

/// <summary>
///     Server-only state on a threat ship grid loaded for <see cref="ThreatEntryMethod.Shuttle"/> entry.
///     Added by <c>ThreatSystem</c> when the ship loads; <see cref="ThreatShuttleSystem"/> owns everything after.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(ThreatShuttleSystem), typeof(ThreatSystem))]
public sealed partial class CMUThreatShuttleComponent : Component
{
    /// <summary>The threat this ship carries.</summary>
    [DataField]
    public ProtoId<ThreatPrototype> Threat;

    /// <summary>The map the threat deployed to; landing zones are chosen on it.</summary>
    [ViewVariables]
    public MapId PlanetMap = MapId.Nullspace;

    /// <summary>The ship's navigation console.</summary>
    [DataField]
    public EntityUid? Navigation;

    /// <summary>The chosen landing zone, once a launch has started.</summary>
    [DataField]
    public EntityUid? Destination;

    [DataField]
    public CMUThreatShuttleState State = CMUThreatShuttleState.Loaded;

    /// <summary>Whether the threat's win conditions start when this ship lands.</summary>
    [DataField]
    public bool StartWinConditionsOnLanding;

    /// <summary>Launch settings copied from the threat at deployment.</summary>
    [DataField]
    public TimeSpan StartupTime = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan HyperspaceTime = TimeSpan.FromSeconds(15);

    /// <summary>Whether the automatic launch is still being attempted.</summary>
    [DataField]
    public bool LaunchPending;

    /// <summary>Next automatic launch attempt while no landing zone accepted the ship.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan RetryAt;

    /// <summary>When automatic launch attempts stop (and the console stays usable), or the flight is given up.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan LaunchDeadline;

    [DataField]
    public TimeSpan RetryInterval = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan LaunchTimeout = TimeSpan.FromSeconds(150);
}
