using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Weapons.Ranged.Prediction;

[Serializable, NetSerializable]
public sealed class PredictedProjectileHitEvent(int projectile, List<(NetEntity Id, MapCoordinates Coordinates)> hit) : EntityEventArgs
{
    public readonly int Projectile = projectile;
    public readonly List<(NetEntity Id, MapCoordinates Coordinates)> Hit = hit;
}
