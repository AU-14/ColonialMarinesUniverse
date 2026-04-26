using Content.Shared.Actions;
using Content.Shared.Popups;
using Content.Shared.Vehicle.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared._RMC14.Vehicle;

public sealed class VehicleLockSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VehicleEnterComponent, MapInitEvent>(OnVehicleMapInit);

        SubscribeLocalEvent<VehicleLockActionComponent, VehicleLockActionEvent>(OnLockAction);
        SubscribeLocalEvent<VehicleLockActionComponent, ComponentShutdown>(OnLockActionShutdown);
    }

    private void OnVehicleMapInit(Entity<VehicleEnterComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient)
            return;

        EnsureComp<VehicleLockComponent>(ent.Owner);
    }

    public void EnableLockAction(EntityUid user, EntityUid vehicle)
    {
        var actionComp = EnsureComp<VehicleLockActionComponent>(user);
        actionComp.Sources.Add(vehicle);
        actionComp.Vehicle = vehicle;

        var lockComp = EnsureComp<VehicleLockComponent>(vehicle);

        if (actionComp.Action == null || TerminatingOrDeleted(actionComp.Action.Value))
            actionComp.Action = _actions.AddAction(user, actionComp.ActionId);

        if (actionComp.Action is { } actionUid)
        {
            _actions.SetEnabled(actionUid, true);
            _actions.SetToggled(actionUid, lockComp.Locked);
        }

        Dirty(user, actionComp);
    }

    public void DisableLockAction(EntityUid user, EntityUid vehicle)
    {
        if (!TryComp(user, out VehicleLockActionComponent? actionComp))
            return;

        actionComp.Sources.Remove(vehicle);
        if (actionComp.Sources.Count > 0)
        {
            foreach (var remaining in actionComp.Sources)
            {
                actionComp.Vehicle = remaining;
                break;
            }

            if (actionComp.Action is { } actionUid &&
                actionComp.Vehicle is { } actionVehicle &&
                TryComp(actionVehicle, out VehicleLockComponent? lockComp))
            {
                _actions.SetToggled(actionUid, lockComp.Locked);
            }

            Dirty(user, actionComp);
            return;
        }

        if (actionComp.Action != null)
        {
            _actions.RemoveAction(user, actionComp.Action.Value);
            actionComp.Action = null;
        }

        RemCompDeferred<VehicleLockActionComponent>(user);
    }

    private void OnLockActionShutdown(Entity<VehicleLockActionComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Action is { } action)
            _actions.RemoveAction(action);
    }

    private void OnLockAction(Entity<VehicleLockActionComponent> ent, ref VehicleLockActionEvent args)
    {
        if (_net.IsClient)
            return;

        if (args.Handled || args.Performer != ent.Owner)
            return;

        args.Handled = true;

        if (ent.Comp.Vehicle is not { } vehicle || Deleted(vehicle))
        {
            return;
        }

        if (!TryComp(vehicle, out VehicleComponent? vehicleComp) || vehicleComp.Operator != ent.Owner)
        {
            _popup.PopupEntity(Loc.GetString("rmc-vehicle-lock-not-driver"), ent.Owner, ent.Owner, PopupType.SmallCaution);
            return;
        }

        var lockComp = EnsureComp<VehicleLockComponent>(vehicle);

        if (!lockComp.Locked && lockComp.ForcedOpen)
        {
            _popup.PopupCursor(
                Loc.GetString("rmc-vehicle-lock-too-damaged"),
                ent.Owner,
                PopupType.SmallCaution);
            return;
        }

        lockComp.Locked = !lockComp.Locked;

        if (ent.Comp.Action is { } actionUid)
            _actions.SetToggled(actionUid, lockComp.Locked);

        _popup.PopupEntity(
            Loc.GetString(lockComp.Locked ? "rmc-vehicle-lock-set-locked" : "rmc-vehicle-lock-set-unlocked"),
            ent.Owner,
            ent.Owner,
            PopupType.Small);
    }

    public void RefreshForcedOpen(EntityUid vehicle)
    {
        if (_net.IsClient)
            return;

        if (!TryComp(vehicle, out VehicleLockComponent? lockComp))
            return;

        if (!TryComp(vehicle, out HardpointIntegrityComponent? frame) || frame.MaxIntegrity <= 0f)
            return;

        var fraction = frame.Integrity / frame.MaxIntegrity;

        if (!lockComp.ForcedOpen)
        {
            if (fraction < lockComp.ForceOpenBelowFraction)
            {
                lockComp.ForcedOpen = true;

                if (lockComp.Locked)
                {
                    lockComp.Locked = false;
                    UpdateOperatorLockAction(vehicle, false);
                    AnnounceOnVehicle(vehicle, "rmc-vehicle-lock-broken-open", PopupType.MediumCaution);
                }
            }
        }
        else if (fraction >= lockComp.RelockAtFraction)
        {
            lockComp.ForcedOpen = false;
            AnnounceOnVehicle(vehicle, "rmc-vehicle-lock-operational-again", PopupType.Medium);
        }
    }

    private void UpdateOperatorLockAction(EntityUid vehicle, bool locked)
    {
        if (!TryComp(vehicle, out VehicleComponent? vehicleComp) || vehicleComp.Operator is not { } op)
            return;

        if (!TryComp(op, out VehicleLockActionComponent? actionComp) || actionComp.Action is not { } actionUid)
            return;

        _actions.SetToggled(actionUid, locked);
    }

    private void AnnounceOnVehicle(EntityUid vehicle, string locString, PopupType popupType)
    {
        var msg = Loc.GetString(locString);

        _popup.PopupEntity(msg, vehicle, popupType);

        foreach (var session in Filter.Pvs(vehicle).Recipients)
        {
            if (session.AttachedEntity is { } viewer)
                _popup.PopupCursor(msg, viewer, popupType);
        }
    }
}
