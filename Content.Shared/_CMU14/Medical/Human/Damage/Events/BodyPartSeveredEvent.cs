using Content.Shared.Body.Part;
using Robust.Shared.GameObjects;

namespace Content.Shared._CMU14.Medical.Human.Damage.Events;

[ByRefEvent]
public readonly record struct BodyPartSeveredEvent(EntityUid Body, EntityUid Part, BodyPartType Type);

[ByRefEvent]
public readonly record struct BodyPartSeveranceAppliedEvent(EntityUid Body, EntityUid Part, BodyPartType Type);
