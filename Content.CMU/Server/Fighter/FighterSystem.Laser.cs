using Content.Shared.CMU14.Fighter;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Spawners;

namespace Content.Server.CMU14.Fighter;

public sealed partial class FighterSystem
{
    [Dependency] private SharedAudioSystem _audio = default!;

    private void WarnLaserStrike(EntityUid uid)
    {
        if (!TryComp(uid, out FighterLaserComponent? laser)) return;
        laser.Incoming = true;
        laser.IncomingAt = _timing.CurTime;
        Dirty(uid, laser);
        _fighterAudio.PlayGround(laser.IncomingSound, Transform(uid).Coordinates, 24, -4);
    }

    private void OnLaserShutdown(Entity<FighterLaserComponent> laser, ref ComponentShutdown args)
    {
        // Timed despawn can run after our update. Clear entity references before
        // state generation, instead of leaving a deleted laser in the next seat state.
        if (TryComp(laser.Comp.Seat, out FighterSeatComponent? owner) && owner.Laser == laser.Owner)
        {
            owner.Laser = null;
            if (!TerminatingOrDeleted(laser.Comp.Seat)) Dirty(laser.Comp.Seat, owner);
        }
        if (!TryComp(laser.Comp.Aircraft, out FighterAircraftComponent? aircraft)) return;
        var id = GetNetEntity(laser.Owner);
        foreach (var seatUid in new[] { aircraft.FrontSeat, aircraft.RearSeat })
        {
            if (seatUid is not { } uid || !TryComp(uid, out FighterSeatComponent? seat)) continue;
            if (seat.LaserLockTarget == id) CancelLaserLock((uid, seat));
            if (seat.Target != id) continue;
            seat.Target = null;
            seat.TargetPosition = null;
            if (!TerminatingOrDeleted(uid)) Dirty(uid, seat);
        }
        if (TryComp(laser.Comp.Aircraft, out FighterWeaponsComponent? weapons)) weapons.NextRefresh = TimeSpan.Zero;
    }

    public bool TryLase(EntityUid? user, out FighterFireStatus status)
    {
        status = FighterFireStatus.NoTarget;
        if (!TryGetSeat(user, out var seat, out var aircraft) || !TryComp(aircraft, out FighterWeaponsComponent? weapons))
            return false;
        if (aircraft.Comp.ForcedRetreat)
        {
            status = FighterFireStatus.Retreat;
            return false;
        }
        // Re-selecting a live mark never moves it or extends its lifetime.
        if (seat.Comp.Laser is { } existing && TryGetTarget(existing, aircraft, weapons, out var current))
        {
            status = FighterFireStatus.Ready;
            return TryLockTarget(user, current.Id);
        }

        var point = FighterFlight.SensorPosition(aircraft.Comp, seat.Comp);
        if (!FighterFlight.SensorAvailable(aircraft.Comp, point, seat.Comp))
        {
            status = FighterFireStatus.OutOfRange;
            return false;
        }
        if (FighterOptics.CloudsBlock(aircraft.Comp, point, _timing.CurTime))
        {
            status = FighterFireStatus.Clouds;
            return false;
        }
        var mapPoint = new MapCoordinates(point, Comp<MapComponent>(aircraft.Comp.TerrainMap).MapId);
        var coordinates = _map.TryFindGridAt(mapPoint, out var grid, out _)
            ? _transform.ToCoordinates(grid, mapPoint) : _transform.ToCoordinates(mapPoint);
        if (!_areas.CanCAS(coordinates))
        {
            status = FighterFireStatus.Protected;
            return false;
        }

        ClearLaser(seat);
        var uid = Spawn("CMUFighterLaser", coordinates);
        var laser = Comp<FighterLaserComponent>(uid);
        laser.Aircraft = aircraft;
        laser.Seat = seat;
        laser.StartedAt = _timing.CurTime;
        laser.ExpiresAt = laser.StartedAt + weapons.LaserLifetime;
        Comp<TimedDespawnComponent>(uid).Lifetime = (float) weapons.LaserLifetime.TotalSeconds;
        seat.Comp.Laser = uid;
        OrientLaser(uid, aircraft.Comp);
        Dirty(uid, laser);
        Dirty(seat);
        RefreshWeapons(aircraft, weapons);
        PlayEffect(aircraft, FighterEffectKind.Laser, seat.Comp.Pilot ? 0 : 1);
        status = FighterFireStatus.Ready;
        return TryLockTarget(user, GetNetEntity(uid));
    }

    private bool TryGetTarget(EntityUid uid, Entity<FighterAircraftComponent> aircraft, FighterWeaponsComponent weapons, out FighterTarget target)
    {
        target = default!;
        if (TerminatingOrDeleted(uid)) return false;
        if (!TryComp(uid, out FighterLaserComponent? laser))
            return TryGetFlare(uid, aircraft.Comp, weapons, out target);
        if (laser.Aircraft != aircraft.Owner || _timing.CurTime >= laser.ExpiresAt ||
            !TryComp(laser.Seat, out FighterSeatComponent? owner) || owner.Laser != uid || owner.Occupant == null ||
            Transform(uid).MapUid != aircraft.Comp.TerrainMap) return false;
        var position = _transform.GetWorldPosition(uid);
        if (!aircraft.Comp.Battlefield.Contains(position)) return false;
        target = new(GetNetEntity(uid), Loc.GetString(owner.Pilot ? "cmu-fighter-pilot-laser" : "cmu-fighter-officer-laser"),
            position, _areas.CanCAS(Transform(uid).Coordinates), true, laser.ExpiresAt, laser.Incoming);
        return true;
    }

    private void OrientLaser(EntityUid laser, FighterAircraftComponent aircraft)
    {
        var towardJet = aircraft.Position - _transform.GetWorldPosition(laser);
        _transform.SetWorldRotation(laser, new Angle(Math.Atan2(towardJet.Y, towardJet.X) - Math.PI / 2));
    }

    private void CancelLaserLock(Entity<FighterSeatComponent> seat)
    {
        if (seat.Comp.LaserLockTarget == null) return;
        seat.Comp.LaserLockTarget = null;
        seat.Comp.LaserLockAmmo = null;
        seat.Comp.LaserLockReadyAt = default;
        if (!TerminatingOrDeleted(seat)) Dirty(seat);
    }

    private void ClearLaser(Entity<FighterSeatComponent> seat)
    {
        if (seat.Comp.Laser is not { } uid) return;
        if (!TerminatingOrDeleted(uid)) QueueDel(uid);
        seat.Comp.Laser = null;
        if (!TerminatingOrDeleted(seat)) Dirty(seat);
    }

    private void UpdateLaser(Entity<FighterSeatComponent> seat, Entity<FighterAircraftComponent> aircraft, FighterWeaponsComponent weapons)
    {
        if (seat.Comp.Laser is { } laser)
        {
            if (TryGetTarget(laser, aircraft, weapons, out _)) OrientLaser(laser, aircraft.Comp);
            else ClearLaser(seat);
        }
        if (seat.Comp.LaserLockTarget is not { } id) return;
        if (seat.Comp.Occupant is not { } user || seat.Comp.Target != id || seat.Comp.WeaponSlot != seat.Comp.LaserLockSlot ||
            seat.Comp.WeaponSlot < 0 || seat.Comp.WeaponSlot >= weapons.Hardpoints.Count ||
            !TryComp(weapons.Hardpoints[seat.Comp.WeaponSlot], out FighterHardpointComponent? mount) ||
            mount.Ammo != seat.Comp.LaserLockAmmo || !TryGetEntity(id, out var target) || target == null ||
            !TryGetTarget(target.Value, aircraft, weapons, out var designation))
        {
            CancelLaserLock(seat);
            return;
        }
        var weapon = GetWeaponStatus((weapons.Hardpoints[seat.Comp.WeaponSlot], mount));
        var status = FighterWeapons.Status(aircraft.Comp, weapons, seat.Comp, weapon, designation, _timing.CurTime);
        if (status == FighterFireStatus.Locking) return;
        if (status == FighterFireStatus.Ready) TryFire(user, out _);
        // Exactly one launch per Fire press, including a failed release revalidation.
        CancelLaserLock(seat);
    }
}
