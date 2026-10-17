using Robust.Shared.GameObjects;

namespace Content.Shared.CMU14.Medical.Anatomy.Organs.Heart.Events;

[ByRefEvent]
public readonly record struct HeartStoppedEvent(EntityUid Body, EntityUid Heart);
