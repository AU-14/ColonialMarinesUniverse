using System.Collections.Generic;
using System.Numerics;
using Content.Server._CMU14.ZLevels.Core;
using Content.Shared._CMU14.Blackfoot;
using Content.Shared._CMU14.ZLevels.Core;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Actions;
using Content.Shared.Audio;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Blackfoot;

public sealed partial class BlackfootFlightSystem : EntitySystem
{
    private const string ThrustersSlotId = "thrusters";
    private static readonly AudioParams InteriorEngineLoopParams = AudioParams.Default
        .WithLoop(true)
        .WithVolume(-2f);

    private const CollisionGroup FootprintBlockMask =
        CollisionGroup.Impassable |
        CollisionGroup.MidImpassable |
        CollisionGroup.HighImpassable |
        CollisionGroup.DropshipImpassable;

    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private AreaSystem _area = default!;
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private HardpointSystem _hardpoints = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ITileDefinitionManager _tile = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private VehicleViewToggleSystem _viewToggle = default!;
    [Dependency] private CMUZLevelsSystem _zLevels = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleComponent, VehicleOperatorSetEvent>(OnVehicleOperatorSet);
        SubscribeLocalEvent<BlackfootPilotActionComponent, ComponentShutdown>(OnPilotActionsShutdown);
        SubscribeLocalEvent<BlackfootPilotActionComponent, BlackfootEngineToggleActionEvent>(OnEngineToggle);
        SubscribeLocalEvent<BlackfootPilotActionComponent, BlackfootTakeoffActionEvent>(OnTakeoff);
        SubscribeLocalEvent<BlackfootPilotActionComponent, BlackfootLandActionEvent>(OnLand);
        SubscribeLocalEvent<BlackfootPilotActionComponent, BlackfootFlightModeToggleActionEvent>(OnFlightModeToggle);
        SubscribeLocalEvent<BlackfootPilotActionComponent, BlackfootRearDoorToggleActionEvent>(OnRearDoorToggle);
        SubscribeLocalEvent<BlackfootPilotActionComponent, BlackfootStowToggleActionEvent>(OnStowToggle);
        SubscribeLocalEvent<BlackfootPilotActionComponent, BlackfootAscendZLevelActionEvent>(OnAscendZLevel);
        SubscribeLocalEvent<BlackfootPilotActionComponent, BlackfootDescendZLevelActionEvent>(OnDescendZLevel);
        SubscribeLocalEvent<BlackfootFlightComponent, MapInitEvent>(OnBlackfootMapInit);
        SubscribeLocalEvent<BlackfootFlightComponent, ComponentShutdown>(OnBlackfootShutdown);
        SubscribeLocalEvent<BlackfootFlightComponent, DamageChangedEvent>(OnBlackfootDamageChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var aircraft = EntityQueryEnumerator<BlackfootFlightComponent>();
        while (aircraft.MoveNext(out var uid, out var flight))
        {
            ProcessFuel((uid, flight), frameTime);
            ProcessThrusters((uid, flight));
            ProcessTransition((uid, flight));
            UpdateProjectedDownwashState((uid, flight));
            UpdateEngineAudio((uid, flight));
        }

        var shadows = EntityQueryEnumerator<BlackfootShadowComponent>();
        while (shadows.MoveNext(out var uid, out var shadow))
        {
            UpdateShadow((uid, shadow));
        }

        var downwashes = EntityQueryEnumerator<BlackfootDownwashComponent>();
        while (downwashes.MoveNext(out var uid, out var downwash))
        {
            UpdateDownwash((uid, downwash));
        }
    }

    private void OnVehicleOperatorSet(Entity<VehicleComponent> ent, ref VehicleOperatorSetEvent args)
    {
        if (!TryComp(ent.Owner, out BlackfootFlightComponent? flight))
            return;

        if (args.OldOperator is { } oldOperator)
            DisablePilotActions(oldOperator, ent.Owner);

        if (args.NewOperator is { } newOperator)
            EnablePilotActions(newOperator, ent.Owner, flight);
    }

    private void OnBlackfootMapInit(Entity<BlackfootFlightComponent> ent, ref MapInitEvent args)
    {
        _zLevels.EnsureZLevelViewer(ent.Owner);
    }

    private void EnablePilotActions(EntityUid pilot, EntityUid vehicle, BlackfootFlightComponent flight)
    {
        var actions = EnsureComp<BlackfootPilotActionComponent>(pilot);
        actions.Vehicle = vehicle;

        actions.EngineToggleAction ??= _actions.AddAction(pilot, actions.EngineToggleActionId);
        actions.TakeoffAction ??= _actions.AddAction(pilot, actions.TakeoffActionId);
        actions.LandAction ??= _actions.AddAction(pilot, actions.LandActionId);
        actions.FlightModeToggleAction ??= _actions.AddAction(pilot, actions.FlightModeToggleActionId);
        actions.StowToggleAction ??= _actions.AddAction(pilot, actions.StowToggleActionId);
        actions.AscendZLevelAction ??= _actions.AddAction(pilot, actions.AscendZLevelActionId);
        actions.DescendZLevelAction ??= _actions.AddAction(pilot, actions.DescendZLevelActionId);

        if (HasComp<BlackfootRearDoorComponent>(vehicle))
            actions.RearDoorToggleAction ??= _actions.AddAction(pilot, actions.RearDoorToggleActionId);

        UpdatePilotActions(pilot, actions, (vehicle, flight));
        Dirty(pilot, actions);
    }

    private void DisablePilotActions(EntityUid pilot, EntityUid vehicle)
    {
        if (!TryComp(pilot, out BlackfootPilotActionComponent? actions) ||
            actions.Vehicle != vehicle)
        {
            return;
        }

        RemovePilotActions(pilot, actions);
        RemCompDeferred<BlackfootPilotActionComponent>(pilot);
    }

    private void OnPilotActionsShutdown(Entity<BlackfootPilotActionComponent> ent, ref ComponentShutdown args)
    {
        RemovePilotActions(ent.Owner, ent.Comp);
    }

    private void RemovePilotActions(EntityUid pilot, BlackfootPilotActionComponent actions)
    {
        RemoveAction(pilot, ref actions.EngineToggleAction);
        RemoveAction(pilot, ref actions.TakeoffAction);
        RemoveAction(pilot, ref actions.LandAction);
        RemoveAction(pilot, ref actions.FlightModeToggleAction);
        RemoveAction(pilot, ref actions.RearDoorToggleAction);
        RemoveAction(pilot, ref actions.StowToggleAction);
        RemoveAction(pilot, ref actions.AscendZLevelAction);
        RemoveAction(pilot, ref actions.DescendZLevelAction);
        actions.Vehicle = null;
    }

    private void RemoveAction(EntityUid pilot, ref EntityUid? action)
    {
        if (action is not { } actionUid)
            return;

        _actions.RemoveAction(pilot, actionUid);
        action = null;
    }

    private void OnEngineToggle(Entity<BlackfootPilotActionComponent> ent, ref BlackfootEngineToggleActionEvent args)
    {
        if (!TryGetPilotedBlackfoot(ent, args.Performer, out var vehicle, out var flight))
            return;

        args.Handled = true;

        switch (flight.Comp.State)
        {
            case BlackfootFlightState.Grounded:
                if (HasTowConnection(vehicle))
                {
                    Popup(args.Performer, "Disconnect towing gear before starting the engines.", PopupType.SmallCaution);
                    break;
                }

                SetState(vehicle, flight, BlackfootFlightState.Idling);
                Popup(args.Performer, "Blackfoot engines idling.");
                break;
            case BlackfootFlightState.Idling:
                SetState(vehicle, flight, BlackfootFlightState.Grounded);
                Popup(args.Performer, "Blackfoot engines offline.");
                break;
            default:
                Popup(args.Performer, "The engines cannot be toggled in the current flight state.", PopupType.SmallCaution);
                break;
        }

        UpdatePilotActions(ent.Owner, ent.Comp, flight);
    }

    private void OnTakeoff(Entity<BlackfootPilotActionComponent> ent, ref BlackfootTakeoffActionEvent args)
    {
        if (!TryGetPilotedBlackfoot(ent, args.Performer, out _, out var flight))
            return;

        args.Handled = true;

        if (!CanStartTakeoff(flight, args.Performer))
            return;

        StartTimedTransition(flight, BlackfootFlightState.TakingOff, flight.Comp.TakeoffDuration);
        Popup(args.Performer, "Blackfoot takeoff sequence started.");
        UpdatePilotActions(ent.Owner, ent.Comp, flight);
    }

    private void OnLand(Entity<BlackfootPilotActionComponent> ent, ref BlackfootLandActionEvent args)
    {
        if (!TryGetPilotedBlackfoot(ent, args.Performer, out _, out var flight))
            return;

        args.Handled = true;

        if (!CanStartLanding(flight, args.Performer))
            return;

        StartTimedTransition(flight, BlackfootFlightState.Landing, flight.Comp.LandingDuration);
        Popup(args.Performer, "Blackfoot landing sequence started.");
        UpdatePilotActions(ent.Owner, ent.Comp, flight);
    }

    private void OnFlightModeToggle(Entity<BlackfootPilotActionComponent> ent, ref BlackfootFlightModeToggleActionEvent args)
    {
        if (!TryGetPilotedBlackfoot(ent, args.Performer, out _, out var flight))
            return;

        args.Handled = true;

        switch (flight.Comp.State)
        {
            case BlackfootFlightState.VTOL:
                flight.Comp.MovementMode = BlackfootMovementMode.Flight;
                SetState(flight.Owner, flight, BlackfootFlightState.Flight);
                Popup(args.Performer, "Blackfoot flight mode engaged.");
                break;
            case BlackfootFlightState.Flight:
                flight.Comp.MovementMode = BlackfootMovementMode.VTOL;
                SetState(flight.Owner, flight, BlackfootFlightState.VTOL);
                Popup(args.Performer, "Blackfoot VTOL mode engaged.");
                break;
            default:
                Popup(args.Performer, "Flight mode can only be changed while airborne.", PopupType.SmallCaution);
                break;
        }

        UpdatePilotActions(ent.Owner, ent.Comp, flight);
    }

    private void OnRearDoorToggle(Entity<BlackfootPilotActionComponent> ent, ref BlackfootRearDoorToggleActionEvent args)
    {
        if (!TryGetPilotedBlackfoot(ent, args.Performer, out var vehicle, out var flight))
            return;

        args.Handled = true;

        if (!TryComp(vehicle, out BlackfootRearDoorComponent? rearDoor))
        {
            Popup(args.Performer, "This Blackfoot has no rear door controls.", PopupType.SmallCaution);
            return;
        }

        rearDoor.Open = !rearDoor.Open;
        Dirty(vehicle, rearDoor);
        Popup(args.Performer, rearDoor.Open ? "Rear door opened." : "Rear door closed.");
        UpdatePilotActions(ent.Owner, ent.Comp, flight);
    }

    private void OnStowToggle(Entity<BlackfootPilotActionComponent> ent, ref BlackfootStowToggleActionEvent args)
    {
        if (!TryGetPilotedBlackfoot(ent, args.Performer, out _, out var flight))
            return;

        args.Handled = true;

        switch (flight.Comp.State)
        {
            case BlackfootFlightState.Grounded:
                SetState(flight.Owner, flight, BlackfootFlightState.Stowed);
                Popup(args.Performer, "Blackfoot stowed.");
                break;
            case BlackfootFlightState.Stowed:
                SetState(flight.Owner, flight, BlackfootFlightState.Grounded);
                Popup(args.Performer, "Blackfoot deployed.");
                break;
            default:
                Popup(args.Performer, "The Blackfoot must be grounded to stow or deploy.", PopupType.SmallCaution);
                break;
        }

        UpdatePilotActions(ent.Owner, ent.Comp, flight);
    }

    private void OnAscendZLevel(Entity<BlackfootPilotActionComponent> ent, ref BlackfootAscendZLevelActionEvent args)
    {
        if (!TryGetPilotedBlackfoot(ent, args.Performer, out _, out var flight))
            return;

        args.Handled = true;
        TryMoveAltitude(flight, args.Performer, 1);
        UpdatePilotActions(ent.Owner, ent.Comp, flight);
    }

    private void OnDescendZLevel(Entity<BlackfootPilotActionComponent> ent, ref BlackfootDescendZLevelActionEvent args)
    {
        if (!TryGetPilotedBlackfoot(ent, args.Performer, out _, out var flight))
            return;

        args.Handled = true;
        TryMoveAltitude(flight, args.Performer, -1);
        UpdatePilotActions(ent.Owner, ent.Comp, flight);
    }

    private void ProcessTransition(Entity<BlackfootFlightComponent> flight)
    {
        if (flight.Comp.State is not (BlackfootFlightState.TakingOff or BlackfootFlightState.Landing))
            return;

        if (_timing.CurTime < flight.Comp.TransitionEndTime)
            return;

        if (flight.Comp.State == BlackfootFlightState.TakingOff)
        {
            FinishTakeoff(flight);
            return;
        }

        FinishLanding(flight);
    }

    private void ProcessFuel(Entity<BlackfootFlightComponent> flight, float frameTime)
    {
        if (!TryComp(flight, out BlackfootFuelPowerComponent? fuel))
            return;

        var drain = GetFuelDrain(flight.Comp, fuel) + GetFuelLeakDrain(flight, fuel);
        if (drain <= 0f)
            return;

        fuel.Fuel = MathF.Max(0f, fuel.Fuel - drain * frameTime);
        Dirty(flight.Owner, fuel);

        if (fuel.Fuel > 0f || !fuel.CrashOnZeroFuel || !IsAirborneOrTransitioning(flight.Comp.State))
            return;

        Crash(flight);
    }

    private void ProcessThrusters(Entity<BlackfootFlightComponent> flight)
    {
        if (!IsAirborneOrTransitioning(flight.Comp.State) ||
            HasFunctionalThrusters(flight.Owner))
        {
            return;
        }

        Crash(flight);
    }

    private float GetFuelDrain(BlackfootFlightComponent flight, BlackfootFuelPowerComponent fuel)
    {
        return flight.State switch
        {
            BlackfootFlightState.Idling => fuel.IdleFuelDrain,
            BlackfootFlightState.VTOL => fuel.VTOLFuelDrain,
            BlackfootFlightState.Flight => fuel.FlightFuelDrain,
            _ => 0f,
        };
    }

    private float GetFuelLeakDrain(Entity<BlackfootFlightComponent> flight, BlackfootFuelPowerComponent fuel)
    {
        if (fuel.FuelLeakDrain <= 0f ||
            fuel.Fuel <= 0f ||
            !TryComp(flight.Owner, out VehicleHardpointFailureComponent? failures) ||
            !_hardpoints.HasHardpointFailure(flight.Owner, VehicleHardpointFailure.FuelLeak, failures))
        {
            return 0f;
        }

        return fuel.FuelLeakDrain;
    }

    private bool CanStartTakeoff(Entity<BlackfootFlightComponent> flight, EntityUid pilot)
    {
        switch (flight.Comp.State)
        {
            case BlackfootFlightState.Idling:
                break;
            case BlackfootFlightState.Stowed:
                Popup(pilot, "Deploy the Blackfoot before takeoff.", PopupType.SmallCaution);
                return false;
            case BlackfootFlightState.Grounded:
                Popup(pilot, "Start the Blackfoot engines before takeoff.", PopupType.SmallCaution);
                return false;
            case BlackfootFlightState.TakingOff:
                Popup(pilot, "The Blackfoot is already taking off.", PopupType.SmallCaution);
                return false;
            case BlackfootFlightState.Crashed:
                Popup(pilot, "The Blackfoot is too damaged to take off.", PopupType.SmallCaution);
                return false;
            default:
                Popup(pilot, "The Blackfoot must be idling before takeoff.", PopupType.SmallCaution);
                return false;
        }

        if (HasTowConnection(flight.Owner))
        {
            Popup(pilot, "Disconnect towing gear before takeoff.", PopupType.SmallCaution);
            return false;
        }

        if (TryComp(flight, out BlackfootFuelPowerComponent? fuel) &&
            fuel.Fuel < fuel.MinimumTakeoffFuel)
        {
            Popup(pilot, "The Blackfoot does not have enough fuel for takeoff.", PopupType.SmallCaution);
            return false;
        }

        if (!HasFunctionalThrusters(flight.Owner))
        {
            Popup(pilot, "The Blackfoot needs functional thrusters before takeoff.", PopupType.SmallCaution);
            return false;
        }

        if (!HasMapOffset(flight.Owner, flight.Comp.AirborneMapOffset))
        {
            Popup(pilot, "No upper Z level is available for takeoff.", PopupType.SmallCaution);
            return false;
        }

        if (!TryValidateTakeoffFootprint(flight, out var reason))
        {
            Popup(pilot, reason, PopupType.SmallCaution);
            return false;
        }

        return true;
    }

    private bool CanStartLanding(Entity<BlackfootFlightComponent> flight, EntityUid pilot)
    {
        if (flight.Comp.State != BlackfootFlightState.VTOL)
        {
            var message = flight.Comp.State == BlackfootFlightState.Flight
                ? "Switch to VTOL mode before landing."
                : "The Blackfoot must be airborne in VTOL mode before landing.";

            Popup(pilot, message, PopupType.SmallCaution);
            return false;
        }

        if (!HasMapOffset(flight.Owner, flight.Comp.GroundMapOffset))
        {
            Popup(pilot, "No lower Z level is available for landing.", PopupType.SmallCaution);
            return false;
        }

        if (!TryValidateLandingFootprint(flight, out var reason))
        {
            Popup(pilot, reason, PopupType.SmallCaution);
            return false;
        }

        return true;
    }

    private void StartTimedTransition(
        Entity<BlackfootFlightComponent> flight,
        BlackfootFlightState state,
        TimeSpan duration)
    {
        SetState(flight.Owner, flight, state);
        flight.Comp.TransitionEndTime = _timing.CurTime + duration;
        Dirty(flight);
    }

    private void FinishTakeoff(Entity<BlackfootFlightComponent> flight)
    {
        if (!CanCompleteTakeoff(flight, out var reason) ||
            !_zLevels.TryMove(flight.Owner, flight.Comp.AirborneMapOffset))
        {
            SetState(flight.Owner, flight, BlackfootFlightState.Idling);
            PopupPilot(flight.Owner, reason ?? "Takeoff failed: the Blackfoot could not move to the upper Z level.", PopupType.SmallCaution);
            return;
        }

        flight.Comp.MovementMode = BlackfootMovementMode.VTOL;
        SetState(flight.Owner, flight, BlackfootFlightState.VTOL);
        SpawnShadow(flight);
        _viewToggle.RefreshOutsideViewers(flight.Owner);
        PopupPilot(flight.Owner, "Blackfoot airborne in VTOL mode.");
    }

    private void FinishLanding(Entity<BlackfootFlightComponent> flight)
    {
        if (!CanCompleteLanding(flight, out var reason) ||
            !_zLevels.TryMove(flight.Owner, flight.Comp.GroundMapOffset))
        {
            SetState(flight.Owner, flight, BlackfootFlightState.VTOL);
            PopupPilot(flight.Owner, reason ?? "Landing failed: the Blackfoot could not move to the lower Z level.", PopupType.SmallCaution);
            return;
        }

        DeleteShadow(flight);
        DeleteDownwash(flight);
        flight.Comp.MovementMode = BlackfootMovementMode.VTOL;
        SetState(flight.Owner, flight, BlackfootFlightState.Idling);
        _viewToggle.RefreshOutsideViewers(flight.Owner);
        PopupPilot(flight.Owner, "Blackfoot landed.");
    }

    private bool TryMoveAltitude(Entity<BlackfootFlightComponent> flight, EntityUid pilot, int offset)
    {
        if (flight.Comp.State is not (BlackfootFlightState.VTOL or BlackfootFlightState.Flight))
        {
            Popup(pilot, "Altitude can only be changed while the Blackfoot is airborne.", PopupType.SmallCaution);
            return false;
        }

        var xform = Transform(flight.Owner);
        if (xform.MapUid is not { } currentMap)
        {
            Popup(pilot, "The Blackfoot is not on a valid Z map.", PopupType.SmallCaution);
            return false;
        }

        if (!_zLevels.TryMapOffset(currentMap, offset, out var targetMap))
        {
            Popup(
                pilot,
                offset > 0
                    ? "There is no higher Z level for the Blackfoot to climb to."
                    : "There is no lower Z level for the Blackfoot to descend to.",
                PopupType.SmallCaution);
            return false;
        }

        if (offset < 0 &&
            (targetMap.Value.Comp.Depth == 0 || TryValidateLandingFootprint(flight, out _)))
        {
            Popup(pilot, "Use the landing sequence to descend to ground level.", PopupType.SmallCaution);
            return false;
        }

        if (!_zLevels.TryMove(flight.Owner, offset, currentMap))
        {
            Popup(pilot, "The Blackfoot could not change altitude.", PopupType.SmallCaution);
            return false;
        }

        _viewToggle.RefreshOutsideViewers(flight.Owner);
        Popup(pilot, offset > 0 ? "Blackfoot climbing one Z level." : "Blackfoot descending one Z level.");
        return true;
    }

    private void Crash(Entity<BlackfootFlightComponent> flight)
    {
        DeleteShadow(flight);
        DeleteDownwash(flight);

        flight.Comp.TransitionEndTime = TimeSpan.Zero;
        flight.Comp.MovementMode = BlackfootMovementMode.VTOL;

        if (IsAirborne(flight.Comp.State))
            _zLevels.TryMove(flight.Owner, flight.Comp.GroundMapOffset);

        _viewToggle.RefreshOutsideViewers(flight.Owner);
        SetState(flight.Owner, flight, BlackfootFlightState.Crashed);

        var ev = new BlackfootCrashedEvent();
        RaiseLocalEvent(flight.Owner, ref ev);
    }

    private void SpawnShadow(Entity<BlackfootFlightComponent> flight)
    {
        DeleteShadow(flight);

        var xform = Transform(flight.Owner);
        if (xform.MapUid is not { } map ||
            !TryProjectToGroundEffectMap(map, flight.Comp.GroundMapOffset, _transform.GetWorldPosition(flight.Owner), out var coords))
        {
            return;
        }

        var rotation = _transform.GetWorldRotation(flight.Owner);
        var shadow = Spawn(flight.Comp.ShadowPrototype, coords, rotation: rotation);
        var shadowComp = EnsureComp<BlackfootShadowComponent>(shadow);
        shadowComp.Aircraft = flight.Owner;
        shadowComp.ProjectedMapOffset = flight.Comp.GroundMapOffset;
        Dirty(shadow, shadowComp);

        flight.Comp.Shadow = shadow;
        Dirty(flight);
    }

    private void UpdateShadow(Entity<BlackfootShadowComponent> shadow)
    {
        if (shadow.Comp.Aircraft is not { } aircraft ||
            TerminatingOrDeleted(aircraft))
        {
            QueueDel(shadow);
            return;
        }

        var xform = Transform(aircraft);
        var rotation = _transform.GetWorldRotation(aircraft);
        if (xform.MapUid is not { } map ||
            !TryProjectToGroundEffectMap(map, shadow.Comp.ProjectedMapOffset, _transform.GetWorldPosition(aircraft), out var coords))
        {
            return;
        }

        _transform.SetMapCoordinates(shadow.Owner, coords);
        _transform.SetWorldRotation(shadow.Owner, rotation);
    }

    private void OnBlackfootDamageChanged(Entity<BlackfootFlightComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta?.AnyPositive() != true)
            return;

        Spawn(ent.Comp.HitEffect, Transform(ent.Owner).Coordinates);
    }

    private void DeleteShadow(Entity<BlackfootFlightComponent> flight)
    {
        if (flight.Comp.Shadow is { } shadow &&
            !TerminatingOrDeleted(shadow))
        {
            _viewToggle.ReplaceOutsideTarget(shadow, flight.Owner);
            QueueDel(shadow);
        }

        flight.Comp.Shadow = null;
        Dirty(flight);
    }

    private void UpdateProjectedDownwashState(Entity<BlackfootFlightComponent> flight)
    {
        if (ShouldShowProjectedDownwash(flight.Comp.State))
        {
            EnsureDownwash(flight);
            return;
        }

        DeleteDownwash(flight);
    }

    private void EnsureDownwash(Entity<BlackfootFlightComponent> flight)
    {
        if (flight.Comp.Downwash is { } existing &&
            !TerminatingOrDeleted(existing))
        {
            return;
        }

        SpawnDownwash(flight);
    }

    private void SpawnDownwash(Entity<BlackfootFlightComponent> flight)
    {
        DeleteDownwash(flight);

        if (!TryGetDownwashCoordinates(flight, out var coords, out var rotation))
        {
            return;
        }

        var downwash = Spawn(flight.Comp.DownwashPrototype, coords, rotation: rotation);
        var downwashComp = EnsureComp<BlackfootDownwashComponent>(downwash);
        downwashComp.Aircraft = flight.Owner;
        downwashComp.ProjectedMapOffset = flight.Comp.GroundMapOffset;
        Dirty(downwash, downwashComp);

        flight.Comp.Downwash = downwash;
        Dirty(flight);
    }

    private void UpdateDownwash(Entity<BlackfootDownwashComponent> downwash)
    {
        if (downwash.Comp.Aircraft is not { } aircraft ||
            TerminatingOrDeleted(aircraft))
        {
            QueueDel(downwash);
            return;
        }

        if (!TryComp(aircraft, out BlackfootFlightComponent? flight) ||
            !TryGetDownwashCoordinates((aircraft, flight), out var coords, out var rotation))
        {
            return;
        }

        _transform.SetMapCoordinates(downwash.Owner, coords);
        _transform.SetWorldRotation(downwash.Owner, rotation);
    }

    private bool TryGetDownwashCoordinates(
        Entity<BlackfootFlightComponent> flight,
        out MapCoordinates coords,
        out Angle rotation)
    {
        coords = default;
        rotation = _transform.GetWorldRotation(flight.Owner);

        var xform = Transform(flight.Owner);
        var worldPosition = _transform.GetWorldPosition(flight.Owner) + rotation.RotateVec(flight.Comp.DownwashOffset);

        if (flight.Comp.State == BlackfootFlightState.TakingOff)
        {
            if (xform.MapID == MapId.Nullspace)
                return false;

            coords = new MapCoordinates(worldPosition, xform.MapID);
            return true;
        }

        if (xform.MapUid is not { } map ||
            !TryProjectToGroundEffectMap(map, flight.Comp.GroundMapOffset, worldPosition, out coords))
        {
            return false;
        }

        return true;
    }

    private bool TryProjectToGroundEffectMap(
        Entity<CMUZLevelMapComponent?> sourceMap,
        int startOffset,
        Vector2 worldPosition,
        out MapCoordinates coords)
    {
        coords = default;

        if (startOffset >= 0)
            startOffset = -1;

        MapComponent? lowestMap = null;

        for (var offset = startOffset;
             _zLevels.TryMapOffset(sourceMap, offset, out var projectedMap, out var projectedMapComp);
             offset--)
        {
            lowestMap = projectedMapComp;

            if (!HasSolidProjectionTile(projectedMap.Value.Owner, worldPosition))
                continue;

            coords = new MapCoordinates(worldPosition, projectedMapComp.MapId);
            return true;
        }

        if (lowestMap == null)
            return false;

        coords = new MapCoordinates(worldPosition, lowestMap.MapId);
        return true;
    }

    private bool HasSolidProjectionTile(EntityUid mapUid, Vector2 worldPosition)
    {
        if (!TryComp(mapUid, out MapGridComponent? grid) ||
            !_map.TryGetTileRef(mapUid, grid, worldPosition, out var tileRef))
        {
            return false;
        }

        return !CMUZLevelOpeningCache.IsOpeningTile(tileRef.Tile, _tile);
    }

    private void DeleteDownwash(Entity<BlackfootFlightComponent> flight)
    {
        if (flight.Comp.Downwash is { } downwash &&
            !TerminatingOrDeleted(downwash))
        {
            QueueDel(downwash);
        }

        flight.Comp.Downwash = null;
        Dirty(flight);
    }

    private void OnBlackfootShutdown(Entity<BlackfootFlightComponent> ent, ref ComponentShutdown args)
    {
        DeleteShadow(ent);
        DeleteDownwash(ent);
        StopInteriorEngineLoop(ent.Owner);
    }

    private bool TryValidateTakeoffFootprint(Entity<BlackfootFlightComponent> flight, out string reason)
    {
        if (!TryValidateCurrentFootprint(
                flight,
                requireOpenAir: true,
                checkBlockers: false,
                out reason))
        {
            return false;
        }

        if (!TryValidateOffsetFootprint(
                flight,
                flight.Comp.AirborneMapOffset,
                requireOpenAir: false,
                checkBlockers: true,
                out reason))
        {
            return false;
        }

        return true;
    }

    private bool CanCompleteTakeoff(Entity<BlackfootFlightComponent> flight, out string? reason)
    {
        reason = null;

        if (!HasFunctionalThrusters(flight.Owner))
        {
            reason = "Takeoff failed: the Blackfoot needs functional thrusters.";
            return false;
        }

        if (!HasMapOffset(flight.Owner, flight.Comp.AirborneMapOffset))
        {
            reason = "Takeoff failed: no upper Z level is available.";
            return false;
        }

        if (!TryValidateTakeoffFootprint(flight, out var footprintReason))
        {
            reason = $"Takeoff failed: {footprintReason}";
            return false;
        }

        return true;
    }

    private bool TryValidateLandingFootprint(Entity<BlackfootFlightComponent> flight, out string reason)
    {
        return TryValidateOffsetFootprint(
            flight,
            flight.Comp.GroundMapOffset,
            requireOpenAir: true,
            checkBlockers: true,
            out reason);
    }

    private bool CanCompleteLanding(Entity<BlackfootFlightComponent> flight, out string? reason)
    {
        reason = null;

        if (!HasMapOffset(flight.Owner, flight.Comp.GroundMapOffset))
        {
            reason = "Landing failed: no lower Z level is available.";
            return false;
        }

        if (!TryValidateLandingFootprint(flight, out var footprintReason))
        {
            reason = $"Landing failed: {footprintReason}";
            return false;
        }

        return true;
    }

    private bool TryValidateCurrentFootprint(
        Entity<BlackfootFlightComponent> flight,
        bool requireOpenAir,
        bool checkBlockers,
        out string reason)
    {
        reason = string.Empty;

        var xform = Transform(flight.Owner);
        if (xform.MapUid is not { } mapUid ||
            !TryComp(mapUid, out MapGridComponent? grid))
        {
            reason = "The Blackfoot is not on a valid Z map.";
            return false;
        }

        var worldPosition = _transform.GetWorldPosition(flight.Owner);
        return TryValidateFootprintOnMap(
            mapUid,
            grid,
            worldPosition,
            flight.Comp.Footprint,
            flight.Comp.FootprintOffsets,
            allowEmptyTiles: false,
            requireOpenAir,
            checkBlockers,
            out reason);
    }

    private bool TryValidateOffsetFootprint(
        Entity<BlackfootFlightComponent> flight,
        int offset,
        bool requireOpenAir,
        bool checkBlockers,
        out string reason)
    {
        reason = string.Empty;

        var xform = Transform(flight.Owner);
        if (xform.MapUid is not { } mapUid ||
            !_zLevels.TryMapOffset(mapUid, offset, out var targetMap))
        {
            reason = offset > 0
                ? "No upper Z level is available for takeoff."
                : "No lower Z level is available for landing.";
            return false;
        }

        var allowEmptyTiles = offset > 0 && !requireOpenAir;
        if (!TryComp(targetMap.Value.Owner, out MapGridComponent? targetGrid))
        {
            if (allowEmptyTiles)
                return true;

            reason = offset > 0
                ? "No upper Z map grid is available for takeoff."
                : "No lower Z map grid is available for landing.";
            return false;
        }

        var worldPosition = _transform.GetWorldPosition(flight.Owner);
        return TryValidateFootprintOnMap(
            targetMap.Value.Owner,
            targetGrid,
            worldPosition,
            flight.Comp.Footprint,
            flight.Comp.FootprintOffsets,
            allowEmptyTiles,
            requireOpenAir,
            checkBlockers,
            out reason);
    }

    private bool TryValidateFootprintOnMap(
        EntityUid mapUid,
        MapGridComponent grid,
        Vector2 worldPosition,
        Vector2i footprint,
        List<Vector2i> footprintOffsets,
        bool allowEmptyTiles,
        bool requireOpenAir,
        bool checkBlockers,
        out string reason)
    {
        reason = string.Empty;

        if (!_map.TryGetTileRef(mapUid, grid, worldPosition, out var centerTile))
        {
            if (allowEmptyTiles)
                return true;

            reason = "The Blackfoot footprint center is outside valid map tiles.";
            return false;
        }

        if (footprintOffsets.Count > 0)
        {
            foreach (var offset in footprintOffsets)
            {
                if (!TryValidateFootprintTile(mapUid, grid, centerTile.GridIndices + offset, offset, allowEmptyTiles, requireOpenAir, checkBlockers, out reason))
                    return false;
            }

            return true;
        }

        var halfX = Math.Max(0, footprint.X / 2);
        var halfY = Math.Max(0, footprint.Y / 2);

        for (var x = -halfX; x <= halfX; x++)
        {
            for (var y = -halfY; y <= halfY; y++)
            {
                var tile = new Vector2i(centerTile.GridIndices.X + x, centerTile.GridIndices.Y + y);
                if (!TryValidateFootprintTile(mapUid, grid, tile, new Vector2i(x, y), allowEmptyTiles, requireOpenAir, checkBlockers, out reason))
                    return false;
            }
        }

        return true;
    }

    private bool TryValidateFootprintTile(
        EntityUid mapUid,
        MapGridComponent grid,
        Vector2i tile,
        Vector2i offset,
        bool allowEmptyTiles,
        bool requireOpenAir,
        bool checkBlockers,
        out string reason)
    {
        reason = string.Empty;

        if (!_map.TryGetTileRef(mapUid, grid, tile, out var tileRef) ||
            tileRef.Tile.IsEmpty)
        {
            if (allowEmptyTiles)
                return true;

            reason = $"The Blackfoot footprint is outside valid map tiles at offset {offset.X},{offset.Y}.";
            return false;
        }

        if (checkBlockers && _turf.IsTileBlocked(tileRef, FootprintBlockMask))
        {
            reason = $"The Blackfoot footprint is blocked at offset {offset.X},{offset.Y}.";
            return false;
        }

        if (requireOpenAir &&
            TryGetAirspaceBlockReason(new EntityCoordinates(mapUid, new Vector2(tile.X + 0.5f, tile.Y + 0.5f)), out var airspaceReason))
        {
            reason = $"{airspaceReason} Offset {offset.X},{offset.Y}.";
            return false;
        }

        return true;
    }

    private bool TryGetAirspaceBlockReason(EntityCoordinates coordinates, out string reason)
    {
        reason = string.Empty;

        if (!_area.TryGetArea(coordinates, out _, out _))
        {
            reason = "The Blackfoot footprint has no area data.";
            return true;
        }

        if (!_area.CanOrbitalBombard(coordinates, out var roofed))
        {
            reason = roofed
                ? "The Blackfoot footprint is blocked by roofing."
                : "The Blackfoot footprint is outside open-air bombardment permissions.";
            return true;
        }

        if (!_area.CanCAS(coordinates))
        {
            reason = "The Blackfoot footprint is outside close-air-support airspace.";
            return true;
        }

        if (!_area.CanSupplyDrop(_transform.ToMapCoordinates(coordinates)))
        {
            reason = "The Blackfoot footprint is outside supply-drop airspace.";
            return true;
        }

        if (!_area.CanMortarFire(coordinates))
        {
            reason = "The Blackfoot footprint is outside mortar-fire airspace.";
            return true;
        }

        if (!_area.CanMortarPlacement(coordinates))
        {
            reason = "The Blackfoot footprint is outside mortar-placement airspace.";
            return true;
        }

        if (!_area.CanLase(coordinates))
        {
            reason = "The Blackfoot footprint is outside lasing airspace.";
            return true;
        }

        if (!_area.CanMedevac(coordinates))
        {
            reason = "The Blackfoot footprint is outside medevac airspace.";
            return true;
        }

        if (!_area.CanParadrop(coordinates))
        {
            reason = "The Blackfoot footprint is outside paradrop airspace.";
            return true;
        }

        return false;
    }

    private bool HasFunctionalThrusters(EntityUid vehicle)
    {
        if (!TryComp(vehicle, out HardpointSlotsComponent? hardpoints))
            return false;

        if (!TryComp(vehicle, out ItemSlotsComponent? itemSlots))
            return false;

        foreach (var slot in hardpoints.Slots)
        {
            if (!string.Equals(slot.Id, ThrustersSlotId, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(slot.HardpointType, "Thruster", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!_itemSlots.TryGetSlot(vehicle, slot.Id, out var itemSlot, itemSlots) ||
                itemSlot.Item is not { } thrusters)
            {
                return false;
            }

            return !TryComp(thrusters, out HardpointIntegrityComponent? integrity) || integrity.Integrity > 0f;
        }

        return false;
    }

    private bool HasMapOffset(EntityUid uid, int offset)
    {
        if (Transform(uid).MapUid is not { } map)
            return false;

        return _zLevels.TryMapOffset(map, offset, out _);
    }

    private bool HasTowConnection(EntityUid vehicle)
    {
        return TryComp(vehicle, out BlackfootTowComponent? tow) &&
            (tow.TowVehicle != null || tow.TowedEntity != null);
    }

    private void SetState(
        EntityUid uid,
        Entity<BlackfootFlightComponent> flight,
        BlackfootFlightState state)
    {
        if (flight.Comp.State == state)
            return;

        var old = flight.Comp.State;
        flight.Comp.PreviousState = old;
        flight.Comp.State = state;
        flight.Comp.StateStartedAt = _timing.CurTime;
        if (state is not (BlackfootFlightState.TakingOff or BlackfootFlightState.Landing))
            flight.Comp.TransitionEndTime = TimeSpan.Zero;

        Dirty(uid, flight.Comp);

        var ev = new BlackfootStateChangedEvent(old, state);
        RaiseLocalEvent(uid, ref ev);

        PlayStateSound(uid, old, state);

        if (IsAirborne(old) != IsAirborne(state))
        {
            var airborneEv = new BlackfootAirborneChangedEvent(IsAirborne(state));
            RaiseLocalEvent(uid, ref airborneEv);
        }

        UpdateEngineAudio(flight);
        RefreshPilotActions(uid, flight);
    }

    private void UpdateEngineAudio(Entity<BlackfootFlightComponent> flight)
    {
        UpdateExteriorEngineAmbience(flight);
        UpdateInteriorEngineLoop(flight);
    }

    private void UpdateExteriorEngineAmbience(Entity<BlackfootFlightComponent> flight)
    {
        if (!TryComp(flight.Owner, out AmbientSoundComponent? ambient))
            return;

        var sound = GetExteriorEngineLoopSound(flight.Owner, flight.Comp.State);
        if (sound == null)
        {
            _ambient.SetAmbience(flight.Owner, false, ambient);
            return;
        }

        _ambient.SetSound(flight.Owner, sound, ambient);
        _ambient.SetAmbience(flight.Owner, true, ambient);
    }

    private void UpdateInteriorEngineLoop(Entity<BlackfootFlightComponent> flight)
    {
        if (!TryComp(flight.Owner, out BlackfootSoundComponent? sounds))
            return;

        var sound = GetInteriorEngineLoopSound(sounds, flight.Comp.State);
        var recipients = GetBlackfootInteriorSoundRecipients(flight.Owner);
        if (sound == null || recipients.Count == 0)
        {
            StopInteriorEngineLoop(flight.Owner, sounds);
            return;
        }

        var soundKey = GetSoundKey(sound);
        if (sounds.InteriorEngineLoopStream != null &&
            sounds.InteriorEngineLoopSoundKey == soundKey &&
            sounds.InteriorEngineLoopRecipients.SetEquals(recipients))
        {
            return;
        }

        StopInteriorEngineLoop(flight.Owner, sounds);

        var filter = Filter.Empty();
        foreach (var recipient in recipients)
        {
            AddInteriorSoundRecipient(filter, recipient);
        }

        if (filter.Count == 0)
            return;

        sounds.InteriorEngineLoopStream = _audio.PlayGlobal(sound, filter, true, InteriorEngineLoopParams)?.Entity;
        sounds.InteriorEngineLoopSoundKey = soundKey;
        sounds.InteriorEngineLoopRecipients.UnionWith(recipients);
    }

    private void StopInteriorEngineLoop(EntityUid uid, BlackfootSoundComponent? sounds = null)
    {
        if (!Resolve(uid, ref sounds, false))
            return;

        sounds.InteriorEngineLoopStream = _audio.Stop(sounds.InteriorEngineLoopStream);
        sounds.InteriorEngineLoopSoundKey = null;
        sounds.InteriorEngineLoopRecipients.Clear();
    }

    private SoundSpecifier? GetExteriorEngineLoopSound(EntityUid uid, BlackfootFlightState state)
    {
        if (!TryComp(uid, out BlackfootSoundComponent? sounds))
            return null;

        return state switch
        {
            BlackfootFlightState.Idling => sounds.EngineIdleLoopSound,
            BlackfootFlightState.TakingOff or
                BlackfootFlightState.VTOL or
                BlackfootFlightState.Flight or
                BlackfootFlightState.Landing => sounds.ExteriorFlightLoopSound ?? sounds.EngineIdleLoopSound,
            _ => null,
        };
    }

    private static SoundSpecifier? GetInteriorEngineLoopSound(BlackfootSoundComponent sounds, BlackfootFlightState state)
    {
        return state switch
        {
            BlackfootFlightState.Idling => sounds.EngineIdleLoopSound,
            BlackfootFlightState.TakingOff or
                BlackfootFlightState.VTOL or
                BlackfootFlightState.Flight or
                BlackfootFlightState.Landing => sounds.InteriorFlightLoopSound ?? sounds.ExteriorFlightLoopSound ?? sounds.EngineIdleLoopSound,
            _ => null,
        };
    }

    private static string GetSoundKey(SoundSpecifier sound)
    {
        return sound switch
        {
            SoundPathSpecifier path => path.Path.ToString(),
            SoundCollectionSpecifier collection => collection.Collection ?? string.Empty,
            _ => sound.ToString() ?? string.Empty,
        };
    }

    private void PlayStateSound(EntityUid uid, BlackfootFlightState oldState, BlackfootFlightState newState)
    {
        if (!TryComp(uid, out BlackfootSoundComponent? sounds))
            return;

        var sound = newState switch
        {
            BlackfootFlightState.Idling when oldState == BlackfootFlightState.Grounded => sounds.EngineStartupSound,
            BlackfootFlightState.Grounded when oldState == BlackfootFlightState.Idling => sounds.EngineShutdownSound,
            BlackfootFlightState.Stowed or BlackfootFlightState.Grounded => sounds.MechanicalSound,
            BlackfootFlightState.TakingOff => sounds.TakeoffSound,
            BlackfootFlightState.Landing => sounds.LandingSound,
            BlackfootFlightState.Flight when oldState == BlackfootFlightState.VTOL => sounds.FlightTransitionSound,
            BlackfootFlightState.VTOL when oldState == BlackfootFlightState.Flight => sounds.FlightTransitionSound,
            _ => null,
        };

        if (sound is { } stateSound)
            PlayBlackfootSound(uid, stateSound);
    }

    private void PlayBlackfootSound(EntityUid uid, SoundSpecifier sound)
    {
        _audio.PlayPvs(sound, uid);

        var interiorFilter = GetInteriorSoundFilter(uid);
        if (interiorFilter.Count > 0)
            _audio.PlayGlobal(sound, interiorFilter, true);
    }

    private Filter GetInteriorSoundFilter(EntityUid vehicle)
    {
        var filter = Filter.Empty();
        foreach (var recipient in GetBlackfootInteriorSoundRecipients(vehicle))
        {
            AddInteriorSoundRecipient(filter, recipient);
        }

        return filter;
    }

    private HashSet<EntityUid> GetBlackfootInteriorSoundRecipients(EntityUid vehicle)
    {
        var recipients = new HashSet<EntityUid>();
        if (!TryComp(vehicle, out VehicleInteriorComponent? interior))
            return recipients;

        foreach (var passenger in interior.Passengers)
        {
            recipients.Add(passenger);
        }

        foreach (var xeno in interior.Xenos)
        {
            recipients.Add(xeno);
        }

        if (TryComp(vehicle, out VehicleComponent? vehicleComp) &&
            vehicleComp.Operator is { } pilot)
        {
            recipients.Add(pilot);
        }

        if (TryComp(vehicle, out VehicleWeaponsComponent? weapons))
        {
            if (weapons.Operator is { } operatorUid)
                recipients.Add(operatorUid);

            foreach (var selectedOperator in weapons.OperatorSelections.Keys)
            {
                recipients.Add(selectedOperator);
            }

            foreach (var hardpointOperator in weapons.HardpointOperators.Values)
            {
                recipients.Add(hardpointOperator);
            }
        }

        return recipients;
    }

    private void AddInteriorSoundRecipient(Filter filter, EntityUid recipient)
    {
        if (TerminatingOrDeleted(recipient))
            return;

        if (TryComp(recipient, out ActorComponent? actor))
            filter.AddPlayer(actor.PlayerSession);
    }

    private void RefreshPilotActions(EntityUid vehicleUid, Entity<BlackfootFlightComponent> flight)
    {
        if (!TryComp(vehicleUid, out VehicleComponent? vehicle) ||
            vehicle.Operator is not { } pilot ||
            !TryComp(pilot, out BlackfootPilotActionComponent? actions) ||
            actions.Vehicle != vehicleUid)
        {
            return;
        }

        UpdatePilotActions(pilot, actions, flight);
    }

    private bool TryGetPilotedBlackfoot(
        Entity<BlackfootPilotActionComponent> ent,
        EntityUid performer,
        out EntityUid vehicle,
        out Entity<BlackfootFlightComponent> flight)
    {
        vehicle = default;
        flight = default;

        if (performer != ent.Owner ||
            ent.Comp.Vehicle is not { } vehicleUid ||
            !TryComp(vehicleUid, out BlackfootFlightComponent? flightComp))
        {
            return false;
        }

        if (!TryComp(vehicleUid, out VehicleComponent? vehicleComp) ||
            vehicleComp.Operator != performer)
        {
            Popup(performer, "Only the Blackfoot pilot can use this control.", PopupType.SmallCaution);
            return false;
        }

        vehicle = vehicleUid;
        flight = (vehicleUid, flightComp);
        return true;
    }

    private void UpdatePilotActions(
        EntityUid pilot,
        BlackfootPilotActionComponent actions,
        Entity<BlackfootFlightComponent> flight)
    {
        SetActionEnabled(
            actions.EngineToggleAction,
            flight.Comp.State is BlackfootFlightState.Stowed or BlackfootFlightState.Grounded or BlackfootFlightState.Idling);
        SetActionToggled(actions.EngineToggleAction, flight.Comp.State != BlackfootFlightState.Grounded && flight.Comp.State != BlackfootFlightState.Stowed && flight.Comp.State != BlackfootFlightState.Crashed);

        SetActionEnabled(actions.TakeoffAction, true);
        SetActionEnabled(actions.LandAction, true);

        SetActionEnabled(actions.FlightModeToggleAction, flight.Comp.State is BlackfootFlightState.VTOL or BlackfootFlightState.Flight);
        SetActionToggled(actions.FlightModeToggleAction, flight.Comp.State == BlackfootFlightState.Flight);

        SetActionEnabled(actions.RearDoorToggleAction, HasComp<BlackfootRearDoorComponent>(flight.Owner));
        SetActionToggled(actions.RearDoorToggleAction, TryComp(flight.Owner, out BlackfootRearDoorComponent? rearDoor) && rearDoor.Open);

        SetActionEnabled(actions.StowToggleAction, flight.Comp.State is BlackfootFlightState.Grounded or BlackfootFlightState.Stowed);
        SetActionToggled(actions.StowToggleAction, flight.Comp.State == BlackfootFlightState.Stowed);

        var canChangeAltitude = flight.Comp.State is BlackfootFlightState.VTOL or BlackfootFlightState.Flight;
        SetActionEnabled(actions.AscendZLevelAction, canChangeAltitude);
        SetActionEnabled(actions.DescendZLevelAction, canChangeAltitude);

        Dirty(pilot, actions);
    }

    private void SetActionEnabled(EntityUid? action, bool enabled)
    {
        if (action is { } actionUid)
            _actions.SetEnabled(actionUid, enabled);
    }

    private void SetActionToggled(EntityUid? action, bool toggled)
    {
        if (action is { } actionUid)
            _actions.SetToggled(actionUid, toggled);
    }

    private void Popup(EntityUid pilot, string message, PopupType type = PopupType.Small)
    {
        _popup.PopupCursor(message, pilot, type);
    }

    private void PopupPilot(EntityUid vehicle, string message, PopupType type = PopupType.Small)
    {
        if (!TryComp(vehicle, out VehicleComponent? vehicleComp) ||
            vehicleComp.Operator is not { } pilot)
        {
            return;
        }

        Popup(pilot, message, type);
    }

    private static bool IsAirborneOrTransitioning(BlackfootFlightState state)
    {
        return state is
            BlackfootFlightState.TakingOff or
            BlackfootFlightState.VTOL or
            BlackfootFlightState.Flight or
            BlackfootFlightState.Landing;
    }

    private static bool IsAirborne(BlackfootFlightState state)
    {
        return state is
            BlackfootFlightState.VTOL or
            BlackfootFlightState.Flight or
            BlackfootFlightState.Landing;
    }

    private static bool ShouldShowProjectedDownwash(BlackfootFlightState state)
    {
        return state is
            BlackfootFlightState.TakingOff or
            BlackfootFlightState.VTOL or
            BlackfootFlightState.Landing;
    }

    private static bool ShouldPlayEngineAmbience(BlackfootFlightState state)
    {
        return state is
            BlackfootFlightState.Idling or
            BlackfootFlightState.TakingOff or
            BlackfootFlightState.VTOL or
            BlackfootFlightState.Flight or
            BlackfootFlightState.Landing;
    }
}
