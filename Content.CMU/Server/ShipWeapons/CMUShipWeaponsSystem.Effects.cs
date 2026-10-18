using System.Numerics;
using Content.Shared._RMC14.Explosion;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Explosion;
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
    private readonly HashSet<Entity<DamageableComponent>> _railTargets = new();
    public static readonly EntProtoId EmsStun = "CMUShipWeaponEmsStun";
    private static readonly ProtoId<ExplosionPrototype> RailExplosion = "RMCOBXenoTunnel";

    private void InitializeEffects()
    {
        SubscribeLocalEvent<MobStateComponent, MoveEvent>(OnEmsMobMoved);
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

    private void RailImpact(EntityUid source, DamageSpecifier damage, MapCoordinates impact)
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
