using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared.Physics;
using Content.Shared.Projectiles;
using Robust.Shared.Physics; // CMU14
using Robust.Shared.Physics.Events;

namespace Content.Shared._RMC14.Projectiles.Penetration;

public sealed partial class RMCPenetratingProjectileSystem : EntitySystem
{
    private const int HardCollisionGroup = (int) (CollisionGroup.HighImpassable | CollisionGroup.Impassable);

    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private RMCSizeStunSystem _rmcSize = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RMCPenetratingProjectileComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<RMCPenetratingProjectileComponent, PreventCollideEvent>(OnPreventCollide);
        // SubscribeLocalEvent<RMCPenetratingProjectileComponent, StartCollideEvent>(OnStartCollide, after: [typeof(SharedProjectileSystem)]); // CMU14: both hit transports use ProjectileHitAcceptedEvent.
        SubscribeLocalEvent<RMCPenetratingProjectileComponent, ProjectileHitEvent>(OnProjectileHit);
        SubscribeLocalEvent<RMCPenetratingProjectileComponent, ProjectileHitAcceptedEvent>(OnHitAccepted); // CMU14
        SubscribeLocalEvent<RMCPenetratingProjectileComponent, AfterProjectileHitEvent>(OnAllowAdditionalHits);
    }

    /// <summary>
    ///     Store the coordinates the projectile was shot from.
    /// </summary>
    private void OnMapInit(Entity<RMCPenetratingProjectileComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.ShotFrom = _transform.GetMoverCoordinates(ent);
        Dirty(ent);
    }

    /// <summary>
    ///     Prevent collision with an already hit entity.
    /// </summary>
    private void OnPreventCollide(Entity<RMCPenetratingProjectileComponent> ent, ref PreventCollideEvent args)
    {
        if(!ent.Comp.HitTargetIds.Contains(GetNetEntity(args.OtherEntity).Id))
            return;

        args.Cancelled = true;
    }

    /// <summary>
    ///     Add the hit target to a list of hit targets that won't be hit another time.
    /// </summary>
    private void OnProjectileHit(Entity<RMCPenetratingProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        var netId = GetNetEntity(args.Target).Id;
        if (ent.Comp.HitTargetIds.Contains(netId))
        {
            args.Handled = true;
            return;
        }

        ent.Comp.HitTargetIds.Add(netId);
        Dirty(ent);
    }

    // CMU14 method: accepted physical and reported hits use the same target material.
    private void OnHitAccepted(Entity<RMCPenetratingProjectileComponent> ent, ref ProjectileHitAcceptedEvent args)
    {
        var target = args.Target;
        var projectile = args.Projectile.Comp;
        var rangeLoss = ent.Comp.RangeLossPerHit;
        var damageLoss = ent.Comp.DamageMultiplierLossPerHit;
        _rmcSize.TryGetSize(target, out var size);

        var hardTarget = false;
        if (TryComp(target, out FixturesComponent? fixtures) &&
            TryComp(ent, out FixturesComponent? projectileFixtures))
        {
            foreach (var fixture in fixtures.Fixtures.Values)
            {
                if (!fixture.Hard || (fixture.CollisionLayer & HardCollisionGroup) == 0)
                    continue;

                foreach (var projectileFixture in projectileFixtures.Fixtures.Values)
                {
                    if ((projectileFixture.CollisionMask & fixture.CollisionLayer) == 0 &&
                        (fixture.CollisionMask & projectileFixture.CollisionLayer) == 0)
                        continue;

                    hardTarget = true;
                    break;
                }

                if (hardTarget)
                    break;
            }
        }

        // Apply damage and range loss multipliers depending on target hit.
        if (hardTarget)
        {
            // Thick Membranes have a lower multiplier.
            if (TryComp(target, out OccluderComponent? occluder) &&
                !occluder.Enabled)
            {
                rangeLoss *= ent.Comp.ThickMembraneMultiplier;
                damageLoss *=  ent.Comp.ThickMembraneMultiplier;

                // Normal membranes have an even lower multiplier.
                if (HasComp<XenoStructureUpgradeableComponent>(target))
                {
                    rangeLoss *= ent.Comp.MembraneMultiplier;
                    damageLoss *=  ent.Comp.MembraneMultiplier;
                }
            }
            else
            {
                rangeLoss *= ent.Comp.WallMultiplier;
                damageLoss *=  ent.Comp.WallMultiplier;
            }
        }
        else if(size >= RMCSizes.Big)
        {
            rangeLoss *= ent.Comp.BigXenoMultiplier;
            damageLoss *=  ent.Comp.BigXenoMultiplier;
        }

        ent.Comp.Range -= rangeLoss;
        Dirty(ent);

        projectile.Damage *= 1 - damageLoss;
        Dirty(ent,projectile);
    }

    /// <summary>
    ///     Make sure additional hits are allowed if range is still above 0.
    /// </summary>
    private void OnAllowAdditionalHits(Entity<RMCPenetratingProjectileComponent> ent, ref AfterProjectileHitEvent args)
    {
        if(ent.Comp.ShotFrom == null)
            return;

        // CMU14: accepted hits already consumed their range before target destruction.
        if (!ent.Comp.ShotFrom.Value.TryDistance(EntityManager, _transform, _transform.GetMoverCoordinates(ent), out var distanceTravelled))
            return;
        // CMU14 end
        var range = ent.Comp.Range - distanceTravelled;

        if (range < 0)
            return;

        args.Projectile.Comp.ProjectileSpent = false;
        Dirty(args.Projectile);
    }
}

/// <summary>
///     Raised on a projectile after it has hit an entity.
/// </summary>
[ByRefEvent]
public record struct AfterProjectileHitEvent(Entity<ProjectileComponent> Projectile, EntityUid Target);

// CMU14: raised once, after veto/deduplication and before target damage.
[ByRefEvent]
public readonly record struct ProjectileHitAcceptedEvent(Entity<ProjectileComponent> Projectile, EntityUid Target);
