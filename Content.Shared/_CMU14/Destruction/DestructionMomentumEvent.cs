using Robust.Shared.GameObjects;

namespace Content.Shared._CMU14.Destruction;

/// <summary>
/// Server-authoritative query for the amount of impact speed required to remove
/// a damageable obstruction. Destruction thresholds are server-only, so shared
/// movement systems use this event instead of referencing server components.
/// </summary>
[ByRefEvent]
public record struct DestructionMomentumQueryEvent(
    EntityUid Target,
    float AvailableSpeed,
    float DamageMultiplier)
{
    public bool HasRemovalThreshold;
    public bool CanDestroy;
    public float RequiredSpeed;
}
