using System.Numerics;
using Content.Shared.CMU14.Fighter;
using Content.Shared.Vehicle.Components;
using VehicleSystem = Content.Shared.Vehicle.Systems.VehicleSystem;

namespace Content.Server.CMU14.Fighter;

public sealed partial class FighterSystem
{
    [Dependency] private VehicleSystem _taxiVehicles = default!;

    public bool TryFormUp(EntityUid? user)
    {
        if (!TryGetSeat(user, out var seat, out var aircraft) || !seat.Comp.Pilot ||
            aircraft.Comp.GroundEntity is not { } hull || !TryComp(hull, out FighterGroundComponent? ground) ||
            ground.State != FighterGroundState.Grounded) return false;
        var best = ground.PadSearchRange * ground.PadSearchRange;
        EntityUid? target = null;
        var pads = EntityQueryEnumerator<FighterLandingPadComponent, TransformComponent>();
        while (pads.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapUid != Transform(hull).MapUid || !CanUsePad(hull, uid)) continue;
            var distance = Vector2.DistanceSquared(_transform.GetWorldPosition(hull), _transform.GetWorldPosition(uid));
            if (distance > best || !GroundSiteClear(hull, xform.Coordinates)) continue;
            var reserved = false;
            var others = EntityQueryEnumerator<FighterGroundComponent>();
            while (others.MoveNext(out var other, out var candidate))
                if (other != hull && candidate.TaxiPad == uid) { reserved = true; break; }
            if (reserved) continue;
            target = uid;
            best = distance;
        }
        ground.TaxiPad = target;
        ground.TaxiLastPosition = _transform.GetWorldPosition(hull);
        ground.TaxiProgressAt = _timing.CurTime;
        if (target != null) seat.Comp.Input = FighterInput.None;
        Dirty(hull, ground);
        if (target == null) _popup.PopupEntity(Loc.GetString("cmu-fighter-pad-unavailable"), seat, user);
        return target != null;
    }

    private void UpdateTaxiOperator(Entity<FighterGroundComponent> ground, Entity<FighterAircraftComponent> aircraft)
    {
        if (!TryComp(ground, out VehicleComponent? vehicle)) return;
        var pilot = CompOrNull<FighterSeatComponent>(aircraft.Comp.FrontSeat);
        var desired = ground.Comp.State == FighterGroundState.Grounded ? pilot?.Occupant : null;
        if (vehicle.Operator == desired) return;
        _taxiVehicles.TryRemoveOperator((ground.Owner, vehicle));
        if (desired is { } driver) _taxiVehicles.TrySetOperator((ground.Owner, vehicle), driver);
    }

    private void ReleaseTaxiOperator(Entity<FighterSeatComponent> seat)
    {
        if (!seat.Comp.Pilot || !TryComp(seat.Comp.Aircraft, out FighterAircraftComponent? aircraft) ||
            aircraft.GroundEntity is not { } hull || !TryComp(hull, out VehicleComponent? vehicle) ||
            vehicle.Operator != seat.Comp.Occupant) return;
        _taxiVehicles.TryRemoveOperator((hull, vehicle));
        if (TryComp(hull, out FighterGroundComponent? ground))
        {
            ground.TaxiPad = null;
            StopTaxi((hull, ground));
        }
    }

    private void StopTaxi(Entity<FighterGroundComponent> ground)
    {
        ground.Comp.TaxiInput = FighterInput.None;
        _groundPhysics.SetLinearVelocity(ground, Vector2.Zero);
        if (TryComp(ground, out GridVehicleMoverComponent? mover))
        {
            mover.CurrentSpeed = mover.AngularVelocityDegrees = 0;
            mover.IsMoving = mover.IsPushMove = mover.IsCommittedToMove = false;
            mover.PushDirection = Vector2i.Zero;
            Dirty(ground, mover);
        }
        Dirty(ground);
    }

    private void UpdateTaxi(Entity<FighterGroundComponent> ground, Entity<FighterAircraftComponent> aircraft, float frameTime)
    {
        UpdateTaxiOperator(ground, aircraft);
        var pilot = CompOrNull<FighterSeatComponent>(aircraft.Comp.FrontSeat);
        var input = pilot is { CameraControl: false } ? ActiveInput(pilot) : FighterInput.None;
        if (pilot == null || _timing.CurTime - pilot.LastInput >= TimeSpan.FromSeconds(1))
        {
            if (ground.Comp.TaxiInput != FighterInput.None)
            {
                ground.Comp.TaxiInput = FighterInput.None;
                Dirty(ground);
            }
        }
        if (ground.Comp.TaxiPad is not { } pad) return;
        if (pilot?.Occupant == null || pilot.CameraControl || input != FighterInput.None ||
            TerminatingOrDeleted(pad) || !CanUsePad(ground, pad) || Transform(pad).MapUid != Transform(ground).MapUid)
        {
            ground.Comp.TaxiPad = null;
            Dirty(ground);
            return;
        }

        var position = _transform.GetWorldPosition(ground);
        var delta = _transform.GetWorldPosition(pad) - position;
        if (delta.LengthSquared() <= .2f * .2f)
        {
            StopTaxi(ground);
            var rotation = _transform.GetWorldRotation(ground);
            var angle = (_transform.GetWorldRotation(pad) + new Angle(Math.PI) - rotation).Theta;
            var difference = Math.Atan2(Math.Sin(angle), Math.Cos(angle));
            _transform.SetWorldRotation(ground, rotation + new Angle(Math.Clamp(difference, -frameTime, frameTime)));
            if (!GroundSiteClear(ground, Transform(ground).Coordinates))
                _transform.SetWorldRotation(ground, rotation);
            else if (Math.Abs(difference) < .025)
            {
                ground.Comp.TaxiPad = null;
                Dirty(ground);
                _popup.PopupEntity(Loc.GetString("cmu-fighter-pad-ready"), ground, pilot.Occupant);
            }
            ground.Comp.TaxiProgressAt = _timing.CurTime;
            return;
        }

        if (Vector2.DistanceSquared(position, ground.Comp.TaxiLastPosition) > .01f)
        {
            ground.Comp.TaxiLastPosition = position;
            ground.Comp.TaxiProgressAt = _timing.CurTime;
        }
        else if (_timing.CurTime - ground.Comp.TaxiProgressAt > TimeSpan.FromSeconds(3))
        {
            ground.Comp.TaxiPad = null;
            StopTaxi(ground);
            _popup.PopupEntity(Loc.GetString("cmu-fighter-pad-path-blocked"), ground, pilot.Occupant);
        }
    }
}
