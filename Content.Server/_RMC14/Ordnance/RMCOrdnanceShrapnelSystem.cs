using System.Numerics;
using Content.Server.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._RMC14.Ordnance;

/// <summary>
///     Spawns live shrapnel projectiles for custom ordnance detonations using the normal projectile shooting path.
///     Keeping this separate from <see cref="RMCChembombSystem"/> makes the detonation flow easier to read and tune.
/// </summary>
public sealed class RMCOrdnanceShrapnelSystem : EntitySystem
{
    [Dependency] private readonly GunSystem _gun = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    /// <summary>
    ///     Fires a burst of shrapnel projectiles from the provided origin.
    ///     The burst can be a full circle or an oriented cone depending on the supplied spread settings.
    /// </summary>
    public void SpawnBurst(
        MapCoordinates origin,
        EntProtoId projectileProto,
        int count,
        EntityUid source,
        Angle centerAngle,
        float spreadAngle = 360f,
        float projectileSpeed = 20f)
    {
        if (count <= 0)
            return;

        spreadAngle = Math.Clamp(spreadAngle, 0f, 360f);
        if (spreadAngle <= 0f)
            spreadAngle = 1f;

        var halfSpread = spreadAngle / 2f;
        var segmentAngle = spreadAngle / count;
        for (var index = 0; index < count; index++)
        {
            var minAngle = -halfSpread + (segmentAngle * index);
            var maxAngle = minAngle + segmentAngle;
            var angle = centerAngle + Angle.FromDegrees(_random.NextFloat(minAngle, maxAngle));
            var direction = angle.ToVec().Normalized();

            var projectile = Spawn(projectileProto, origin);
            _gun.ShootProjectile(projectile, direction, Vector2.Zero, source, source, projectileSpeed);
        }
    }
}
