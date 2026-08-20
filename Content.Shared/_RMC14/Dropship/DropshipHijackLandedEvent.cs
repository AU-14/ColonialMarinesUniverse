namespace Content.Shared._RMC14.Dropship;

[ByRefEvent] // CMU14 struct
public readonly record struct DropshipHijackLandedEvent(EntityUid Map, string? HijackerFaction = null, string? VictimFaction = null, bool IsHumanHijack = false);
