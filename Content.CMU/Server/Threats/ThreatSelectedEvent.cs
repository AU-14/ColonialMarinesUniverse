using Robust.Shared.GameObjects;

namespace Content.Server.CMU14.Threats;

/// <summary>
///     Raised after the round's selected threat changes mid-round (post-roundstart threat vote),
///     so state seeded at round start can re-evaluate against the chosen threat.
/// </summary>
[ByRefEvent]
public readonly record struct ThreatSelectedEvent;
