using Robust.Shared.GameStates;
using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Physics;

namespace Content.Shared._RMC14.Weapons.Ranged.Prediction;

[RegisterComponent]
public sealed partial class PredictedProjectileClientComponent : Component
{
    [DataField]
    public bool Hit;

    [DataField]
    public EntityCoordinates? Coordinates;

    /// <summary>
    /// Targets for which this local copy already produced impact feedback. A
    /// penetrating projectile can remain in the same contact for several frames;
    /// those frames must not become repeated hit reports or hit markers.
    /// </summary>
    public HashSet<NetEntity> HitTargets = new();

    /// <summary>
    /// Motion to restore after the server confirms that a vanilla penetration hit
    /// did not spend the projectile. Only one such hit is allowed in flight.
    /// </summary>
    public Vector2? PendingPenetrationVelocity;

    public BodyType? PendingPenetrationBodyType;
}
