namespace Content.Shared.Throwing;

/// <summary>
/// Raised on an entity after it has thrown something.
/// </summary>
[ByRefEvent]
public readonly record struct ThrowEvent(EntityUid? User, EntityUid Thrown);

/// <summary>
/// Raised on an entity after it has been thrown.
/// </summary>
[ByRefEvent]
public readonly record struct ThrownEvent(EntityUid? User, EntityUid Thrown);

/// <summary>
/// Raised directed on the target entity being hit by the thrown entity.
/// </summary>
[ByRefEvent]
public readonly record struct ThrowHitByEvent(EntityUid Thrown, EntityUid Target, ThrownItemComponent Component);

/// <summary>
/// Raised directed on the thrown entity that hits another.
/// </summary>
[ByRefEvent]
public record struct ThrowDoHitEvent(EntityUid Thrown, EntityUid Target, ThrownItemComponent Component)
{
    // CMU14: a custom thrown weapon may handle its impact before generic damage and stamina.
    public bool Handled;
}
