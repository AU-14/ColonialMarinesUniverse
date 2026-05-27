using System.Numerics;
using Content.Shared._CMU14.Blackfoot;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._CMU14.Blackfoot;

public sealed partial class BlackfootLandingPadSystem : EntitySystem
{
    private const CollisionGroup PadBlockMask =
        CollisionGroup.Impassable |
        CollisionGroup.MidImpassable |
        CollisionGroup.HighImpassable |
        CollisionGroup.DropshipImpassable;

    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlackfootLandingPadComponent, ActivateInWorldEvent>(OnPadActivate, after: [typeof(ActivatableUISystem)]);
        SubscribeLocalEvent<BlackfootLandingPadComponent, ComponentShutdown>(OnPadShutdown);
        SubscribeLocalEvent<BlackfootFlightComputerComponent, ActivateInWorldEvent>(OnComputerActivate, after: [typeof(ActivatableUISystem)]);

        Subs.BuiEvents<BlackfootFlightComputerComponent>(BlackfootFlightComputerUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnComputerUiOpened);
            subs.Event<BlackfootFlightComputerFuelToggleMsg>(OnFuelToggle);
            subs.Event<BlackfootFlightComputerBatteryToggleMsg>(OnBatteryToggle);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BlackfootLandingPadComponent>();
        while (query.MoveNext(out var uid, out var pad))
        {
            UpdatePad((uid, pad), frameTime);
        }

        var lights = EntityQueryEnumerator<BlackfootLandingPadLightComponent>();
        while (lights.MoveNext(out var uid, out var light))
        {
            UpdatePadLight((uid, light));
        }
    }

    private void OnPadActivate(Entity<BlackfootLandingPadComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _popup.PopupEntity("Use tools to pack the Blackfoot landing pad.", ent, args.User, PopupType.SmallCaution);
    }

    private void OnPadShutdown(Entity<BlackfootLandingPadComponent> ent, ref ComponentShutdown args)
    {
        DeletePadLights(ent, dirty: false);
    }

    private void OnComputerActivate(Entity<BlackfootFlightComputerComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (!TryGetPad(ent, out var pad))
        {
            _popup.PopupEntity("No deployed Blackfoot landing pad is linked.", ent, args.User, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;

        if (pad.Comp.ParkedAircraft == null)
        {
            _popup.PopupEntity("No Blackfoot is parked on the landing pad.", ent, args.User, PopupType.SmallCaution);
            return;
        }

        var enabled = !(pad.Comp.Refueling || pad.Comp.Recharging);
        var fuelPumpLinked = !pad.Comp.RequireFuelPump || TryGetFuelPump(pad, out _);
        pad.Comp.Refueling = enabled && fuelPumpLinked;
        pad.Comp.Recharging = enabled;
        Dirty(pad);
        PushComputerState(ent, pad);

        var message = enabled
            ? pad.Comp.Refueling
                ? "Blackfoot refuel and recharge cycle started."
                : "No linked fuel pump found; Blackfoot recharge cycle started."
            : "Blackfoot refuel and recharge cycle stopped.";

        _popup.PopupEntity(message, ent, args.User);
    }

    private void OnComputerUiOpened(Entity<BlackfootFlightComputerComponent> ent, ref BoundUIOpenedEvent args)
    {
        PushComputerState(ent);
    }

    private void OnFuelToggle(Entity<BlackfootFlightComputerComponent> ent, ref BlackfootFlightComputerFuelToggleMsg args)
    {
        if (!TryGetPad(ent, out var pad))
            return;

        var enable = !pad.Comp.Refueling;
        if (enable && pad.Comp.RequireFuelPump && !TryGetFuelPump(pad, out _))
        {
            pad.Comp.Refueling = false;
            Dirty(pad);
            PushComputerState(ent, pad);
            return;
        }

        pad.Comp.Refueling = enable;
        Dirty(pad);
        PushComputerState(ent, pad);
    }

    private void OnBatteryToggle(Entity<BlackfootFlightComputerComponent> ent, ref BlackfootFlightComputerBatteryToggleMsg args)
    {
        if (!TryGetPad(ent, out var pad))
            return;

        pad.Comp.Recharging = !pad.Comp.Recharging;
        Dirty(pad);
        PushComputerState(ent, pad);
    }

    private void UpdatePad(Entity<BlackfootLandingPadComponent> pad, float frameTime)
    {
        if (pad.Comp.State != BlackfootLandingPadState.Deployed)
        {
            DeletePadLights(pad);

            if (pad.Comp.ParkedAircraft != null ||
                pad.Comp.Refueling ||
                pad.Comp.Recharging ||
                pad.Comp.FuelPump != null)
            {
                pad.Comp.ParkedAircraft = null;
                pad.Comp.Refueling = false;
                pad.Comp.Recharging = false;
                pad.Comp.FuelPump = null;
                Dirty(pad);
                PushLinkedComputerStates(pad);
            }

            return;
        }

        EnsurePadLights(pad);

        var parked = FindParkedAircraft(pad);
        if (pad.Comp.ParkedAircraft != parked)
        {
            pad.Comp.ParkedAircraft = parked;
            pad.Comp.Refueling &= parked != null;
            pad.Comp.Recharging &= parked != null;
            Dirty(pad);
            PushLinkedComputerStates(pad);
        }

        if (parked is not { } aircraft ||
            !TryComp(aircraft, out BlackfootFuelPowerComponent? fuelPower))
        {
            return;
        }

        var changed = false;
        var fuelPumpLinked = !pad.Comp.RequireFuelPump || TryGetFuelPump(pad, out _);
        if (pad.Comp.Refueling && !fuelPumpLinked)
        {
            pad.Comp.Refueling = false;
            changed = true;
        }

        if (pad.Comp.Refueling && fuelPumpLinked)
        {
            fuelPower.Fuel = MathF.Min(fuelPower.MaxFuel, fuelPower.Fuel + pad.Comp.FuelRate * frameTime);
            if (fuelPower.Fuel >= fuelPower.MaxFuel)
                pad.Comp.Refueling = false;

            changed = true;
        }

        if (pad.Comp.Recharging)
        {
            fuelPower.Battery = MathF.Min(fuelPower.MaxBattery, fuelPower.Battery + pad.Comp.BatteryRate * frameTime);
            if (fuelPower.Battery >= fuelPower.MaxBattery)
                pad.Comp.Recharging = false;

            changed = true;
        }

        if (!changed)
            return;

        Dirty(aircraft, fuelPower);
        Dirty(pad);
        PushLinkedComputerStates(pad);
    }

    private void UpdatePadLight(Entity<BlackfootLandingPadLightComponent> light)
    {
        var state = BlackfootLandingPadLightState.Off;
        EntityUid? linkedPad = null;

        if (light.Comp.LandingPad is { } existing &&
            TryComp(existing, out BlackfootLandingPadComponent? existingPad) &&
            existingPad.State == BlackfootLandingPadState.Deployed &&
            PadLightInRange(light, (existing, existingPad)))
        {
            linkedPad = existing;
            state = GetPadLightState(existingPad);
        }
        else if (TryFindPadForLight(light, out var pad))
        {
            linkedPad = pad.Owner;
            state = GetPadLightState(pad.Comp);
        }

        if (light.Comp.LandingPad == linkedPad &&
            light.Comp.State == state)
        {
            return;
        }

        light.Comp.LandingPad = linkedPad;
        light.Comp.State = state;
        Dirty(light);
    }

    private BlackfootLandingPadLightState GetPadLightState(BlackfootLandingPadComponent pad)
    {
        if (pad.State != BlackfootLandingPadState.Deployed)
            return BlackfootLandingPadLightState.Off;

        return pad.ParkedAircraft != null || pad.Refueling || pad.Recharging
            ? BlackfootLandingPadLightState.Servicing
            : BlackfootLandingPadLightState.Ready;
    }

    private bool TryFindPadForLight(
        Entity<BlackfootLandingPadLightComponent> light,
        out Entity<BlackfootLandingPadComponent> pad)
    {
        pad = default;

        var lightXform = Transform(light.Owner);
        var lightPosition = _transform.GetWorldPosition(light.Owner);
        var bestDistance = float.MaxValue;
        var query = EntityQueryEnumerator<BlackfootLandingPadComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.State != BlackfootLandingPadState.Deployed ||
                xform.MapUid != lightXform.MapUid)
            {
                continue;
            }

            var distance = Vector2.DistanceSquared(lightPosition, _transform.GetWorldPosition(uid));
            if (distance > light.Comp.PadSearchRange * light.Comp.PadSearchRange ||
                distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            pad = (uid, comp);
        }

        return pad.Owner != default;
    }

    private bool PadLightInRange(
        Entity<BlackfootLandingPadLightComponent> light,
        Entity<BlackfootLandingPadComponent> pad)
    {
        var lightXform = Transform(light.Owner);
        var padXform = Transform(pad.Owner);
        if (lightXform.MapUid != padXform.MapUid)
            return false;

        var distance = Vector2.DistanceSquared(
            _transform.GetWorldPosition(light.Owner),
            _transform.GetWorldPosition(pad.Owner));
        return distance <= light.Comp.PadSearchRange * light.Comp.PadSearchRange;
    }

    private bool CanDeploy(Entity<BlackfootLandingPadComponent> pad, out string reason)
    {
        reason = string.Empty;

        var xform = Transform(pad.Owner);
        if (xform.MapUid is not { } mapUid ||
            !TryComp(mapUid, out MapGridComponent? grid) ||
            !_map.TryGetTileRef(mapUid, grid, _transform.GetWorldPosition(pad.Owner), out var centerTile))
        {
            reason = "The landing pad must be deployed on valid ground.";
            return false;
        }

        var halfX = Math.Max(0, pad.Comp.Footprint.X / 2);
        var halfY = Math.Max(0, pad.Comp.Footprint.Y / 2);

        for (var x = -halfX; x <= halfX; x++)
        {
            for (var y = -halfY; y <= halfY; y++)
            {
                var tile = new Vector2i(centerTile.GridIndices.X + x, centerTile.GridIndices.Y + y);
                if (!_map.TryGetTileRef(mapUid, grid, tile, out var tileRef) ||
                    tileRef.Tile.IsEmpty ||
                    _turf.IsTileBlocked(tileRef, PadBlockMask))
                {
                    reason = "The landing pad needs a clear 3x3 deployment area.";
                    return false;
                }
            }
        }

        return true;
    }

    private EntityUid? FindParkedAircraft(Entity<BlackfootLandingPadComponent> pad)
    {
        var padXform = Transform(pad.Owner);
        if (padXform.MapUid is not { } mapUid ||
            !TryComp(mapUid, out MapGridComponent? grid) ||
            !_map.TryGetTileRef(mapUid, grid, _transform.GetWorldPosition(pad.Owner), out var padTile))
        {
            return null;
        }

        var halfX = Math.Max(0, pad.Comp.Footprint.X / 2);
        var halfY = Math.Max(0, pad.Comp.Footprint.Y / 2);
        var aircraftQuery = EntityQueryEnumerator<BlackfootFlightComponent, TransformComponent>();

        while (aircraftQuery.MoveNext(out var aircraft, out var flight, out var aircraftXform))
        {
            if (aircraftXform.MapUid != mapUid ||
                !CanParkAircraftState(flight.State))
            {
                continue;
            }

            if (!_map.TryGetTileRef(mapUid, grid, _transform.GetWorldPosition(aircraft), out var aircraftTile))
                continue;

            var dx = Math.Abs(aircraftTile.GridIndices.X - padTile.GridIndices.X);
            var dy = Math.Abs(aircraftTile.GridIndices.Y - padTile.GridIndices.Y);
            if (dx <= halfX && dy <= halfY)
                return aircraft;
        }

        return null;
    }

    internal static bool CanParkAircraftState(BlackfootFlightState state)
    {
        return state is
            BlackfootFlightState.Grounded or
            BlackfootFlightState.Idling or
            BlackfootFlightState.Stowed or
            BlackfootFlightState.Crashed;
    }

    private bool TryGetFuelPump(
        Entity<BlackfootLandingPadComponent> pad,
        out Entity<BlackfootFuelPumpComponent> fuelPump)
    {
        fuelPump = default;

        if (pad.Comp.FuelPump is { } linked &&
            TryComp(linked, out BlackfootFuelPumpComponent? linkedPump) &&
            linkedPump.LandingPad == pad.Owner &&
            FuelPumpInRange(pad, (linked, linkedPump)))
        {
            fuelPump = (linked, linkedPump);
            return true;
        }

        if (pad.Comp.FuelPump is { } stalePump &&
            TryComp(stalePump, out BlackfootFuelPumpComponent? stalePumpComp) &&
            stalePumpComp.LandingPad == pad.Owner)
        {
            stalePumpComp.LandingPad = null;
            Dirty(stalePump, stalePumpComp);
        }

        if (pad.Comp.FuelPump != null)
        {
            pad.Comp.FuelPump = null;
            Dirty(pad);
        }

        return false;
    }

    private bool FuelPumpInRange(
        Entity<BlackfootLandingPadComponent> pad,
        Entity<BlackfootFuelPumpComponent> fuelPump)
    {
        var padXform = Transform(pad.Owner);
        var pumpXform = Transform(fuelPump.Owner);
        if (padXform.MapUid != pumpXform.MapUid)
            return false;

        var range = MathF.Min(pad.Comp.FuelPumpSearchRange, fuelPump.Comp.PadSearchRange);
        var distance = Vector2.DistanceSquared(
            _transform.GetWorldPosition(pad.Owner),
            _transform.GetWorldPosition(fuelPump.Owner));
        return distance <= range * range;
    }

    private bool TryGetPad(
        Entity<BlackfootFlightComputerComponent> computer,
        out Entity<BlackfootLandingPadComponent> pad)
    {
        if (computer.Comp.LandingPad is { } linked &&
            TryComp(linked, out BlackfootLandingPadComponent? linkedPad) &&
            linkedPad.State == BlackfootLandingPadState.Deployed &&
            FlightComputerInRange(computer, (linked, linkedPad)))
        {
            pad = (linked, linkedPad);
            return true;
        }

        pad = default;
        if (computer.Comp.LandingPad != null)
        {
            computer.Comp.LandingPad = null;
            Dirty(computer);
        }

        return false;
    }

    private bool FlightComputerInRange(
        Entity<BlackfootFlightComputerComponent> computer,
        Entity<BlackfootLandingPadComponent> pad)
    {
        var computerXform = Transform(computer.Owner);
        var padXform = Transform(pad.Owner);
        if (computerXform.MapUid != padXform.MapUid)
            return false;

        var distance = Vector2.DistanceSquared(
            _transform.GetWorldPosition(computer.Owner),
            _transform.GetWorldPosition(pad.Owner));
        return distance <= computer.Comp.PadSearchRange * computer.Comp.PadSearchRange;
    }

    private void EnsurePadLights(Entity<BlackfootLandingPadComponent> pad)
    {
        if (pad.Comp.LightPrototype is not { } lightPrototype)
            return;

        var changed = false;
        for (var i = pad.Comp.Lights.Count - 1; i >= 0; i--)
        {
            if (Exists(pad.Comp.Lights[i]))
                continue;

            pad.Comp.Lights.RemoveAt(i);
            changed = true;
        }

        var padXform = Transform(pad.Owner);
        for (var i = pad.Comp.Lights.Count; i < pad.Comp.LightOffsets.Count; i++)
        {
            var offset = padXform.LocalRotation.RotateVec(pad.Comp.LightOffsets[i]);
            var light = Spawn(lightPrototype, padXform.Coordinates.Offset(offset));
            _transform.SetLocalRotation(light, padXform.LocalRotation);

            if (TryComp(light, out BlackfootLandingPadLightComponent? lightComp))
            {
                lightComp.LandingPad = pad.Owner;
                lightComp.State = GetPadLightState(pad.Comp);
                Dirty(light, lightComp);
            }

            pad.Comp.Lights.Add(light);
            changed = true;
        }

        for (var i = 0; i < pad.Comp.Lights.Count && i < pad.Comp.LightOffsets.Count; i++)
        {
            var light = pad.Comp.Lights[i];
            if (!Exists(light))
                continue;

            var offset = padXform.LocalRotation.RotateVec(pad.Comp.LightOffsets[i]);
            _transform.SetCoordinates(light, padXform.Coordinates.Offset(offset));
            _transform.SetLocalRotation(light, padXform.LocalRotation);

            if (TryComp(light, out BlackfootLandingPadLightComponent? lightComp))
            {
                lightComp.LandingPad = pad.Owner;
                lightComp.State = GetPadLightState(pad.Comp);
                Dirty(light, lightComp);
            }
        }

        if (changed)
            Dirty(pad);
    }

    private void DeletePadLights(Entity<BlackfootLandingPadComponent> pad, bool dirty = true)
    {
        if (pad.Comp.Lights.Count == 0)
            return;

        foreach (var light in pad.Comp.Lights)
        {
            if (Exists(light))
                QueueDel(light);
        }

        pad.Comp.Lights.Clear();

        if (dirty)
            Dirty(pad);
    }

    private void PushComputerState(
        Entity<BlackfootFlightComputerComponent> computer,
        Entity<BlackfootLandingPadComponent>? pad = null)
    {
        Entity<BlackfootLandingPadComponent> activePad;
        if (pad is { } providedPad)
        {
            activePad = providedPad;
        }
        else if (!TryGetPad(computer, out activePad))
        {
            _ui.SetUiState(
                computer.Owner,
                BlackfootFlightComputerUiKey.Key,
                new BlackfootFlightComputerBuiState(null, 0f, 0f, 0f, 0f, false, false, false, false));
            return;
        }

        var fuelPumpLinked = !activePad.Comp.RequireFuelPump || TryGetFuelPump(activePad, out _);
        var aircraft = activePad.Comp.ParkedAircraft;
        if (aircraft is not { } aircraftUid ||
            !TryComp(aircraftUid, out BlackfootFuelPowerComponent? fuelPower))
        {
            _ui.SetUiState(
                computer.Owner,
                BlackfootFlightComputerUiKey.Key,
                new BlackfootFlightComputerBuiState(
                    null,
                    0f,
                    0f,
                    0f,
                    0f,
                    activePad.Comp.Refueling,
                    activePad.Comp.Recharging,
                    true,
                    fuelPumpLinked));
            return;
        }

        _ui.SetUiState(
            computer.Owner,
            BlackfootFlightComputerUiKey.Key,
            new BlackfootFlightComputerBuiState(
                GetNetEntity(aircraftUid),
                fuelPower.Fuel,
                fuelPower.MaxFuel,
                fuelPower.Battery,
                fuelPower.MaxBattery,
                activePad.Comp.Refueling,
                activePad.Comp.Recharging,
                true,
                fuelPumpLinked));
    }

    private void PushLinkedComputerStates(Entity<BlackfootLandingPadComponent> pad)
    {
        var query = EntityQueryEnumerator<BlackfootFlightComputerComponent>();
        while (query.MoveNext(out var uid, out var computer))
        {
            if (computer.LandingPad != pad.Owner)
                continue;

            PushComputerState((uid, computer), pad);
        }
    }
}
