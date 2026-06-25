namespace Content.Shared._CMU14.Threats.Mobs.Ape;

public readonly record struct ApeLeapHitEvent(Entity<ApeLeapingComponent> Leaping, EntityUid Hit);
