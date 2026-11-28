using Content.Shared.CMU14.Threats;

namespace Content.Server.CMU14.Threats;

/// <summary>
///     Broadcast once a threat deployment completes: after the threat's <see cref="ThreatPrototype.AddGameRules"/>
///     have started and before its win conditions start (win conditions wait for the landing instead when
///     <see cref="ThreatPrototype.WinConditionsOnLanding"/> is set).
/// </summary>
/// <param name="Threat">The deployed threat.</param>
/// <param name="Leaders">Spawned leader bodies, with or without minds.</param>
/// <param name="Members">Spawned member bodies, with or without minds.</param>
/// <param name="Shuttle">The ship grid for <see cref="ThreatEntryMethod.Shuttle"/> entry; otherwise null.</param>
[ByRefEvent]
public readonly record struct ThreatDeployedEvent(
    ThreatPrototype Threat,
    IReadOnlyList<EntityUid> Leaders,
    IReadOnlyList<EntityUid> Members,
    EntityUid? Shuttle);
