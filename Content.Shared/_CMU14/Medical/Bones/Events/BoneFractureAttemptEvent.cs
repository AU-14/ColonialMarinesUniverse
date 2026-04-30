using Robust.Shared.GameObjects;

namespace Content.Shared._CMU14.Medical.Bones.Events;

/// <summary>
///     Raised on a body-part entity right before a fracture is assigned (or
///     upgraded) so other systems can veto. Setting <see cref="Cancelled"/> to
///     true skips the assignment entirely.
/// </summary>
public sealed class BoneFractureAttemptEvent : CancellableEntityEventArgs
{
    public readonly EntityUid Part;
    public readonly FractureSeverity ProposedSeverity;

    public BoneFractureAttemptEvent(EntityUid part, FractureSeverity proposed)
    {
        Part = part;
        ProposedSeverity = proposed;
    }
}
