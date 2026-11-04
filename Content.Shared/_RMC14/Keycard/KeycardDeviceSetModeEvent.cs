using Content.Shared._RMC14.AlertLevel; // CMU14: mode carries a target alert level now
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Keycard;

// CMU14: was KeycardDeviceMode Mode
// public sealed record KeycardDeviceSetModeEvent(KeycardDeviceMode Mode);
[Serializable, NetSerializable]
public sealed record KeycardDeviceSetModeEvent(RMCAlertLevels Mode);
