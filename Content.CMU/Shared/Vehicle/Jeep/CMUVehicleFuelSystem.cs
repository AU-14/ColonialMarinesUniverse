using System.Diagnostics.CodeAnalysis;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Vehicle.Jeep;

public sealed class CMUVehicleFuelSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleFuelComponent, VehicleCanRunEvent>(OnCanRun);
        SubscribeLocalEvent<CMUVehicleFuelComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CMUVehicleFuelComponent, DoAfterAttemptEvent<CMUVehicleRefuelDoAfterEvent>>(OnRefuelAttempt);
        SubscribeLocalEvent<CMUVehicleFuelComponent, CMUVehicleRefuelDoAfterEvent>(OnRefuel);
    }

    private void OnCanRun(Entity<CMUVehicleFuelComponent> ent, ref VehicleCanRunEvent args)
    {
        if (!HasFuel((ent, ent.Comp)))
            args.CanRun = false;
    }

    private void OnExamined(Entity<CMUVehicleFuelComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("cmu-vehicle-fuel-examine", ("percent", (int) MathF.Round(Fraction(ent) * 100))));
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<CMUVehicleFuelComponent, GridVehicleMoverComponent, VehicleComponent>();
        while (query.MoveNext(out var uid, out var fuel, out var mover, out var vehicle))
        {
            // Only the engine burns fuel: pushes and coasting with nobody at the wheel are free.
            if (!mover.IsMoving || mover.IsPushMove || vehicle.Operator is not { } driver || !HasFuel((uid, fuel)))
                continue;

            fuel.Pending += fuel.BurnRate * frameTime;
            if (_timing.CurTime < fuel.NextSync)
                continue;

            fuel.NextSync = _timing.CurTime + fuel.SyncInterval;
            var hundredths = (int) (fuel.Pending * 100f);
            if (hundredths <= 0 || !TryGetTank((uid, fuel), out var tank, out _))
                continue;

            fuel.Pending -= hundredths / 100f;
            _solution.RemoveReagent(tank.Value, fuel.Reagent.Id, FixedPoint2.FromHundredths(hundredths));
            if (!HasFuel((uid, fuel)))
                _popup.PopupEntity(Loc.GetString("cmu-vehicle-fuel-empty"), uid, driver, PopupType.MediumCaution);
        }
    }

    public bool HasFuel(Entity<CMUVehicleFuelComponent?> vehicle)
    {
        return Resolve(vehicle, ref vehicle.Comp, false) &&
               TryGetTank((vehicle, vehicle.Comp), out _, out var tank) &&
               tank.GetTotalPrototypeQuantity(vehicle.Comp.Reagent) > 0;
    }

    public bool IsFuelDoor(Entity<CMUVehicleFuelComponent?> vehicle, string part)
    {
        return Resolve(vehicle, ref vehicle.Comp, false) && vehicle.Comp.Door == part;
    }

    /// <summary>
    /// Whether an item holds the solution this tank is filled from, so it's meant for the fuel door.
    /// </summary>
    public bool IsFuelCan(Entity<CMUVehicleFuelComponent?> vehicle, EntityUid can)
    {
        return Resolve(vehicle, ref vehicle.Comp, false) &&
               _solution.TryGetSolution(can, vehicle.Comp.Solution, out _, out _);
    }

    /// <summary>
    /// Starts pouring a fuel can into the tank. The progress bar is the tank level: it starts at the
    /// current fill and the tank fills along with it. Returns false if the item isn't a fuel can.
    /// </summary>
    public bool TryStartRefuel(Entity<CMUVehicleFuelComponent?> vehicle, EntityUid user, EntityUid can)
    {
        if (!Resolve(vehicle, ref vehicle.Comp, false) ||
            !TryGetTank((vehicle, vehicle.Comp), out _, out var tank) ||
            !_solution.TryGetSolution(can, vehicle.Comp.Solution, out _, out var contents))
        {
            return false;
        }

        var fuel = vehicle.Comp;
        if (contents.GetTotalPrototypeQuantity(fuel.Reagent) <= 0)
        {
            _popup.PopupClient(Loc.GetString("cmu-vehicle-fuel-can-empty"), vehicle, user);
            return true;
        }

        if (tank.AvailableVolume <= 0)
        {
            _popup.PopupClient(Loc.GetString("cmu-vehicle-fuel-full"), vehicle, user);
            return true;
        }

        var delay = TimeSpan.FromSeconds(tank.MaxVolume.Float() / fuel.PourRate);
        var args = new DoAfterArgs(EntityManager, user, delay, new CMUVehicleRefuelDoAfterEvent(), vehicle, vehicle, can)
        {
            BreakOnMove = true,
            NeedHand = true,
            BlockDuplicate = true,
            AttemptFrequency = AttemptFrequency.EveryTick,
        };

        if (!_doAfter.TryStartDoAfter(args, out var id))
            return true;

        _doAfter.CMUSetProgress(id.Value, Fraction((vehicle, fuel)));
        _audio.PlayPredicted(fuel.PourSound, vehicle, user);
        return true;
    }

    private void OnRefuelAttempt(Entity<CMUVehicleFuelComponent> ent, ref DoAfterAttemptEvent<CMUVehicleRefuelDoAfterEvent> args)
    {
        var doAfter = args.DoAfter;
        var progress = (float) ((_timing.CurTime - doAfter.StartTime) / doAfter.Args.Delay);
        if (!Pour(ent, doAfter.Args.Used, progress))
            args.Cancel();
    }

    private void OnRefuel(Entity<CMUVehicleFuelComponent> ent, ref CMUVehicleRefuelDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        if (Pour(ent, args.Used, 1f))
            _popup.PopupClient(Loc.GetString("cmu-vehicle-fuel-full"), ent, args.User);
    }

    /// <summary>
    /// Moves fuel from the can into the tank until it is filled to the given fraction. False once the
    /// can runs dry.
    /// </summary>
    private bool Pour(Entity<CMUVehicleFuelComponent> ent, EntityUid? can, float fraction)
    {
        if (_net.IsClient)
            return true;

        if (can == null ||
            !TryGetTank(ent, out var tankEnt, out var tank) ||
            !_solution.TryGetSolution(can.Value, ent.Comp.Solution, out var canEnt, out _))
        {
            return false;
        }

        var need = FixedPoint2.Min(tank.MaxVolume * Math.Clamp(fraction, 0f, 1f), tank.MaxVolume) - tank.Volume;
        if (need <= FixedPoint2.Zero)
            return true;

        var moved = _solution.RemoveReagent(canEnt.Value, ent.Comp.Reagent.Id, need);
        if (moved > FixedPoint2.Zero)
            _solution.TryAddReagent(tankEnt.Value, ent.Comp.Reagent.Id, moved);

        if (moved >= need)
            return true;

        _popup.PopupEntity(Loc.GetString("cmu-vehicle-fuel-can-empty"), ent, PopupType.Small);
        return false;
    }

    private bool TryGetTank(
        Entity<CMUVehicleFuelComponent> ent,
        [NotNullWhen(true)] out Entity<SolutionComponent>? tankEnt,
        [NotNullWhen(true)] out Solution? tank)
    {
        return _solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out tankEnt, out tank);
    }

    private float Fraction(Entity<CMUVehicleFuelComponent> ent)
    {
        if (!TryGetTank(ent, out _, out var tank) || tank.MaxVolume <= 0)
            return 0f;

        return Math.Clamp(tank.GetTotalPrototypeQuantity(ent.Comp.Reagent).Float() / tank.MaxVolume.Float(), 0f, 1f);
    }
}
