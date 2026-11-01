using System.Numerics;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Lungs;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.ShipWeapons;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Explosion;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.ShipWeapons;

public sealed partial class CMUShipWeaponsSystem
{
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private RMCSlowSystem _slow = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    private readonly HashSet<Entity<DamageableComponent>> _railTargets = new();
    public static readonly EntProtoId EmsStun = "CMUShipWeaponEmsStun";
    private static readonly ProtoId<ExplosionPrototype> RailExplosion = "RMCOBXenoTunnel";
    public static readonly EntProtoId GasHaze = "CMUShipWeaponGasHaze";

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
            !TryGetGroundCoordinates(position, out var coordinates) ||
            !_area.CanOrbitalBombard(coordinates, out _))
            return;

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
