using System.Numerics;
using Content.Shared._RMC14.Vents;
using Content.Shared._RMC14.Xenonids.Burrow;
using Content.Shared.CMU14.Fob;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Content.Shared.Trigger.Systems;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Fob;

/// <summary>Sensor-guided rockets retain one target and commit to its last position when tracking is lost.</summary>
public sealed partial class CMUFobMissileSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private CMUFobEquipmentSystem _equipment = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedProjectileSystem _projectiles = default!;
    [Dependency] private CMUFobReserveSystem _reserve = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TriggerSystem _trigger = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUFobSupportGunComponent, AmmoShotEvent>(OnShot);
        InitializeDamage();
    }

    private void OnShot(Entity<CMUFobSupportGunComponent> gun, ref AmmoShotEvent args)
    {
        if (!_reserve.Enabled || !gun.Comp.RequiresSensor || gun.Comp.Target is not { } target
            || gun.Comp.Mount is not { } mount || !TryComp<CMUFobGunMountComponent>(mount, out var platform)
            || platform.Network is not { } network || !_equipment.TryGetSpottingSensor(network, target, out var sensor))
            return;
        foreach (var projectile in args.FiredProjectiles)
        {
            if (!TryComp<CMUFobMissileComponent>(projectile, out var missile)) continue;
            missile.Target = target;
            missile.Sensor = sensor;
            missile.Network = network;
            missile.LastPosition = _transform.GetMapCoordinates(target);
            missile.ExpiresAt = _timing.CurTime + missile.FlightLifetime;
        }
    }

    private bool CanTrack(Entity<CMUFobMissileComponent> missile, EntityUid target)
        => !TerminatingOrDeleted(target) && !MetaData(target).EntityPaused
           && Transform(target).MapID == missile.Comp.LastPosition.MapId
           && TryComp<MobStateComponent>(target, out var state) && state.CurrentState != MobState.Dead
           && !_containers.IsEntityOrParentInContainer(target) && !HasComp<VentCrawlingComponent>(target)
           && !(TryComp<XenoBurrowComponent>(target, out var burrow) && burrow.Active)
           && _equipment.CanTrack(missile.Comp.Network, missile.Comp.Sensor, target, missile.Comp.TrackingRange);

    public override void Update(float frameTime)
    {
        if (!_reserve.Enabled) return;
        var query = EntityQueryEnumerator<CMUFobMissileComponent, ProjectileComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var missile, out var projectile, out var physics))
        {
            if (missile.Target == null || projectile.ProjectileSpent || TerminatingOrDeleted(uid)
                || EntityManager.IsQueuedForDeletion(uid) || MetaData(uid).EntityPaused) continue;
            var position = _transform.GetMapCoordinates(uid);
            if (position.MapId != missile.LastPosition.MapId)
            {
                QueueDel(uid);
                continue;
            }
            if (!missile.TrackingLost)
            {
                if (CanTrack((uid, missile), missile.Target.Value))
                    missile.LastPosition = _transform.GetMapCoordinates(missile.Target.Value);
                else
                    missile.TrackingLost = true;
            }

            var delta = missile.LastPosition.Position - position.Position;
            var distance = delta.Length();
            var onTarget = !missile.TrackingLost && _lookup.GetWorldAABB(missile.Target.Value)
                .Enlarged(missile.ImpactRadius).Contains(position.Position);
            if (onTarget || distance <= missile.ImpactRadius || _timing.CurTime >= missile.ExpiresAt)
            {
                if (onTarget)
                    _projectiles.ProjectileCollide((uid, projectile, physics), missile.Target.Value);
                projectile.ProjectileSpent = true;
                _trigger.Trigger(uid, projectile.Shooter);
                if (!TerminatingOrDeleted(uid)) QueueDel(uid);
                continue;
            }

            // Slow for the final step instead of overshooting the remembered point between physics ticks.
            var velocity = delta / distance * Math.Min(missile.Speed, distance / Math.Max(frameTime, 0.001f));
            _physics.SetLinearVelocity(uid, physics.LinearVelocity + velocity - _physics.GetMapLinearVelocity(uid, physics), body: physics);
            _transform.SetWorldRotationNoLerp(uid, delta.ToWorldAngle() + projectile.Angle);
        }
    }
}
