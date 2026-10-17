using System.Numerics;
using Content.Shared.CMU14.Fighter;
using Content.Shared.Database;
using Robust.Shared.Spawners;
using Robust.Shared.Map;

namespace Content.Shared._RMC14.Dropship.Weapon;

public abstract partial class SharedDropshipWeaponSystem
{
    [Dependency] private FighterAudioSystem _fighterAudio = default!;
    /// <summary>Fighter release uses CAS payload effects without pretending its cockpit is an FTL dropship.</summary>
    public bool TryFireFighterAmmo(Entity<DropshipAmmoComponent> ammo, Entity<FighterHardpointComponent> point,
        EntityCoordinates coordinates, EntityUid actor, TimeSpan? strikeTravelTime = null,
        FighterWeaponKind kind = FighterWeaponKind.Missile, Vector2 aircraftPosition = default)
    {
        if (_net.IsClient || !coordinates.IsValid(EntityManager) || !_area.CanCAS(coordinates) ||
            point.Comp.Ammo != ammo.Owner || ammo.Comp.RoundsPerShot <= 0 || ammo.Comp.Rounds < ammo.Comp.RoundsPerShot)
            return false;

        var time = _timing.CurTime;
        var payload = ammo.Comp;
        var target = payload.TargetSpread > 0
            ? coordinates.Offset(_random.NextVector2(-payload.TargetSpread, payload.TargetSpread + 1)) : coordinates;
        if (payload.Explosion != null && HasNonDeletableWallOnTile(target))
            target = FindAlternateLandingTile(target, 3);
        var flight = new AmmoInFlightComponent
        {
            Target = target,
            MarkerAt = time + (strikeTravelTime ?? payload.TravelTime),
            ShotsLeft = payload.RoundsPerShot,
            ShotsPerVolley = payload.ShotsPerVolley,
            Damage = payload.Damage,
            ArmorPiercing = payload.ArmorPiercing,
            BulletSpread = payload.BulletSpread,
            SoundTravelTime = strikeTravelTime.HasValue ? TimeSpan.Zero : payload.SoundTravelTime,
            SoundMarker = null,
            // The fighter reports on release and at the real dispersed impacts,
            // rather than replaying the dropship flyby after the salvo has ended.
            SoundGround = null,
            SoundImpact = null,
            SoundWarning = null,
            MarkerWarning = !strikeTravelTime.HasValue && payload.MarkerWarning,
            WarningMarkerAt = time + TimeSpan.FromSeconds(DefaultMarkerDuration),
            ImpactEffects = new(payload.ImpactEffects),
            Explosion = payload.Explosion,
            Implosion = payload.Implosion,
            Fire = payload.Fire,
            SoundEveryShots = payload.SoundEveryShots,
            // A close strafing pass must not wait through the dropship target-marker sequence.
            MarkerDuration = strikeTravelTime.HasValue ? TimeSpan.Zero : TimeSpan.FromSeconds(DefaultMarkerDuration),
        };
        var flightUid = Spawn(null, MapCoordinates.Nullspace);
        AddComp(flightUid, flight);
        var visual = Spawn(null, target);
        var trace = AddComp<FighterStrikeVisualComponent>(visual);
        AddComp<FighterPayloadVisualComponent>(flightUid).Visual = visual;
        trace.Kind = kind;
        var direction = aircraftPosition - _transform.ToMapCoordinates(target).Position;
        trace.Direction = direction.LengthSquared() > .001f ? Vector2.Normalize(direction) : Vector2.UnitY;
        trace.ImpactAt = flight.MarkerAt + flight.MarkerDuration;
        trace.Volleys = (int) Math.Ceiling(payload.RoundsPerShot / (double) Math.Max(1, payload.ShotsPerVolley));
        trace.VolleyInterval = (float) flight.ShotDelay.TotalSeconds;
        trace.ExpiresAt = trace.ImpactAt + TimeSpan.FromSeconds(trace.Volleys * trace.VolleyInterval + .4f);
        EnsureComp<TimedDespawnComponent>(visual).Lifetime = (float) (trace.ExpiresAt - time).TotalSeconds;
        Dirty(visual, trace);
        payload.Rounds -= payload.RoundsPerShot;
        _appearance.SetData(ammo, DropshipAmmoVisuals.Fill, payload.Rounds);
        Dirty(ammo);
        _audio.PlayPvs(FighterAudioSystem.Release(kind, cockpit: true), point);
        _fighterAudio.PlayGround(FighterAudioSystem.Release(kind), target, volume: kind == FighterWeaponKind.Gau ? -1 : -4);
        _adminLog.Add(LogType.RMCDropshipWeapon, $"{ToPrettyString(actor)} fired fighter ammunition {ToPrettyString(ammo.Owner)} at {coordinates}");
        if (payload.DeleteOnEmpty && payload.Rounds <= 0) QueueDel(ammo);
        return true;
    }
}
