using Content.Shared.Body.Part;
using Robust.Shared.GameObjects;

namespace Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;

[ByRefEvent]
public readonly record struct BodyPartSeveredEvent(EntityUid Body, EntityUid Part, BodyPartType Type);

/// <summary>A request to detach an exact attached part. Only a successful commit publishes BodyPartSeveredEvent.</summary>
[ByRefEvent]
public record struct BodyPartSeverAttemptEvent(EntityUid Body, EntityUid Part, BodyPartType Type, bool Surgical = false)
{
    public bool Cancelled;
    public bool Succeeded;
    public EntityUid? DetachedBody;
}
