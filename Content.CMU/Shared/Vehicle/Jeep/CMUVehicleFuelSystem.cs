using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
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
        if (ent.Comp.Fuel <= 0f)
            args.CanRun = false;
    }

    private void OnExamined(Entity<CMUVehicleFuelComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("cmu-vehicle-fuel-examine", ("percent", (int) MathF.Round(Fraction(ent.Comp) * 100))));
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<CMUVehicleFuelComponent, GridVehicleMoverComponent, VehicleComponent>();
        while (query.MoveNext(out var uid, out var fuel, out var mover, out var vehicle))
        {
            if (!mover.IsMoving || fuel.Fuel <= 0f)
                continue;

            fuel.Fuel = MathF.Max(0f, fuel.Fuel - fuel.BurnRate * frameTime);
            if (fuel.Fuel > 0f && _timing.CurTime < fuel.NextSync)
                continue;

            fuel.NextSync = _timing.CurTime + fuel.SyncInterval;
            Dirty(uid, fuel);

            if (fuel.Fuel <= 0f && vehicle.Operator is { } driver)
                _popup.PopupEntity(Loc.GetString("cmu-vehicle-fuel-empty"), uid, driver, PopupType.MediumCaution);
        }
    }

    /// <summary>
    /// Starts pouring a fuel can into the tank. The progress bar is the tank level: it starts at the
    /// current fill and the tank fills along with it. Returns false if the item isn't a fuel can.
    /// </summary>
    public bool TryStartRefuel(Entity<CMUVehicleFuelComponent?> vehicle, EntityUid user, EntityUid can)
    {
        if (!Resolve(vehicle, ref vehicle.Comp, false) ||
            !_solution.TryGetSolution(can, vehicle.Comp.CanSolution, out _, out var solution))
        {
            return false;
        }

        var fuel = vehicle.Comp;
        if (solution.GetTotalPrototypeQuantity(fuel.Reagent) <= 0)
        {
            _popup.PopupClient(Loc.GetString("cmu-vehicle-fuel-can-empty"), vehicle, user);
            return true;
        }

        if (fuel.Fuel >= fuel.MaxFuel)
        {
            _popup.PopupClient(Loc.GetString("cmu-vehicle-fuel-full"), vehicle, user);
            return true;
        }

        var delay = TimeSpan.FromSeconds(fuel.MaxFuel / fuel.PourRate);
        var args = new DoAfterArgs(EntityManager, user, delay, new CMUVehicleRefuelDoAfterEvent(), vehicle, vehicle, can)
        {
            BreakOnMove = true,
            NeedHand = true,
            BlockDuplicate = true,
            AttemptFrequency = AttemptFrequency.EveryTick,
        };

        if (!_doAfter.TryStartDoAfter(args, out var id))
            return true;

        _doAfter.CMUSetProgress(id.Value, Fraction(fuel));
        _audio.PlayPredicted(fuel.PourSound, vehicle, user);
        return true;
    }

    private void OnRefuelAttempt(Entity<CMUVehicleFuelComponent> ent, ref DoAfterAttemptEvent<CMUVehicleRefuelDoAfterEvent> args)
    {
        var doAfter = args.DoAfter;
        var progress = (float) ((_timing.CurTime - doAfter.StartTime) / doAfter.Args.Delay);
        if (!Pour(ent, doAfter.Args.Used, progress * ent.Comp.MaxFuel))
            args.Cancel();
    }

    private void OnRefuel(Entity<CMUVehicleFuelComponent> ent, ref CMUVehicleRefuelDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        Pour(ent, args.Used, ent.Comp.MaxFuel);
        _popup.PopupClient(Loc.GetString("cmu-vehicle-fuel-full"), ent, args.User);
    }

    /// <summary>
    /// Moves fuel from the can until the tank reaches the target level. False once the can is dry.
    /// </summary>
    private bool Pour(Entity<CMUVehicleFuelComponent> ent, EntityUid? can, float target)
    {
        if (_net.IsClient)
            return true;

        if (can == null || !_solution.TryGetSolution(can.Value, ent.Comp.CanSolution, out var solutionEnt, out _))
            return false;

        var need = MathF.Min(target, ent.Comp.MaxFuel) - ent.Comp.Fuel;
        if (need <= 0f)
            return true;

        var poured = (float) _solution.RemoveReagent(solutionEnt.Value, ent.Comp.Reagent.Id, need);
        ent.Comp.Fuel += poured;
        Dirty(ent);

        if (poured >= need)
            return true;

        _popup.PopupEntity(Loc.GetString("cmu-vehicle-fuel-can-empty"), ent, PopupType.Small);
        return false;
    }

    private static float Fraction(CMUVehicleFuelComponent fuel)
    {
        return fuel.MaxFuel <= 0f ? 0f : Math.Clamp(fuel.Fuel / fuel.MaxFuel, 0f, 1f);
    }
}
