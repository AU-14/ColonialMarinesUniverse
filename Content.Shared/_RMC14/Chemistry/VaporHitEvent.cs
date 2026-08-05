using Content.Shared.Chemistry.Components;

namespace Content.Shared._RMC14.Chemistry;

[ByRefEvent]
public readonly record struct VaporHitEvent(Entity<SolutionComponent> Solution, int Power);
