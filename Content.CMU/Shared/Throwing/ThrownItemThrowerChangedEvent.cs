using Robust.Shared.GameObjects;

namespace Content.Shared.Throwing;

[ByRefEvent]
public readonly record struct ThrownItemThrowerChangedEvent(EntityUid? PreviousThrower);
