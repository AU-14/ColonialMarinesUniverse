using System;
using Content.Shared._CMU14.Blackfoot;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Actions;
using Content.Shared.Buckle.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Server._CMU14.Blackfoot;

public sealed partial class BlackfootDoorGunSystem : EntitySystem
{
    private const string DoorGunHardpointType = "DoorGun";

    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private VehicleSystem _vehicle = default!;
    [Dependency] private VehicleViewToggleSystem _viewToggle = default!;
    [Dependency] private CMUZLevelShootingSystem _zShooting = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BlackfootDoorGunSeatComponent, StrappedEvent>(OnWeaponSeatStrapped);
        SubscribeLocalEvent<BlackfootDoorGunSeatComponent, UnstrappedEvent>(OnWeaponSeatUnstrapped);
        SubscribeLocalEvent<BlackfootDoorGunActionComponent, VehicleHardpointSelectActionEvent>(OnHardpointActionSelected, after: [typeof(VehicleWeaponsSystem)]);
        SubscribeLocalEvent<BlackfootDoorGunActionComponent, ShotAttemptedEvent>(OnDoorGunShotAttempted);
        SubscribeLocalEvent<BlackfootDoorGunActionComponent, BlackfootDoorGunZModeToggleActionEvent>(OnZModeToggle);
        SubscribeLocalEvent<BlackfootDoorGunActionComponent, ComponentShutdown>(OnDoorGunActionsShutdown);
    }

    private void OnWeaponSeatStrapped(Entity<BlackfootDoorGunSeatComponent> ent, ref StrappedEvent args)
    {
        if (!IsBlackfootDoorGunSeat(ent, out var vehicle))
            return;

        var user = args.Buckle.Owner;
        var actions = EnsureComp<BlackfootDoorGunActionComponent>(user);
        actions.Vehicle = vehicle;
        actions.ZModeToggleAction ??= _actions.AddAction(user, actions.ZModeToggleActionId);

        UpdateZModeAction(user, actions);
        Dirty(user, actions);

        if (vehicle is { } vehicleUid)
            _viewToggle.SetOutsideView(user, vehicleUid);
    }

    private void OnWeaponSeatUnstrapped(Entity<BlackfootDoorGunSeatComponent> ent, ref UnstrappedEvent args)
    {
        var user = args.Buckle.Owner;

        if (TryComp(user, out BlackfootDoorGunActionComponent? actions))
        {
            RemoveAction(user, ref actions.ZModeToggleAction);
            actions.Vehicle = null;
            RemCompDeferred<BlackfootDoorGunActionComponent>(user);
        }

        _zShooting.SetShootDown(user, false);
    }

    private void OnHardpointActionSelected(Entity<BlackfootDoorGunActionComponent> ent, ref VehicleHardpointSelectActionEvent args)
    {
        if (args.Performer != ent.Owner)
            return;

        if (ent.Comp.Vehicle is { } vehicle && IsUsingDoorGun(ent.Owner, vehicle))
            _viewToggle.SetOutsideView(ent.Owner, vehicle);
        else
            _zShooting.SetShootDown(ent.Owner, false);

        UpdateZModeAction(ent.Owner, ent.Comp);
    }

    private void OnDoorGunShotAttempted(Entity<BlackfootDoorGunActionComponent> ent, ref ShotAttemptedEvent args)
    {
        if (args.User != ent.Owner ||
            ent.Comp.Vehicle is not { } vehicle ||
            !IsUsingDoorGun(ent.Owner, vehicle))
        {
            return;
        }

        if (TryComp(vehicle, out BlackfootRearDoorComponent? rearDoor) && rearDoor.Open)
            return;

        args.Cancel();
        _popup.PopupCursor("Open the rear door before firing the M866.", ent.Owner, PopupType.SmallCaution);
    }

    private void OnDoorGunActionsShutdown(Entity<BlackfootDoorGunActionComponent> ent, ref ComponentShutdown args)
    {
        RemoveAction(ent.Owner, ref ent.Comp.ZModeToggleAction);
    }

    private void OnZModeToggle(Entity<BlackfootDoorGunActionComponent> ent, ref BlackfootDoorGunZModeToggleActionEvent args)
    {
        if (args.Handled || args.Performer != ent.Owner)
            return;

        args.Handled = true;

        if (ent.Comp.Vehicle is not { } vehicle ||
            !HasComp<BlackfootFlightComponent>(vehicle) ||
            !IsUsingDoorGun(ent.Owner, vehicle))
        {
            _popup.PopupCursor("Select the M866 automatic grenade launcher first.", ent.Owner, PopupType.SmallCaution);
            UpdateZModeAction(ent.Owner, ent.Comp);
            return;
        }

        _viewToggle.SetOutsideView(ent.Owner, vehicle);

        var shootDown = !_zShooting.IsShootDownEnabled(ent.Owner);
        _zShooting.SetShootDown(ent.Owner, shootDown);

        UpdateZModeAction(ent.Owner, ent.Comp);

        var message = shootDown
            ? "M866 set to fire one Z-level below."
            : "M866 set to fire on the current Z-level.";

        _popup.PopupCursor(message, ent.Owner);
    }

    private bool IsBlackfootDoorGunSeat(Entity<BlackfootDoorGunSeatComponent> seat, out EntityUid? vehicle)
    {
        vehicle = null;

        if (!_vehicle.TryGetVehicleFromInterior(seat.Owner, out vehicle) ||
            vehicle is not { } vehicleUid ||
            !HasComp<BlackfootFlightComponent>(vehicleUid))
        {
            vehicle = null;
            return false;
        }

        return true;
    }

    private bool IsUsingDoorGun(EntityUid user, EntityUid vehicle)
    {
        if (!TryComp(user, out VehicleWeaponsOperatorComponent? weaponsOperator) ||
            weaponsOperator.Vehicle != vehicle ||
            weaponsOperator.SelectedWeapon is not { } selected ||
            !TryComp(selected, out HardpointItemComponent? hardpoint))
        {
            return false;
        }

        return string.Equals(hardpoint.HardpointType, DoorGunHardpointType, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateZModeAction(EntityUid user, BlackfootDoorGunActionComponent actions)
    {
        if (actions.ZModeToggleAction is not { } action)
            return;

        var enabled = actions.Vehicle is { } vehicle && IsUsingDoorGun(user, vehicle);
        var toggled = _zShooting.IsShootDownEnabled(user);
        _actions.SetEnabled(action, enabled);
        _actions.SetToggled(action, toggled);
    }

    private void RemoveAction(EntityUid user, ref EntityUid? action)
    {
        if (action is not { } actionUid)
            return;

        _actions.RemoveAction(user, actionUid);
        action = null;
    }
}
