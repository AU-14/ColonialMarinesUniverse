using Content.Shared.Body.Part;
using Robust.Shared.GameObjects;

namespace Content.Shared._CMU14.Medical.BodyPart.Events;

[ByRefEvent]
public readonly record struct BodyPartSeveredEvent(EntityUid Body, EntityUid Part, BodyPartType Type);
