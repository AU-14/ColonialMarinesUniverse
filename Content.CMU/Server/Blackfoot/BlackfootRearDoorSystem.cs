using System;
using Content.Server._CMU14.ZLevels.Core;
using Content.Shared._CMU14.Blackfoot;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Vehicle.Viewport;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Localization;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._CMU14.Blackfoot;

public sealed partial class BlackfootRearDoorSystem : EntitySystem
{
    private const float AirborneExitLocalPosition = 0.95f;
    private const float AirborneExitVelocity = -8f;

    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private VehicleSystem _vehicle = default!;
    [Dependency] private VehicleViewToggleSystem _viewToggle = default!;
    [Dependency] private CMUZLevelsSystem _zLevels = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlackfootRearDoorControlComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<BlackfootLookOutsideComponent, GetVerbsEvent<AlternativeVerb>>(OnLookOutsideVerb);
        SubscribeLocalEvent<BlackfootRearDoorComponent, VehicleEntryAttemptEvent>(OnEntryAttempt);
        SubscribeLocalEvent<BlackfootRearDoorComponent, VehicleExitAttemptEvent>(OnExitAttempt);
        SubscribeLocalEvent<BlackfootRearDoorComponent, VehicleExitedEvent>(OnExited);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BlackfootRearDoorVisualsComponent>();
        while (query.MoveNext(out var uid, out var visuals))
        {
            if (!_vehicle.TryGetVehicleFromInterior(uid, out var vehicle) ||
                vehicle is not { } vehicleUid ||
                !TryComp(vehicleUid, out BlackfootRearDoorComponent? rearDoor) ||
                visuals.Open == rearDoor.Open)
            {
                continue;
            }

            visuals.Open = rearDoor.Open;
            Dirty(uid, visuals);
        }
    }

    private void OnActivate(Entity<BlackfootRearDoorControlComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (!_vehicle.TryGetVehicleFromInterior(ent.Owner, out var vehicle) ||
            vehicle is not { } vehicleUid ||
            !TryComp(vehicleUid, out BlackfootRearDoorComponent? rearDoor))
        {
            _popup.PopupEntity("This control is not linked to a Blackfoot rear door.", args.User, args.User, PopupType.SmallCaution);
            return;
        }

        rearDoor.Open = !rearDoor.Open;
        Dirty(vehicleUid, rearDoor);

        _popup.PopupEntity(rearDoor.Open ? "Rear door opened." : "Rear door closed.", args.User, args.User);
        args.Handled = true;
    }

    private void OnLookOutsideVerb(Entity<BlackfootLookOutsideComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract ||
            !args.CanAccess ||
            args.Using != null ||
            !_vehicle.TryGetVehicleFromInterior(ent.Owner, out var vehicle) ||
            vehicle is not { } vehicleUid ||
            !HasComp<BlackfootFlightComponent>(vehicleUid))
        {
            return;
        }

        var user = args.User;
        var source = ent.Owner;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-blackfoot-look-outside"),
            Act = () => ToggleLookOutside(user, vehicleUid, source),
        });
    }

    private void ToggleLookOutside(EntityUid user, EntityUid vehicle, EntityUid source)
    {
        if (TryComp(user, out VehicleViewportUserComponent? existing))
        {
            var sameSource = existing.Source == source;
            CloseLookOutside(user, existing);

            if (sameSource)
                return;
        }

        var userState = EnsureComp<VehicleViewportUserComponent>(user);
        if (TryComp(user, out EyeComponent? eye))
            userState.PreviousTarget = eye.Target;

        userState.Source = source;
        _zLevels.EnsureZLevelViewer(vehicle);
        _eye.SetTarget(user, vehicle);
        _viewToggle.EnableViewToggle(user, vehicle, source, userState.PreviousTarget, isOutside: true);
        Dirty(user, userState);
    }

    private void CloseLookOutside(EntityUid user, VehicleViewportUserComponent state)
    {
        if (TryComp(user, out EyeComponent? eye))
            _eye.SetTarget(user, state.PreviousTarget, eye);

        if (state.Source is { } source)
            _viewToggle.DisableViewToggle(user, source);

        if (state.PeekTarget is { } peekTarget && Exists(peekTarget))
            QueueDel(peekTarget);

        RemCompDeferred<VehicleViewportUserComponent>(user);
    }

    private void OnEntryAttempt(Entity<BlackfootRearDoorComponent> ent, ref VehicleEntryAttemptEvent args)
    {
        if (ent.Comp.Open || args.EntryIndex != ent.Comp.RearEntryIndex)
            return;

        _popup.PopupEntity("Open the rear door before boarding from the back.", args.User, args.User, PopupType.SmallCaution);
        args.Cancelled = true;
    }

    private void OnExitAttempt(Entity<BlackfootRearDoorComponent> ent, ref VehicleExitAttemptEvent args)
    {
        if (!HasComp<BlackfootRearDoorVisualsComponent>(args.Exit))
            return;

        if (!ent.Comp.Open)
        {
            _popup.PopupEntity("Open the rear door before exiting from the back.", args.User, args.User, PopupType.SmallCaution);
            args.Cancelled = true;
            return;
        }

        if (!TryComp(ent.Owner, out BlackfootFlightComponent? flight) ||
            !IsAirborneExitState(flight.State) ||
            ent.Comp.AllowAirborneExit)
        {
            return;
        }

        _popup.PopupEntity("The Blackfoot is moving too fast to jump out.", args.User, args.User, PopupType.SmallCaution);
        args.Cancelled = true;
    }

    private void OnExited(Entity<BlackfootRearDoorComponent> ent, ref VehicleExitedEvent args)
    {
        if (!ent.Comp.Open ||
            !ent.Comp.AllowAirborneExit ||
            !HasComp<BlackfootRearDoorVisualsComponent>(args.Exit) ||
            !TryComp(ent.Owner, out BlackfootFlightComponent? flight) ||
            !IsAirborneExitState(flight.State))
        {
            return;
        }

        StartAirborneExitFall(args.User);
    }

    private void StartAirborneExitFall(EntityUid user)
    {
        if (!TryComp(user, out CMUZPhysicsComponent? zPhysics) ||
            !TryComp(user, out PhysicsComponent? physics))
        {
            return;
        }

        _physics.SetBodyStatus(user, physics, BodyStatus.InAir);
        _zLevels.SetZLocalPosition((user, zPhysics), MathF.Max(zPhysics.LocalPosition, AirborneExitLocalPosition));
        _zLevels.SetZVelocity((user, zPhysics), MathF.Min(zPhysics.Velocity, AirborneExitVelocity));
    }

    private static bool IsAirborneExitState(BlackfootFlightState state)
    {
        return state is
            BlackfootFlightState.VTOL or
            BlackfootFlightState.Flight or
            BlackfootFlightState.Landing;
    }
}
