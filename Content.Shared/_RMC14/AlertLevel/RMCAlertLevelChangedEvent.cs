using Robust.Shared.GameObjects;

namespace Content.Shared._RMC14.AlertLevel;

[ByRefEvent]
// CMU14: Ship carries the warship whose level changed, null for the legacy global entity
public readonly record struct RMCAlertLevelChangedEvent(RMCAlertLevels Level, EntityUid? Ship = null);
