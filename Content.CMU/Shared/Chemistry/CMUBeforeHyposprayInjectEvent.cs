namespace Content.Shared.CMU14.Chemistry;

/// <summary>
/// Raised on the target just before an RMC hypospray doses it, so CMU procedures can
/// attribute the injection to its user.
/// </summary>
/// <param name="User">Who is using the hypospray.</param>
/// <param name="Hypospray">The hypospray being used.</param>
[ByRefEvent]
public readonly record struct CMUBeforeHyposprayInjectEvent(EntityUid User, EntityUid Hypospray);
