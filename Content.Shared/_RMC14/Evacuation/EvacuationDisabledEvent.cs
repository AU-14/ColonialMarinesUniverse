namespace Content.Shared._RMC14.Evacuation;

[ByRefEvent] // CMU14 struct
public readonly record struct EvacuationDisabledEvent(EntityUid Map);
