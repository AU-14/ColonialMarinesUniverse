using Content.Server._RMC14.Explosion;
using Content.Server.Explosion.Components;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._RMC14.Explosion;
using Content.Shared.Projectiles;
using Content.Shared.Trigger;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.Explosion.EntitySystems;

public sealed partial class ProjectileGrenadeSystem : EntitySystem
{
    private readonly List<EntityUid> _spawned = new();

    [Dependency] private GunSystem _gun = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private TransformSystem _transformSystem = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ProjectileGrenadeComponent, ComponentInit>(OnFragInit);
        SubscribeLocalEvent<ProjectileGrenadeComponent, ComponentStartup>(OnFragStartup);
        SubscribeLocalEvent<ProjectileGrenadeComponent, TriggerEvent>(OnFragTrigger);
    }

    private void OnFragInit(Entity<ProjectileGrenadeComponent> entity, ref ComponentInit args)
    {
        entity.Comp.Container = _container.EnsureContainer<Container>(entity.Owner, "cluster-payload");
    }

    /// <summary>
    /// Setting the unspawned count based on capacity so we know how many new entities to spawn
    /// </summary>
    private void OnFragStartup(Entity<ProjectileGrenadeComponent> entity, ref ComponentStartup args)
    {
        if (entity.Comp.FillPrototype == null)
            return;

        entity.Comp.UnspawnedCount = Math.Max(0, entity.Comp.Capacity - entity.Comp.Container.ContainedEntities.Count);
    }

    /// <summary>
    /// Can be triggered either by damage or the use in hand timer
    /// </summary>
    private void OnFragTrigger(Entity<ProjectileGrenadeComponent> entity, ref TriggerEvent args)
    {
        // RMC14 - a null trigger key intentionally activates every effect on the airburst projectile.
        if (args.Key != null && args.Key != entity.Comp.TriggerKey)
            return;

        FragmentIntoProjectiles(entity.Owner, entity.Comp);
        args.Handled = true;
    }

    /// <summary>
    /// Spawns projectiles at the coordinates of the grenade upon triggering
    /// Can customize the angle and velocity the projectiles come out at
    /// </summary>
    private void FragmentIntoProjectiles(EntityUid uid, ProjectileGrenadeComponent component)
    {
        var grenadeCoord = _transformSystem.GetMapCoordinates(uid);
        var shootCount = 0;
        var totalCount = component.Container.ContainedEntities.Count + component.UnspawnedCount;

        // RMC14 - direct hits can consume the payload before it is fired.
        if (totalCount <= 0)
            return;

        var hitEntities = new List<EntityUid>();
        var segmentAngle = 360 / totalCount;

        _spawned.Clear();
        while (TrySpawnContents(grenadeCoord, component, out var contentUid))
        {
            Angle angle;
            if (component.RandomAngle)
                angle = _random.NextAngle();
            else
            {
                var angleMin = segmentAngle * shootCount;
                var angleMax = segmentAngle * (shootCount + 1);
                angle = Angle.FromDegrees(_random.Next(angleMin, angleMax));

                // RMC14 - apply directional spread, IFF, and direct-hit behavior.
                var ev = new FragmentIntoProjectilesEvent(contentUid, totalCount, angle, shootCount, hitEntities);
                RaiseLocalEvent(uid, ref ev);

                if (ev.TotalCount <= 0)
                    return;

                if (ev.Handled)
                {
                    hitEntities = ev.HitEntities;
                    angle = ev.Angle;
                }

                shootCount++;
            }

            EntityUid? gunUid = null;
            EntityUid? user = null;

            // RMC14 - preserve attribution from the airburst projectile.
            if (TryComp(uid, out ProjectileComponent? clusterProjectile))
            {
                gunUid = clusterProjectile.Weapon;
                user = clusterProjectile.Shooter;
            }

            // velocity is randomized to make the projectiles look
            // slightly uneven, doesn't really change much, but it looks better
            var direction = angle.ToVec().Normalized();
            var velocity = _random.NextVector2(component.MinVelocity, component.MaxVelocity);
            _gun.ShootProjectile(contentUid, direction, velocity, gunUid, user, component.ProjectileSpeed);
            _spawned.Add(contentUid);
        }

        // RMC14 - initialize hit limits and payload timers, including star shell bursts.
        var clusterEv = new CMClusterSpawnedEvent(_spawned, hitEntities, uid);
        RaiseLocalEvent(uid, ref clusterEv);
        RaiseLocalEvent(uid, new AmmoShotEvent
        {
            FiredProjectiles = _spawned,
        });

        QueueDel(uid);
    }

    /// <summary>
    /// Spawns one instance of the fill prototype or contained entity at the coordinate indicated
    /// </summary>
    private bool TrySpawnContents(MapCoordinates spawnCoordinates, ProjectileGrenadeComponent component, out EntityUid contentUid)
    {
        contentUid = default;

        if (component.UnspawnedCount > 0)
        {
            component.UnspawnedCount--;
            contentUid = Spawn(component.FillPrototype, spawnCoordinates);
            return true;
        }

        if (component.Container.ContainedEntities.Count > 0)
        {
            contentUid = component.Container.ContainedEntities[0];

            if (!_container.Remove(contentUid, component.Container))
                return false;

            return true;
        }

        return false;
    }
}
