using System.Linq;
using System.Numerics;
using Content.Server.Atmos.Components;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Lungs;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.ShipWeapons;
using Content.Shared.CMU14.ZLevels.Core;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Light.Components;
using Content.Shared.Explosion;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;

namespace Content.Server.CMU14.ShipWeapons;

public sealed partial class CMUShipWeaponsSystem
{
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private RMCSlowSystem _slow = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private ITileDefinitionManager _cloudTileDefinitions = default!;
    private readonly HashSet<Entity<DamageableComponent>> _railTargets = new();
    private readonly HashSet<Entity<IsRoofComponent>> _cloudRoofs = new();
    private readonly List<(EntityUid Field, Vector2i Tile)> _verticalCloudSpread = new();
    public static readonly EntProtoId EmsStun = "CMUShipWeaponEmsStun";
    private static readonly ProtoId<ExplosionPrototype> RailExplosion = "RMCOBXenoTunnel";
    public static readonly EntProtoId GasHaze = "CMUShipWeaponGasHaze";
    private static readonly Vector2i[] CloudSpreadDirections =
        [new(1, 0), new(-1, 0), new(0, 1), new(0, -1)];
    private static readonly int[] CloudLevelOffsets = [-1, 1];

    private void InitializeEffects()
    {
        SubscribeLocalEvent<MobStateComponent, MoveEvent>(OnEmsMobMoved);
        SubscribeLocalEvent<CMUOrbitalGasComponent, CMUOrbitalGasExposureEvent>(OnGasExposure);
    }

    private void OnGasExposure(Entity<CMUOrbitalGasComponent> ent, ref CMUOrbitalGasExposureEvent args)
    {
        var victim = args.Victim;
        if (HasComp<XenoComponent>(victim))
        {
            _slow.TrySlowdown(victim, TimeSpan.FromSeconds(1.5), ignoreDurationModifier: true);
            if (!_status.TryUpdateStatusEffectDuration(victim, GasHaze, TimeSpan.FromSeconds(1.5), force: true))
                Log.Warning($"Could not apply orbital gas haze to {ToPrettyString(victim)}.");
            return;
        }

        var exposure = EnsureComp<CMUOrbitalGasVictimComponent>(victim);
        var now = _timing.CurTime;
        if (exposure.NextOrganDamage == TimeSpan.Zero || now - exposure.LastExposure > TimeSpan.FromSeconds(1.5))
            exposure.NextOrganDamage = now + TimeSpan.FromSeconds(3);
        exposure.LastExposure = now;
        if (now < exposure.NextOrganDamage)
            return;

        exposure.NextOrganDamage = now + TimeSpan.FromSeconds(3);
        var damage = new DamageSpecifier { DamageDict = { ["Poison"] = FixedPoint2.New(1) } };
        foreach (var organ in _medicalIndex.GetOrgans(victim))
        {
            if (!HasComp<LungsComponent>(organ.Owner) && !HasComp<CMUBrainComponent>(organ.Owner))
                continue;
            var damaged = new OrganDamagedEvent(victim, organ.Owner, damage, OrganDamageSource.Reagent);
            RaiseLocalEvent(organ.Owner, ref damaged);
        }
    }

    private void OnEmsMobMoved(Entity<MobStateComponent> ent, ref MoveEvent args)
    {
        var fields = EntityQueryEnumerator<CMUShipWeaponEmsFieldComponent>();
        while (fields.MoveNext(out var uid, out var field))
        {
            if (_timing.CurTime < field.EndsAt)
                ApplyEms(ent, uid, field);
        }
    }

    private void UpdateCloudSpread()
    {
        _verticalCloudSpread.Clear();
        var fields = EntityQueryEnumerator<CMUShipWeaponCloudSpreadComponent>();
        while (fields.MoveNext(out var uid, out var field))
        {
            if (_timing.CurTime >= field.EndsAt)
            {
                QueueDel(uid);
                continue;
            }

            var profile = _prototypes.Index(field.Weapon);
            if (_timing.CurTime < field.NextSpread ||
                profile.CloudMaxRadius <= profile.CloudRadius)
                continue;

            var center = _transform.GetMapCoordinates(uid);
            foreach (var tile in field.CoveredTiles)
                QueueVerticalCloudSpread(center, tile, field);

            var nextFrontier = new HashSet<Vector2i>();
            foreach (var tile in field.Frontier)
            {
                if (!IsCloudTileOpen(center.Offset(new Vector2(tile.X, tile.Y))))
                {
                    nextFrontier.Add(tile);
                    continue;
                }

                foreach (var direction in CloudSpreadDirections)
                {
                    var neighbor = tile + direction;
                    if (neighbor.X * neighbor.X + neighbor.Y * neighbor.Y >
                        profile.CloudMaxRadius * profile.CloudMaxRadius ||
                        field.CoveredTiles.Contains(neighbor))
                        continue;

                    if (TrySpawnCloudTile(profile, center, neighbor, field))
                        nextFrontier.Add(neighbor);
                    else
                        nextFrontier.Add(tile);
                }
            }

            field.Frontier = nextFrontier;
            field.NextSpread = _timing.CurTime + TimeSpan.FromSeconds(1);
        }

        // Apply vertical arrivals after all fronts advance so entity iteration order cannot cascade through decks.
        foreach (var (uid, tile) in _verticalCloudSpread)
        {
            if (!TryComp<CMUShipWeaponCloudSpreadComponent>(uid, out var field) ||
                _timing.CurTime >= field.EndsAt || field.CoveredTiles.Contains(tile))
                continue;

            if (TrySpawnCloudTile(_prototypes.Index(field.Weapon), _transform.GetMapCoordinates(uid), tile, field))
                field.Frontier.Add(tile);
        }
    }

    private void QueueVerticalCloudSpread(MapCoordinates center, Vector2i tile,
        CMUShipWeaponCloudSpreadComponent field)
    {
        var cell = center.Offset(new Vector2(tile.X, tile.Y));
        var map = _map.GetMap(center.MapId);
        foreach (var offset in CloudLevelOffsets)
        {
            if (!_zLevels.TryMapOffset(map, offset, out _, out var otherMap) ||
                !field.LevelFields.TryGetValue(otherMap.MapId, out var target) ||
                !TryComp<CMUShipWeaponCloudSpreadComponent>(target, out var targetField) ||
                targetField.CoveredTiles.Contains(tile) ||
                !IsCloudTileOpen(cell))
                continue;

            var other = new MapCoordinates(cell.Position, otherMap.MapId);
            var upper = offset > 0 ? other : cell;
            var lower = offset > 0 ? cell : other;
            if (!IsCloudFloorOpen(upper) || !IsCloudTileOpen(other))
                continue;

            _cloudRoofs.Clear();
            _lookup.GetEntitiesInRange(lower, 0.1f, _cloudRoofs);
            if (_cloudRoofs.Any(roof => roof.Comp.Enabled))
                continue;

            _verticalCloudSpread.Add((target, tile));
        }
    }

    private bool IsCloudFloorOpen(MapCoordinates cell)
    {
        if (!_map.TryFindGridAt(cell, out var gridUid, out var grid))
            return true;

        var tile = _map.WorldToTile(gridUid, grid, cell.Position);
        // Intact roof tiles can be transparent, but clouds need a physical opening.
        return !_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) ||
               CMUZLevelOpeningCache.IsFallThroughTile(tileRef.Tile, _cloudTileDefinitions);
    }

    private bool TryGetCloudCoordinates(MapCoordinates cell, out EntityCoordinates coordinates)
    {
        if (TryGetGroundCoordinates(cell, out coordinates))
            return true;

        var map = _map.GetMap(cell.MapId);
        if (!_zLevels.TryGetZNetwork(map, out _) || !HasComp<MapGridComponent>(map))
            return false;

        coordinates = _transform.ToCoordinates(map, cell);
        return true;
    }

    private bool IsCloudTileOpen(MapCoordinates cell)
    {
        var anchored = _rmcMap.GetAnchoredEntitiesEnumerator(cell);
        while (anchored.MoveNext(out var entity))
        {
            if (!HasComp<DoorComponent>(entity) &&
                TryComp<AirtightComponent>(entity, out var airtight) && airtight.AirBlocked)
                return false;
        }

        return true;
    }

    private bool TrySpawnCloudTile(CMUShipWeaponPrototype profile, MapCoordinates impact,
        Vector2i tile, CMUShipWeaponCloudSpreadComponent field)
    {
        if (profile.Cloud is not { } cloud)
            return false;

        var cell = impact.Offset(new Vector2(tile.X, tile.Y));
        if (!TryGetCloudCoordinates(cell, out var coordinates) ||
            !_area.CanOrbitalBombard(coordinates, out _) ||
            !IsCloudTileOpen(cell))
            return false;

        ExtendCloudHold(field);
        var spawned = Spawn(cloud, coordinates);
        // Spread clouds share the impact's deadline rather than extending it with each generation.
        Comp<TimedDespawnComponent>(spawned).Lifetime = (float) (field.EndsAt - _timing.CurTime).TotalSeconds;
        field.CoveredTiles.Add(tile);
        field.Clouds.Add(spawned);
        return true;
    }

    private void ExtendCloudHold(CMUShipWeaponCloudSpreadComponent source)
    {
        var minimumEnd = _timing.CurTime + TimeSpan.FromSeconds(7);
        if (source.EndsAt >= minimumEnd)
            return;

        foreach (var uid in source.LevelFields.Values)
        {
            if (!TryComp<CMUShipWeaponCloudSpreadComponent>(uid, out var field))
                continue;

            field.EndsAt = minimumEnd;
            foreach (var cloud in field.Clouds)
            {
                if (TryComp<TimedDespawnComponent>(cloud, out var despawn))
                    despawn.Lifetime = 7f;
            }

            if (!TryComp<CMUShipWeaponEmsFieldComponent>(uid, out var ems))
                continue;

            ems.EndsAt = minimumEnd;
            foreach (var mob in ems.Exposures.Keys)
            {
                if (TerminatingOrDeleted(mob) || !_status.TryGetTime(mob, EmsStun, out var active) ||
                    active.EndEffectTime == null || active.EndEffectTime >= minimumEnd + ems.StunAfterField)
                    continue;

                if (!_status.TryUpdateStatusEffectDuration(mob, EmsStun,
                        minimumEnd + ems.StunAfterField - _timing.CurTime, force: true))
                    Log.Warning($"Could not extend orbital EMS stun on {ToPrettyString(mob)}.");
            }
        }
    }

    private void UpdateEmsFields()
    {
        var fields = EntityQueryEnumerator<CMUShipWeaponEmsFieldComponent>();
        while (fields.MoveNext(out var uid, out var field))
        {
            if (_timing.CurTime >= field.EndsAt)
            {
                QueueDel(uid);
                continue;
            }

            if (_timing.CurTime < field.NextPulse)
                continue;

            field.NextPulse = _timing.CurTime + TimeSpan.FromSeconds(0.25);
            _mobs.Clear();
            _lookup.GetEntitiesInRange(_transform.GetMapCoordinates(uid), field.Radius, _mobs);
            foreach (var mob in _mobs)
                ApplyEms(mob, uid, field);
        }
    }

    private void ApplyEms(EntityUid mob, EntityUid fieldUid, CMUShipWeaponEmsFieldComponent field)
    {
        if (TerminatingOrDeleted(mob) || EntityManager.IsQueuedForDeletion(mob))
            return;

        var center = _transform.GetMapCoordinates(fieldUid);
        var position = _transform.GetMapCoordinates(mob);
        if (position.MapId != center.MapId ||
            Vector2.DistanceSquared(position.Position, center.Position) > field.Radius * field.Radius ||
            !TryGetCloudCoordinates(position, out var coordinates) ||
            !_area.CanOrbitalBombard(coordinates, out _))
            return;

        if (TryComp<CMUShipWeaponCloudSpreadComponent>(fieldUid, out var spread))
        {
            var tile = new Vector2i(
                (int) MathF.Floor(position.X) - (int) MathF.Floor(center.X),
                (int) MathF.Floor(position.Y) - (int) MathF.Floor(center.Y));
            if (!spread.CoveredTiles.Contains(tile) || !IsCloudTileOpen(position))
                return;
        }

        ApplyEmsBurn(mob, fieldUid, field);

        var stunUntil = field.EndsAt + field.StunAfterField;
        if (_status.TryGetStatusEffect(mob, EmsStun, out _) &&
            _status.TryGetTime(mob, EmsStun, out var active) &&
            (active.EndEffectTime == null || active.EndEffectTime >= stunUntil))
            return;

        // EMS has an absolute recovery deadline, unaffected by species or chemical stun multipliers.
        if (!_status.TryUpdateStatusEffectDuration(mob, EmsStun,
                stunUntil - _timing.CurTime, force: true))
            Log.Warning($"Could not apply orbital EMS stun to {ToPrettyString(mob)}.");
    }

    private void ApplyEmsBurn(EntityUid mob, EntityUid fieldUid, CMUShipWeaponEmsFieldComponent field)
    {
        var now = _timing.CurTime;
        if (!field.Exposures.TryGetValue(mob, out var exposure))
        {
            field.Exposures[mob] = (now + TimeSpan.FromSeconds(1), now, 0f);
            return;
        }

        // Preserve the strike's damage budget, but don't charge time spent outside the field.
        if (now - exposure.LastExposure > TimeSpan.FromSeconds(0.5))
            exposure.NextDamage = now + TimeSpan.FromSeconds(1);
        exposure.LastExposure = now;
        field.Exposures[mob] = exposure;
        if (now < exposure.NextDamage)
            return;

        var amount = HasComp<XenoComponent>(mob)
            ? field.BurnDamagePerSecond
            : Math.Min(field.BurnDamagePerSecond, field.HumanDamageCap - exposure.Damage);
        var next = exposure.NextDamage + TimeSpan.FromSeconds(1);
        field.Exposures[mob] = (next, now, exposure.Damage);
        if (amount <= 0f)
            return;

        var damage = new DamageSpecifier { DamageDict = { ["Heat"] = FixedPoint2.New(amount) } };
        var applied = _damage.TryChangeDamage(mob, damage, ignoreResistances: true, origin: fieldUid,
            ignoreGlobalModifiers: true);
        if (applied != null)
            field.Exposures[mob] = (next, now, exposure.Damage + applied.GetTotal().Float());
    }

    private void RailImpact(EntityUid source, DamageSpecifier damage, MapCoordinates impact)
    {
        foreach (var map in _zLevels.GetAllNetworkMaps(_map.GetMap(impact.MapId)))
        {
            if (_zLevels.TryGetMapCoordinates(map, impact.Position, out var projected))
                RailImpactOnLevel(source, damage, projected);
        }
    }

    private void RailImpactOnLevel(EntityUid source, DamageSpecifier damage, MapCoordinates impact)
    {
        var centerTile = new Vector2(MathF.Floor(impact.X), MathF.Floor(impact.Y));
        var center = centerTile + new Vector2(0.5f);
        _railTargets.Clear();
        _lookup.GetEntitiesInRange(new MapCoordinates(center, impact.MapId), 4, _railTargets, LookupFlags.Uncontained);
        foreach (var target in _railTargets)
        {
            if (TerminatingOrDeleted(target) || EntityManager.IsQueuedForDeletion(target))
                continue;

            var position = _transform.GetMapCoordinates(target);
            var tile = new Vector2(MathF.Floor(position.X), MathF.Floor(position.Y));
            if (position.MapId != impact.MapId ||
                MathF.Abs(tile.X - centerTile.X) > 2 || MathF.Abs(tile.Y - centerTile.Y) > 2 ||
                !TryGetGroundCoordinates(position, out var coordinates) ||
                !_area.CanOrbitalBombard(coordinates, out _))
                continue;

            var stateBeforeDamage = TryComp(target, out MobStateComponent? mob) ? (MobState?) mob.CurrentState : null;
            var preparing = new ExplosionDamagePreparingEvent(impact, damage.Clone());
            RaiseLocalEvent(target, ref preparing);
            var applied = _damage.TryChangeDamage(target, preparing.Damage, ignoreResistances: true,
                origin: source, impact: DamageImpact.Explosion);
            var received = new ExplosionReceivedEvent(RailExplosion, impact, applied ?? new DamageSpecifier(), stateBeforeDamage);
            RaiseLocalEvent(target, ref received);
        }

        var effect = Spawn("RMCExplosionEffectClusterOB", _transform.ToCoordinates(impact));
        _rmcExplosion.TryDoEffect(effect);
    }
}
