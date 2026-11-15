using System.Collections.Generic;
using System.Numerics;
using Content.Shared.CMU14.Blackfoot;
using Content.Shared.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Actions;
using Content.Shared.ActionBlocker;
using Content.Shared.Audio;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared.CMU14.Vehicle.Open;
using Robust.Shared.Network;
using Content.Shared.DoAfter;
using Content.Shared.CMU14.Vehicle;
using Content.Shared._RMC14.Power;

namespace Content.Shared.CMU14.Vehicle;

public sealed partial class VehiclePowerSourceSharedSystem : EntitySystem
{

public override void Initialize()
{
    SubscribeLocalEvent<VehiclePowerSourceTankComponent, VehicleRefuelAttemptEvent>(RefuelFueltank);
    SubscribeLocalEvent<VehiclePowerSourceTankComponent, VehicleRefuelDoAfterEvent>(OnRefuelComplete);
}

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
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private AreaSystem _area = default!;
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private HardpointSystem _hardpoints = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private VehicleViewToggleSystem _viewToggle = default!;
    [Dependency] private CMUVehicleEngineSwitchSystem _engineSwitch = default!;
    [Dependency] private VehicleMovementModeSystem _movementModeSystem = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private VehicleSystem _rmcVehicles = default!;
    [Dependency] private Content.Shared.Vehicle.Systems.VehicleSystem _vehicleSystem = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;

    public void ProcessFuel(Entity<VehiclePowerSourceTankComponent> our_fueltank, float frameTime)
    {

        if (!TryComp(our_fueltank, out VehiclePowerSourceDrainComponent? fuel_drain) ||
            !TryComp(our_fueltank, out VehicleMovementStateComponent? vehicle_movement_state) ||
            !TryComp(our_fueltank, out VehiclePowerSourceStatesComponent? power_source_states))
        {
            return;
        }

        var total_drain = GetFuelDrain(fuel_drain, vehicle_movement_state) * GetFuelLeakDrain(power_source_states, fuel_drain, our_fueltank);
        if (total_drain <= 0f)
            return;
        if(our_fueltank.Comp.Fuel <= 0f)
            {
            SwitchEngine(our_fueltank);
            var ev = new VehicleFuelDepletedEvent();
            RaiseLocalEvent(our_fueltank.Owner, ref ev);
            Dirty(our_fueltank.Owner, our_fueltank.Comp);
            return;
            }

        our_fueltank.Comp.Fuel = MathF.Max(0f, our_fueltank.Comp.Fuel - total_drain * frameTime);
        Dirty(our_fueltank.Owner, our_fueltank.Comp);
    }

    private float GetFuelDrain(VehiclePowerSourceDrainComponent our_drain, VehicleMovementStateComponent our_states)
    {
        return our_states.VehicleMove switch
        {
            VehicleMoveState.Idle => our_drain.IdleFuelDrain,
            VehicleMoveState.Reduced => our_drain.ReducedFuelDrain,
            VehicleMoveState.Active => our_drain.ActiveFuelDrain,
            VehicleMoveState.Overdrive => our_drain.OverdriveFuelDrain,
            _ => 0f,
        };
    }

    private float GetFuelLeakDrain(VehiclePowerSourceStatesComponent our_status, VehiclePowerSourceDrainComponent our_drain, VehiclePowerSourceTankComponent our_fueltank)
    {
        if (our_fueltank.Fuel <= 0f)
        {
            return 1f;
        }

        switch (our_status.PowerSourceTankState)
        {
            case VehiclePowerSourceTankState.Normal:
                return our_drain.LeakNormal;
            case VehiclePowerSourceTankState.Patched:
                return our_drain.LeakPatched;
            case VehiclePowerSourceTankState.Leaking:
                return our_drain.LeakLeaking;
            case VehiclePowerSourceTankState.Torn:
                return our_drain.LeakTorn;
            case VehiclePowerSourceTankState.Ruptured:
                return our_drain.LeakRuptured;
            case VehiclePowerSourceTankState.Gone:
                return our_drain.LeakGone;
            default:
                return 1f;
        }
    }

    public void SwitchEngine(Entity<VehiclePowerSourceTankComponent> our_fueltank)
    {
        if (_net.IsClient)
            return;

        if (!TryComp(our_fueltank.Owner, out VehicleComponent? vehicleComponent))
            return;

        if (!TryComp(our_fueltank.Owner, out VehicleMovementModeComponent? mode))
            return;

        if (!TryComp(our_fueltank.Owner, out VehiclePowerSourceTankComponent? fueltank))
            return;

        if(fueltank.Fuel > 0)
            mode.Enabled = !mode.Enabled;
        else
            mode.Enabled = false;

        if (vehicleComponent.Operator is { } driver &&
            TryComp(driver, out VehicleMovementActionHolderComponent? holder) &&
            holder.Action3 is { } engineAction)
        {
            _actions.SetToggled(engineAction, mode.Enabled);
        }

        if(TryComp(our_fueltank.Owner, out VehicleMovementStateComponent? move_state))
        {
            if(mode.Enabled)
                move_state.VehicleMove = VehicleMoveState.Idle;
            else
                move_state.VehicleMove = VehicleMoveState.Disabled;
            Dirty(our_fueltank.Owner, move_state);
        }
        Dirty(our_fueltank.Owner, mode);
        if (TryComp(our_fueltank.Owner, out CMUVehicleEngineSoundComponent? sound))
            {
            if(mode.Enabled)
                _engineSwitch.SetSwitched((our_fueltank.Owner, sound), true);
            else
                _engineSwitch.SetSwitched((our_fueltank.Owner, sound), false);
            }
        _vehicleSystem.RefreshCanRun((our_fueltank.Owner, vehicleComponent));
    }

    private void RefuelFueltank(Entity<VehiclePowerSourceTankComponent> fueltank, ref VehicleRefuelAttemptEvent args)
    {
        var doAfter = new DoAfterArgs(
            EntityManager,
            args.user,
            TimeSpan.FromSeconds(4),
            new VehicleRefuelDoAfterEvent(),
            fueltank.Owner,
            fueltank.Owner,
            args.fuel_cell)
        {
            BreakOnMove = true,
            NeedHand = true,
            BlockDuplicate = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnRefuelComplete(
        Entity<VehiclePowerSourceTankComponent> fueltank,
        ref VehicleRefuelDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } cell || !HasComp<RMCFusionCellComponent>(cell))
        {
            return;
        }

        args.Handled = true;

        if (_net.IsClient)
            return;

        var newFuel = Math.Clamp(fueltank.Comp.Fuel + 1000f, 0f, fueltank.Comp.MaxFuel);

        if (newFuel <= fueltank.Comp.Fuel)
            return;

        fueltank.Comp.Fuel = newFuel;
        Dirty(fueltank.Owner, fueltank.Comp);
        QueueDel(cell);
        var refuel_audio = new SoundPathSpecifier("/Audio/Effects/refill.ogg");
        _audio.PlayPvs(refuel_audio, fueltank.Owner);
    }

}
