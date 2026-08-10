namespace Content.Shared._RMC14.Xenonids;

/// <summary>
/// Raised after a xeno has completed its map initialization.
/// </summary>
[ByRefEvent]
public readonly record struct XenoMapInitializedEvent(EntityUid Xeno);
