using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Ape;

public readonly record struct ApeLeapHitEvent(Entity<ApeLeapingComponent> Leaping, EntityUid Hit);





