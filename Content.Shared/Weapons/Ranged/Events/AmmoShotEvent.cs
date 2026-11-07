using Robust.Shared.Map; // CMU14

namespace Content.Shared.Weapons.Ranged.Events;

/// <summary>
/// Raised on a gun when projectiles have been fired from it.
/// </summary>
public sealed class AmmoShotEvent : EntityEventArgs
{
    public List<EntityUid> FiredProjectiles = default!;

    /// <summary>CMU14: the final target after mounted-weapon and cross-level adjustments.</summary>
    public EntityCoordinates? TargetCoordinates;
}
