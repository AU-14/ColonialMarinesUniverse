using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Content.Shared.Buckle.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Fighter;

/// <summary>The ground fighter uses the same movement and collision controller as wheeled vehicles.</summary>
public sealed partial class FighterTaxiSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeAllEvent<FighterInputEvent>(OnInput);
        SubscribeLocalEvent<FighterGroundComponent, VehicleDriveInputEvent>(OnDriveInput);
        SubscribeLocalEvent<FighterGroundComponent, VehicleCanRunEvent>(OnCanRun);
    }

    private void OnInput(FighterInputEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user || !TryComp(user, out BuckleComponent? buckle) ||
            !TryComp(buckle.BuckledTo, out FighterSeatComponent? seat) || seat.Occupant != user ||
            !TryComp(seat.Aircraft, out FighterAircraftComponent? aircraft)) return;
        // Tick-ordered predictive events replay this state after a server correction.
        // A raw network message plus a local assignment lost held inputs on rollback.
        seat.Input = ev.Input & (FighterInput.Forward | FighterInput.Back | FighterInput.Left | FighterInput.Right);
        seat.LastInput = _timing.CurTime;
        seat.CameraControl = ev.CameraControl && FighterFlight.InAirspace(aircraft);
        Dirty(buckle.BuckledTo.Value, seat);
        if (seat.Pilot && aircraft.GroundEntity is { } ground && TryComp(ground, out FighterGroundComponent? taxi))
        {
            taxi.TaxiInput = taxi.State == FighterGroundState.Grounded && !seat.CameraControl ? seat.Input : FighterInput.None;
            Dirty(ground, taxi);
        }
    }

    private void OnCanRun(Entity<FighterGroundComponent> ground, ref VehicleCanRunEvent args)
    {
        if (ground.Comp.State != FighterGroundState.Grounded)
            args.CanRun = false;
    }

    private void OnDriveInput(Entity<FighterGroundComponent> ground, ref VehicleDriveInputEvent args)
    {
        // The server owns takeoff and landing movement, including an empty cockpit.
        args.Handled = true;
        if (ground.Comp.State != FighterGroundState.Grounded ||
            !TryComp(ground.Comp.Aircraft, out FighterAircraftComponent? aircraft) ||
            !TryComp(aircraft.FrontSeat, out FighterSeatComponent? pilot) || pilot.Occupant == null ||
            !TryComp(ground, out VehicleComponent? vehicle) || vehicle.Operator != pilot.Occupant)
            return;

        if (ground.Comp.TaxiInput != FighterInput.None)
        {
            args.Throttle = FighterFlight.Axis(ground.Comp.TaxiInput, FighterInput.Forward, FighterInput.Back);
            args.Steering = FighterFlight.Axis(ground.Comp.TaxiInput, FighterInput.Left, FighterInput.Right);
            return;
        }
        if (pilot.CameraControl) return;

        // Zero input means braking. Never fall through to character movement:
        // the crew is buckled to a child seat, not the vehicle's mover entity.
    }
}
