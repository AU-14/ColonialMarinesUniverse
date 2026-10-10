namespace Content.Shared.CMU14.ZLevels.Core;

[ByRefEvent]
public readonly record struct CMUZPairedEvent(EntityUid Twin);

[ByRefEvent]
public readonly record struct CMUZUnpairedEvent(EntityUid FormerTwin);
