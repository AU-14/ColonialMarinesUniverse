using Robust.Shared.GameObjects;

namespace Content.Shared._CMU14.Medical.StatusEffects.Events;

[ByRefEvent]
public readonly record struct PainTierChangedEvent(
    EntityUid Body,
    PainTier OldTier,
    PainTier NewTier);
