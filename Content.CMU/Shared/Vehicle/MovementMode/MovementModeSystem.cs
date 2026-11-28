using System;
using Content.Shared._RMC14.Input;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Actions;
using Content.Shared.Vehicle;
using Content.Shared.CMU14.Vehicle.Open;

namespace Content.Shared.CMU14.Vehicle;

public sealed partial class VehicleMovementModeSystem : EntitySystem
{
public override void Initialize()
{
    SubscribeLocalEvent<VehicleMovementActionHolderComponent, VehicleStartUpShutDownEvent>(SwitchEngine);
    SubscribeLocalEvent<VehicleMovementActionHolderComponent, VehicleGearShiftUpEvent>(ShiftUp);
    SubscribeLocalEvent<VehicleMovementActionHolderComponent, VehicleGearShiftDownEvent>(ShiftDown);
    SubscribeLocalEvent<VehicleMovementModeComponent, VehicleCanRunEvent>(OnCanRun);
}

    [Dependency] private INetManager _net = default!;
    [Dependency] private VehicleSystem _rmcVehicles = default!;

    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private Content.Shared.Vehicle.Systems.VehicleSystem _vehicleSystem = default!;

    [Dependency] private VehicleWeaponsSystem _vehicleWeapons = default!;
    [Dependency] private CMUVehicleEngineSwitchSystem _vehicleOpenEngineSwitch = default!;
    [Dependency] private VehiclePowerSourceSharedSystem _FuelSystem = default!;

    private void SwitchEngine(Entity<VehicleMovementActionHolderComponent> holder, ref VehicleStartUpShutDownEvent args)
    {
        if (_net.IsClient || args.Handled || args.Performer != holder.Owner)
            return;

        if (!_rmcVehicles.TryResolveControlledVehicle(holder.Owner, out var vehicle))
            return;

        if (!TryComp(vehicle, out VehicleComponent? vehicleComponent) ||
            vehicleComponent.Operator != holder.Owner)
            return;

        if (!TryComp(vehicle, out VehicleMovementModeComponent? mode))
            return;

        if (!TryComp(vehicle, out VehiclePowerSourceTankComponent? fueltank))
            return;

        args.Handled = true;
        _FuelSystem.SwitchEngine((vehicle, fueltank));
    }

    private void ShiftUp(Entity<VehicleMovementActionHolderComponent> holder, ref VehicleGearShiftUpEvent args)
    {
        if (_net.IsClient || args.Handled || args.Performer != holder.Owner)
            return;

        if (!_rmcVehicles.TryResolveControlledVehicle(args.Performer, out var vehicle))
            return;

        if (!TryComp(vehicle, out VehicleComponent? vehicleComp) || vehicleComp.Operator != holder.Owner)
            return;

        if (!TryComp(vehicle, out VehicleMovementSpeedGearComponent? movement_gear_mod))
            return;

        if (!TryComp(vehicle, out VehicleMovementModeModifiersComponent? speed_data))
            return;

        if (!TryComp(vehicle, out VehicleMovementModeComponent? vehicle_move_mode))
            return;

        if (!TryComp(vehicle, out VehicleMovementStateComponent? vehicle_move_state))
            return;

        if (vehicle_move_mode.MoveMode >= VehicleMovementStateList.Overdrive2)
            return;

        vehicle_move_mode.MoveMode =
            (VehicleMovementStateList)((int) vehicle_move_mode.MoveMode + 1);

        movement_gear_mod.GearSpeedMultiplier = GetOurSpeed(speed_data, vehicle_move_mode);
        if(vehicle_move_mode.Enabled)
        {
            SetFuelDrain(vehicle_move_mode, vehicle_move_state);
            Dirty(vehicle, vehicle_move_mode);
            Dirty(vehicle, movement_gear_mod);
            Dirty(vehicle, vehicle_move_state);
            _vehicleWeapons.RefreshWeaponsUi(vehicle);
        }
    }

    private void ShiftDown(Entity<VehicleMovementActionHolderComponent> holder, ref VehicleGearShiftDownEvent args)
    {
        if (_net.IsClient || args.Handled || args.Performer != holder.Owner)
            return;

        if (!_rmcVehicles.TryResolveControlledVehicle(args.Performer, out var vehicle))
            return;

        if (!TryComp(vehicle, out VehicleComponent? vehicleComp) || vehicleComp.Operator != holder.Owner)
            return;

        if (!TryComp(vehicle, out VehicleMovementSpeedGearComponent? movement_gear_mod))
            return;

        if (!TryComp(vehicle, out VehicleMovementModeModifiersComponent? speed_data))
            return;

        if (!TryComp(vehicle, out VehicleMovementModeComponent? vehicle_move_mode))
            return;

        if (!TryComp(vehicle, out VehicleMovementStateComponent? vehicle_move_state))
            return;

        if (vehicle_move_mode.MoveMode <= VehicleMovementStateList.SlowestStandard)
            return;

        vehicle_move_mode.MoveMode =
            (VehicleMovementStateList)((int) vehicle_move_mode.MoveMode - 1);

        movement_gear_mod.GearSpeedMultiplier = GetOurSpeed(speed_data, vehicle_move_mode);
        if(vehicle_move_mode.Enabled)
        {
            SetFuelDrain(vehicle_move_mode, vehicle_move_state);
            Dirty(vehicle, vehicle_move_mode);
            Dirty(vehicle, movement_gear_mod);
            Dirty(vehicle, vehicle_move_state);
            _vehicleWeapons.RefreshWeaponsUi(vehicle);
        }
    }

    private float GetOurSpeed(VehicleMovementModeModifiersComponent our_data, VehicleMovementModeComponent our_state)
    {
        return our_state.MoveMode switch
        {
            VehicleMovementStateList.SlowestStandard => our_data.SlowestStandard,
            VehicleMovementStateList.SlowStandard => our_data.SlowStandard,
            VehicleMovementStateList.Standard => our_data.Standard,
            VehicleMovementStateList.FastStandard => our_data.FastStandard,
            VehicleMovementStateList.FastestStandard => our_data.FastestStandard,
            VehicleMovementStateList.Overdrive => our_data.Overdrive,
            VehicleMovementStateList.Overdrive2 => our_data.Overdrive2,
            _ => 0f,
        };
    }

    private void SetFuelDrain(VehicleMovementModeComponent our_mode, VehicleMovementStateComponent our_state)
    {
        our_state.VehicleMove = our_mode.MoveMode switch
        {
            VehicleMovementStateList.SlowestStandard => VehicleMoveState.Reduced,
            VehicleMovementStateList.SlowStandard => VehicleMoveState.Reduced,
            VehicleMovementStateList.Standard => VehicleMoveState.Active,
            VehicleMovementStateList.FastStandard => VehicleMoveState.Active,
            VehicleMovementStateList.FastestStandard => VehicleMoveState.Overdrive,
            VehicleMovementStateList.Overdrive => VehicleMoveState.Overdrive,
            VehicleMovementStateList.Overdrive2 => VehicleMoveState.Overdrive,
            _ => VehicleMoveState.Disabled,
        };
    }

    public void EnableMovementActions(EntityUid user)
    {
        var holder = EnsureComp<VehicleMovementActionHolderComponent>(user);

        holder.Action1 ??= _actions.AddAction(user, holder.ActionId1);
        holder.Action2 ??= _actions.AddAction(user, holder.ActionId2);
        holder.Action3 ??= _actions.AddAction(user, holder.ActionId3);

        if (holder.Action3 is { } engineAction &&
            _rmcVehicles.TryResolveControlledVehicle(user, out var vehicle) &&
            TryComp(vehicle, out VehicleMovementModeComponent? mode))
        {
            _actions.SetToggled(engineAction, mode.Enabled);
        }

        Dirty(user, holder);
    }

    public void DisableMovementActions(EntityUid user)
    {
        if (!TryComp(user, out VehicleMovementActionHolderComponent? holder))
            return;

        if (holder.Action1 is { } gearUp)
            _actions.RemoveAction(user, gearUp);

        if (holder.Action2 is { } gearDown)
            _actions.RemoveAction(user, gearDown);

        if (holder.Action3 is { } engineToggle)
            _actions.RemoveAction(user, engineToggle);

        RemCompDeferred<VehicleMovementActionHolderComponent>(user);
    }

    private void OnCanRun(Entity<VehicleMovementModeComponent> ent, ref VehicleCanRunEvent args)
    {
        if (!ent.Comp.Enabled)
            args.CanRun = false;
    }

}

[Serializable, NetSerializable]
public sealed partial class VehicleNetworkEntityGetter : EntityEventArgs
{
    public NetEntity Vehicle;

    public VehicleNetworkEntityGetter()
    {
    }

    public VehicleNetworkEntityGetter(NetEntity vehicle)
    {
        Vehicle = vehicle;
    }

}
