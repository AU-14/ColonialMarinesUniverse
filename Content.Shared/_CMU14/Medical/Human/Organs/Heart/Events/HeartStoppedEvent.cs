using Robust.Shared.GameObjects;

namespace Content.Shared._CMU14.Medical.Human.Organs.Heart.Events;

[ByRefEvent]
public readonly record struct HeartStoppedEvent(EntityUid Body, EntityUid Heart);
