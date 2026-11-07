using Content.Shared.CMU14.Threats.Mobs.Threat;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Threats.Mobs.Threat;

public sealed partial class CMUThreatSystem
{
    private void OnSpitterAction(Entity<CMUSpitterThreatComponent> ent, ref CMUSpitterAcidActionEvent args)
    {
        if (args.Handled || !TryStartSpitterAcid(ent, args.Target, ent.Comp))
            return;
        args.Handled = true;
    }

    private bool TryStartSpitterAcid(EntityUid uid, EntityCoordinates target, CMUSpitterThreatComponent spitter)
    {
        if (_timing.CurTime < spitter.NextUseAt)
            return false;

        var origin = _transform.GetMapCoordinates(uid);
        var destination = _transform.ToMapCoordinates(target);
        var distance = System.Numerics.Vector2.Distance(origin.Position, destination.Position);
        if (origin.MapId != destination.MapId ||
            distance < 0.1f || distance > spitter.Range ||
            spitter.ProjectileSpeed <= 0f)
            return false;

        spitter.NextUseAt = _timing.CurTime + spitter.Cooldown;
        PlayAbilitySound(uid, spitter.Sound);
        var projectile = Spawn(spitter.ProjectilePrototype, Transform(uid).Coordinates);
        var spit = EnsureComp<CMUThreatSpitProjectileComponent>(projectile);
        spit.Source = uid;
        spit.Destination = destination;
        spit.Duration = TimeSpan.FromSeconds(distance / spitter.ProjectileSpeed);
        spit.EndsAt = _timing.CurTime + spit.Duration;
        spit.AcidPoolPrototype = spitter.AcidPoolPrototype;
        spit.AcidRadius = spitter.AcidRadius;
        spit.AcidDuration = spitter.AcidDuration;
        spit.AcidDamage = spitter.AcidDamage;
        spit.AcidDamageInterval = spitter.AcidDamageInterval;
        Dirty(projectile, spit);
        _gun.ShootProjectile(projectile, destination.Position - origin.Position, System.Numerics.Vector2.Zero,
            uid, uid, speed: spitter.ProjectileSpeed);
        return true;
    }

    private void OnSpitProjectileHit(Entity<CMUThreatSpitProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        if (args.Handled || ent.Comp.Impacted)
            return;

        args.Handled = true;
        ent.Comp.Impacted = true;
        SpawnAcidPool(ent.Comp, _transform.GetMapCoordinates(ent));
        QueueDel(ent);
    }

    private void UpdateSpitProjectiles()
    {
        var query = EntityQueryEnumerator<CMUThreatSpitProjectileComponent>();
        while (query.MoveNext(out var uid, out var spit))
        {
            if (spit.Impacted || _timing.CurTime < spit.EndsAt)
                continue;

            spit.Impacted = true;
            SpawnAcidPool(spit, spit.Destination);
            QueueDel(uid);
        }
    }

    private void SpawnAcidPool(CMUThreatSpitProjectileComponent spit, MapCoordinates coordinates)
    {
        if (coordinates.MapId != spit.Destination.MapId)
            return;

        if (!_map.TryFindGridAt(coordinates, out var gridUid, out var grid))
            return;

        var center = _map.TileIndicesFor(gridUid, grid, coordinates);
        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                var tile = center + new Robust.Shared.Maths.Vector2i(x, y);
                if (_map.GetTileRef(gridUid, grid, tile).Tile.IsEmpty)
                    continue;

                SpawnAcidPoolTile(spit, _map.GridTileToLocal(gridUid, grid, tile));
            }
        }
    }

    private void SpawnAcidPoolTile(CMUThreatSpitProjectileComponent spit, EntityCoordinates coordinates)
    {
        var poolUid = Spawn(spit.AcidPoolPrototype, coordinates);
        var pool = EnsureComp<CMUThreatAcidPoolComponent>(poolUid);
        pool.Source = spit.Source;
        pool.Radius = spit.AcidRadius;
        pool.Damage = spit.AcidDamage;
        pool.DamageInterval = spit.AcidDamageInterval;
        pool.ExpiresAt = _timing.CurTime + spit.AcidDuration;
        pool.NextDamageAt = _timing.CurTime + spit.AcidDamageInterval;
    }

    private void UpdateAcidPools()
    {
        var query = EntityQueryEnumerator<CMUThreatAcidPoolComponent>();
        while (query.MoveNext(out var uid, out var pool))
        {
            if (_timing.CurTime >= pool.ExpiresAt)
            {
                QueueDel(uid);
                continue;
            }

            if (_timing.CurTime < pool.NextDamageAt)
                continue;

            pool.NextDamageAt = _timing.CurTime + pool.DamageInterval;
            var origin = _transform.GetMapCoordinates(uid);
            foreach (var victim in _lookup.GetEntitiesInRange<MobStateComponent>(origin, pool.Radius))
            {
                if (!IsValidVictim(uid, victim))
                    continue;

                var source = pool.Source is { } attacker && Exists(attacker) ? attacker : (EntityUid?)null;
                _damage.TryChangeDamage(victim, pool.Damage, origin: source);
            }
        }
    }
}
