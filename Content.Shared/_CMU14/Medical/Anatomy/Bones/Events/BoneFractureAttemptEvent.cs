namespace Content.Shared._CMU14.Medical.Anatomy.Bones.Events;

/// <summary>
///     Raised on a body-part entity right before a fracture is assigned (or
///     upgraded) so other systems can veto. Setting <see cref="Cancelled"/> to
///     true skips the assignment entirely.
/// </summary>
[ByRefEvent]
public record struct BoneFractureAttemptEvent(
    EntityUid Part,
    FractureSeverity ProposedSeverity,
    bool Cancelled = false);
