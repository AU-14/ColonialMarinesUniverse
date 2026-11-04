namespace Content.Shared.CMU14.Medical.Anatomy.Organs.Events;

/// <summary>
/// Exact body clock boundary forwarded by the existing body event owner before its
/// physiological effects. A reset discards pending work; ordinary boundaries settle it.
/// InStasis overrides the still-queryable marker during its shutdown callback.
/// </summary>
[ByRefEvent]
public readonly record struct CMUOrganPhysiologyBoundaryEvent(
    EntityUid Body,
    TimeSpan Time,
    bool? InStasis = null,
    bool Reset = false);
