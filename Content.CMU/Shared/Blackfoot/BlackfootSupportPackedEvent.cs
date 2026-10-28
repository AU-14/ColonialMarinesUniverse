namespace Content.Shared.CMU14.Blackfoot;

/// <summary>raised on the deployed gear right before it's deleted, so the packed kit can copy stuff off it</summary>
[ByRefEvent]
public readonly record struct BlackfootSupportPackedEvent(EntityUid Packed, EntityUid User);
