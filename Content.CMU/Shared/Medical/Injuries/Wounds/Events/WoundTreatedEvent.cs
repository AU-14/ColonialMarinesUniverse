using Robust.Shared.GameObjects;

namespace Content.Shared.CMU14.Medical.Injuries.Wounds.Events;

[ByRefEvent]
public readonly record struct WoundTreatedEvent(EntityUid Body, EntityUid Part);
