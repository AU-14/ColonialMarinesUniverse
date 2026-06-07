namespace Content.Shared._RMC14.Xenonids.Hive;

[ByRefEvent]
public readonly record struct BurrowedLarvaAddedEvent(EntityUid Hive, int Amount);
