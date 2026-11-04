using System.Numerics;
using Content.Shared.CMU14.Fighter;
using Content.Shared.Vehicle.Components;
using VehicleSystem = Content.Shared.Vehicle.Systems.VehicleSystem;

namespace Content.Server.CMU14.Fighter;

public sealed partial class FighterSystem
{
    [Dependency] private VehicleSystem _taxiVehicles = default!;

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
        if (TryComp(hull, out FighterGroundComponent? ground)) StopTaxi((hull, ground));
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
        if (pilot != null && _timing.CurTime - pilot.LastInput < TimeSpan.FromSeconds(1)) return;
        if (ground.Comp.TaxiInput == FighterInput.None) return;
        ground.Comp.TaxiInput = FighterInput.None;
        Dirty(ground);
    }
}
