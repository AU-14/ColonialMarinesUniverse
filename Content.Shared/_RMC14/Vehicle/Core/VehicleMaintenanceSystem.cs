using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Content.Shared.Verbs;
using Robust.Shared.Network;

namespace Content.Shared._RMC14.Vehicle;

/// <summary>Locks tank controls and exposes its service panels while field repairs are performed.</summary>
public sealed class VehicleMaintenanceSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VehicleMaintenanceComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<VehicleMaintenanceComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<VehicleMaintenanceComponent, VehicleCanRunEvent>(OnCanRun);
        SubscribeLocalEvent<VehicleMaintenanceComponent, VehicleMaintenanceDoAfterEvent>(OnPanelsDoAfter);
        SubscribeLocalEvent<VehicleMaintenanceComponent, DamageModifyEvent>(OnDamageModify, before: [typeof(HardpointSystem)]);
        SubscribeLocalEvent<VehicleMaintenanceComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<VehicleMaintenanceComponent, VehicleOperatorSetEvent>(OnOperatorSet);
        SubscribeLocalEvent<VehicleOperatorComponent, VehicleMaintenanceActionEvent>(OnAction);
    }

    private void OnOperatorSet(Entity<VehicleMaintenanceComponent> ent, ref VehicleOperatorSetEvent args)
    {
        if (_net.IsClient)
            return;

        _actions.RemoveAction(ent.Comp.DriverAction);
        ent.Comp.DriverAction = null;
        if (args.NewOperator is { } driver)
            _actions.AddAction(driver, ref ent.Comp.DriverAction, ent.Comp.Action);
        UpdateAction(ent.Comp);
    }

    private void OnAction(Entity<VehicleOperatorComponent> ent, ref VehicleMaintenanceActionEvent args)
    {
        if (args.Handled || ent.Comp.Vehicle is not { } vehicle ||
            !TryComp<VehicleMaintenanceComponent>(vehicle, out var maintenance))
            return;

        args.Handled = true;
        if (_net.IsServer)
            TryToggle((vehicle, maintenance), args.Performer, fromDriver: true);
    }

    private void OnGetVerbs(Entity<VehicleMaintenanceComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.Mode == VehicleMaintenanceMode.Operational
                ? "rmc-vehicle-maintenance-open" : "rmc-vehicle-maintenance-close"),
            Act = () => TryToggle(ent, user),
        });
    }

    public bool TryToggle(Entity<VehicleMaintenanceComponent> ent, EntityUid user, bool fromDriver = false)
    {
        if (_net.IsClient || !TryComp<VehicleComponent>(ent, out var vehicle))
            return false;

        if (ent.Comp.Mode is VehicleMaintenanceMode.Opening or VehicleMaintenanceMode.Closing)
        {
            if (_doAfter.GetStatus(ent.Comp.PanelDoAfter) == DoAfterStatus.Running)
                return false;

            // Deleting the operator can remove their do-after without its completion event.
            // Let another crew member recover the interrupted panel operation.
            SetMode(ent, ent.Comp.Mode == VehicleMaintenanceMode.Opening
                ? VehicleMaintenanceMode.Operational : VehicleMaintenanceMode.Maintenance);
        }

        if (fromDriver && vehicle.Operator != user)
            return false;

        if (!fromDriver && vehicle.Operator != null && ent.Comp.Mode == VehicleMaintenanceMode.Operational)
        {
            _popup.PopupEntity(Loc.GetString("rmc-vehicle-maintenance-driver-required"), user, user);
            return false;
        }

        if (TryComp<GridVehicleMoverComponent>(ent, out var mover) &&
            (mover.IsMoving || MathF.Abs(mover.CurrentSpeed) > 0.01f))
        {
            _popup.PopupEntity(Loc.GetString("rmc-vehicle-maintenance-stop-first"), user, user);
            return false;
        }

        var ev = new VehicleMaintenanceDoAfterEvent();
        var args = new DoAfterArgs(EntityManager, user, ent.Comp.PanelDelay, ev, ent,
            target: fromDriver ? user : ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = !fromDriver,
        };
        var previous = ent.Comp.Mode;
        SetMode(ent, previous == VehicleMaintenanceMode.Operational
            ? VehicleMaintenanceMode.Opening : VehicleMaintenanceMode.Closing);
        if (!_doAfter.TryStartDoAfter(args, out ent.Comp.PanelDoAfter))
        {
            SetMode(ent, previous);
            return false;
        }

        return true;
    }

    private void OnPanelsDoAfter(Entity<VehicleMaintenanceComponent> ent, ref VehicleMaintenanceDoAfterEvent args)
    {
        if (args.Handled || ent.Comp.Mode is not (VehicleMaintenanceMode.Opening or VehicleMaintenanceMode.Closing))
            return;

        args.Handled = true;
        ent.Comp.PanelDoAfter = null;
        var opening = ent.Comp.Mode == VehicleMaintenanceMode.Opening;
        SetMode(ent, opening != args.Cancelled ? VehicleMaintenanceMode.Maintenance : VehicleMaintenanceMode.Operational);
    }

    private void SetMode(Entity<VehicleMaintenanceComponent> ent, VehicleMaintenanceMode mode)
    {
        ent.Comp.Mode = mode;
        Dirty(ent);
        _blocker.UpdateCanMove(ent);
        UpdateAction(ent.Comp);
    }

    private void UpdateAction(VehicleMaintenanceComponent maintenance)
    {
        if (maintenance.DriverAction is { } action)
            _actions.SetToggled(action, maintenance.ControlsLocked);
    }

    private void OnState(Entity<VehicleMaintenanceComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _blocker.UpdateCanMove(ent);
    }

    private void OnCanRun(Entity<VehicleMaintenanceComponent> ent, ref VehicleCanRunEvent args)
    {
        if (ent.Comp.ControlsLocked)
            args.CanRun = false;
    }

    private void OnDamageModify(Entity<VehicleMaintenanceComponent> ent, ref DamageModifyEvent args)
    {
        if (ent.Comp.ControlsLocked && args.Damage.AnyPositive())
            args.Damage += DamageSpecifier.GetPositive(args.Damage) * (ent.Comp.ExposedDamageMultiplier - 1f);
    }

    private void OnExamine(Entity<VehicleMaintenanceComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.ControlsLocked)
            args.PushMarkup(Loc.GetString("rmc-vehicle-maintenance-examine"));
    }
}
