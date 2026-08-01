using Content.Shared._AU14.Insurgency;

namespace Content.Server._AU14.Insurgency;

/// <summary>
///     Broadcast once when a faction definition is applied for the round. Other systems
///     (economy, spawn text, later phases) react to this instead of polling for the active
///     faction. Event-driven by design: the apply runs a single time on faction selection.
/// </summary>
[ByRefEvent]
public readonly record struct InsurgencyFactionAppliedEvent(FactionDefinition Definition);
