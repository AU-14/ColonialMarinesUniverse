using Robust.Shared.GameObjects;

namespace Content.Shared._CMU14.Medical.Wounds.Events;

public readonly record struct WoundTreatedEvent(EntityUid Body, EntityUid Part);
