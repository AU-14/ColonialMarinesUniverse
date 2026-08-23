using Content.Server.Explosion.Components;
using Content.Shared._RMC14.Explosion;

namespace Content.Server._RMC14.Explosion;

public sealed partial class CMClusterGrenadeHolderSystem : EntitySystem // CMU14 Class
{
    private EntityQuery<UserLimitHitsComponent> _userLimits = default!;

    public override void Initialize()
    {
        _userLimits = GetEntityQuery<UserLimitHitsComponent>();

        SubscribeLocalEvent<ProjectileGrenadeComponent, CMClusterSpawnedEvent>(OnClusterSpawnedIgnoreHolder); // CMU14
    }

    private void OnClusterSpawnedIgnoreHolder(Entity<ProjectileGrenadeComponent> ent, ref CMClusterSpawnedEvent args)
    {
        if (HasComp<ClusterLimitHitsComponent>(ent))
            return;

        var parent = Transform(ent).ParentUid;
        while (parent.IsValid())
        {
            if (!_userLimits.HasComp(parent))
            {
                parent = Transform(parent).ParentUid;
                continue;
            }

            foreach (var spawned in args.Spawned)
            {
                var projectile = EnsureComp<ProjectileLimitHitsComponent>(spawned);
                projectile.Limit = 0;
                projectile.IgnoredEntities.Add(parent);
                projectile.OriginEntityId = args.OriginEntity.Id;
                Dirty(spawned, projectile);
            }

            return;
        }
    }
}
