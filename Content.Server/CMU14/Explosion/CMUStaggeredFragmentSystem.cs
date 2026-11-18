using Content.Server.Explosion.Components;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.CMU14.Explosion;
using Content.Shared.Containers;
using Content.Shared.Projectiles;
using Content.Shared.Trigger;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Explosion;

public sealed class CMUStaggeredFragmentSystem : EntitySystem
{
    [Dependency] private GunSystem _gun = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private TransformSystem _transformSystem = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private GunIFFSystem _gunIFF = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    private readonly List<EntityUid> _spawned = new();
    private readonly List<(EntityUid Uid, float NextTime)> _fragmentingGrenades = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUStaggeredFragmentComponent, TriggerEvent>(OnStaggeredTrigger);
    }

    private void OnStaggeredTrigger(Entity<CMUStaggeredFragmentComponent> ent, ref TriggerEvent args)
    {
        if (!TryComp(ent, out ProjectileGrenadeComponent? grenadeComp))
            return;

        if (args.Key != null && args.Key != grenadeComp.TriggerKey)
            return;

        // Initialize staggered fragment data
        var totalCount = grenadeComp.Container.ContainedEntities.Count + grenadeComp.UnspawnedCount;
        if (totalCount <= 0)
        {
            QueueDel(ent);
            return;
        }

        ent.Comp.TotalFragments = totalCount;
        ent.Comp.FragmentsSpawned = 0;
        ent.Comp.NextFragmentTime = (float)_timing.CurTime.TotalSeconds;

        _fragmentingGrenades.Add((ent.Owner, ent.Comp.NextFragmentTime));
        args.Handled = true;
    }

    public override void Update(float frameTime)
    {
        var curTime = (float)_timing.CurTime.TotalSeconds;

        for (var i = _fragmentingGrenades.Count - 1; i >= 0; i--)
        {
            var (grenadeUid, _) = _fragmentingGrenades[i];

            if (!TryComp(grenadeUid, out CMUStaggeredFragmentComponent? staggeredComp) ||
                !TryComp(grenadeUid, out ProjectileGrenadeComponent? grenadeComp))
            {
                _fragmentingGrenades.RemoveSwap(i);
                continue;
            }

            if (curTime < staggeredComp.NextFragmentTime)
                continue;

            // Spawn next fragment
            if (!TrySpawnNextFragment(grenadeUid, grenadeComp, staggeredComp, curTime))
            {
                _fragmentingGrenades.RemoveSwap(i);
                QueueDel(grenadeUid);
            }
            else
            {
                staggeredComp.NextFragmentTime = curTime + staggeredComp.DelayBetweenFragments;
                staggeredComp.FragmentsSpawned++;
            }
        }
    }

    private bool TrySpawnNextFragment(EntityUid grenadeUid, ProjectileGrenadeComponent grenadeComp, 
        CMUStaggeredFragmentComponent staggeredComp, float curTime)
    {
        var grenadeCoord = _transformSystem.GetMapCoordinates(grenadeUid);
        EntityUid? contentUid = null;

        // Spawn one projectile
        if (grenadeComp.UnspawnedCount > 0)
        {
            grenadeComp.UnspawnedCount--;
            contentUid = Spawn(grenadeComp.FillPrototype, grenadeCoord);
        }
        else if (grenadeComp.Container.ContainedEntities.Count > 0)
        {
            contentUid = grenadeComp.Container.ContainedEntities[0];
            if (!_container.Remove(contentUid.Value, grenadeComp.Container))
                return false;
        }

        if (contentUid == null)
            return staggeredComp.FragmentsSpawned >= staggeredComp.TotalFragments;

        // Calculate angle for triangle pattern
        Angle angle;
        if (staggeredComp.UseTrianglePattern && staggeredComp.TotalFragments == 3)
        {
            // Triangle pattern: left (-spread), center (0), right (+spread)
            angle = staggeredComp.FragmentsSpawned switch
            {
                0 => Angle.FromDegrees(-staggeredComp.TriangleSpreadAngle / 2),
                1 => Angle.FromDegrees(0),
                2 => Angle.FromDegrees(staggeredComp.TriangleSpreadAngle / 2),
                _ => Angle.FromDegrees(0)
            };
        }
        else
        {
            // Standard random spread if not triangle pattern or wrong count
            var segmentAngle = 360f / staggeredComp.TotalFragments;
            var angleMin = segmentAngle * staggeredComp.FragmentsSpawned;
            var angleMax = segmentAngle * (staggeredComp.FragmentsSpawned + 1);
            angle = Angle.FromDegrees(_random.Next((int)angleMin, (int)angleMax));
        }

        // Handle RMC14 projectile grenade customization event
        var hitEntities = new List<EntityUid>();
        var ev = new FragmentIntoProjectilesEvent(contentUid.Value, staggeredComp.TotalFragments, angle, 
            staggeredComp.FragmentsSpawned, hitEntities);
        RaiseLocalEvent(grenadeUid, ref ev);

        if (ev.Handled)
        {
            angle = ev.Angle;
        }

        // Inherit IFF if configured
        if (grenadeComp is { InheritIFF: true } &&
            TryComp(grenadeUid, out ProjectileIFFComponent? grenadeIFFComponent))
        {
            _gunIFF.GiveAmmoMultiFactionIFF(contentUid.Value, grenadeIFFComponent.Factions, grenadeIFFComponent.Enabled);
        }

        // Shoot the projectile
        var direction = angle.ToVec().Normalized();
        var velocity = _random.NextVector2(grenadeComp.MinVelocity, grenadeComp.MaxVelocity);
        
        EntityUid? gunUid = null;
        EntityUid? user = null;
        if (TryComp(grenadeUid, out ProjectileComponent? clusterProjectile))
        {
            gunUid = clusterProjectile.Weapon;
            user = clusterProjectile.Shooter;
        }

        _gun.ShootProjectile(contentUid.Value, direction, velocity, gunUid, user, grenadeComp.ProjectileSpeed);
        _spawned.Add(contentUid.Value);

        // All fragments spawned?
        if (staggeredComp.FragmentsSpawned >= staggeredComp.TotalFragments - 1)
        {
            var clusterEv = new CMClusterSpawnedEvent(_spawned, hitEntities, grenadeUid);
            RaiseLocalEvent(grenadeUid, ref clusterEv);
            _spawned.Clear();
            return false;
        }

        return true;
    }
}
