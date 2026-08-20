namespace Content.Shared._RMC14.Xenonids.Egg;

[ByRefEvent] // CMU14 struct
public readonly record struct XenoOvipositorChangedEvent(bool Attached, EntityUid? Queen = null, EntityUid? Hive = null);
