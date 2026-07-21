using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Weapons.Ranged.Events;

/// <summary>
/// Raised on the client to indicate it'd like to shoot.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestShootEvent : EntityEventArgs
{
    /// <summary>
    /// The gun shooting.
    /// </summary>
    public NetEntity Gun;

    /// <summary>
    /// The location the player is shooting at.
    /// </summary>
    public NetCoordinates Coordinates;

    /// <summary>
    /// The target the player is shooting at, if any.
    /// </summary>
    public NetEntity? Target;

    /// <summary>
    /// If the client wants to continuously shoot.
    /// If true, the gun will continue firing until a stop event is sent from the client.
    /// </summary>
    public bool Continuous;

    /// <summary>
    /// Client-side projectile entity IDs created while predicting this shot.
    /// </summary>
    public List<int>? Shot;

    /// <summary>
    /// Last authoritative server tick applied by the requesting client.
    /// </summary>
    public GameTick LastRealTick;
}

/// <summary>
/// Tells a client that predicted projectile copies have no authoritative counterpart.
/// </summary>
[Serializable, NetSerializable]
public sealed class PredictedProjectileCleanupEvent : EntityEventArgs
{
    public List<int> Projectiles = [];
}
