using Content.Shared.Actions;
using Content.Shared._RMC14.NightVision;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Popups;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Vehicle;

public sealed class CMUVehicleVisionSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedNightVisionSystem _nightVision = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VehicleViewToggleComponent, VehicleViewToggleEnabledEvent>(OnViewToggleEnabled);
        SubscribeLocalEvent<VehicleViewToggleComponent, VehicleViewToggledEvent>(OnViewToggled);
        SubscribeLocalEvent<CMUVehicleVisionOperatorComponent, VehicleViewToggleDisabledEvent>(OnViewToggleShutdown);
        SubscribeLocalEvent<CMUVehicleVisionOperatorComponent, CMUVehicleNightVisionActionEvent>(OnNightVisionAction);
        SubscribeLocalEvent<CMUVehicleVisionOperatorComponent, CMUVehicleThermalVisionActionEvent>(OnThermalVisionAction);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<CMUVehicleVisionOperatorComponent>();
        while (query.MoveNext(out var user, out var vision))
        {
            var thermalReady = vision.ThermalReadyAt <= _timing.CurTime;
            if (vision.ThermalActionWasReady != thermalReady)
            {
                vision.ThermalActionWasReady = thermalReady;
                if (TryComp(user, out VehicleViewToggleComponent? view))
                    SetActionStates(user, vision, view.IsOutside && view.OutsideTarget == vision.Vehicle);
            }

            if (vision.Mode == CMUVehicleVisionMode.Thermal && _timing.CurTime >= vision.ThermalUntil)
            {
                if (TryComp(vision.Vehicle, out CMUVehicleOpticsComponent? optics))
                    SetMode(user, vision, vision.ModeBeforeThermal, optics);
                else
                    RestoreVision(user, vision);

                if (TryComp(user, out VehicleViewToggleComponent? view))
                    SetActionStates(user, vision, view.IsOutside && view.OutsideTarget == vision.Vehicle);
            }
        }
    }

    private void OnViewToggleEnabled(
        Entity<VehicleViewToggleComponent> ent,
        ref VehicleViewToggleEnabledEvent args)
    {
        if (_net.IsClient || !TryComp(args.OutsideTarget, out CMUVehicleOpticsComponent? optics))
            return;

        if (TryComp(ent.Owner, out CMUVehicleVisionOperatorComponent? existing) &&
            existing.Vehicle != args.OutsideTarget)
        {
            RemoveActions(ent.Owner, existing);
            RestoreVision(ent.Owner, existing);
        }

        var vision = EnsureComp<CMUVehicleVisionOperatorComponent>(ent.Owner);
        vision.Vehicle = args.OutsideTarget;
        _actions.AddAction(ent.Owner, ref vision.NightVisionAction, optics.NightVisionAction);
        _actions.AddAction(ent.Owner, ref vision.ThermalVisionAction, optics.ThermalVisionAction);
        SetActionStates(ent.Owner, vision, ent.Comp.IsOutside);
    }

    private void OnViewToggled(
        Entity<VehicleViewToggleComponent> ent,
        ref VehicleViewToggledEvent args)
    {
        if (_net.IsClient || !TryComp(ent.Owner, out CMUVehicleVisionOperatorComponent? vision))
            return;

        if (!args.IsOutside)
            RestoreVision(ent.Owner, vision);

        SetActionStates(ent.Owner, vision, args.IsOutside);
    }

    private void OnViewToggleShutdown(
        Entity<CMUVehicleVisionOperatorComponent> ent,
        ref VehicleViewToggleDisabledEvent args)
    {
        var user = ent.Owner;
        if (_net.IsClient || !TryComp(user, out CMUVehicleVisionOperatorComponent? vision))
            return;

        RemoveActions(user, vision);
        RestoreVision(user, vision);
        vision.Vehicle = EntityUid.Invalid;
    }

    private void OnNightVisionAction(
        Entity<CMUVehicleVisionOperatorComponent> ent,
        ref CMUVehicleNightVisionActionEvent args)
    {
        if (_net.IsClient || args.Handled || args.Performer != ent.Owner ||
            !TryGetActiveOptics(ent.Owner, ent.Comp, out var optics))
        {
            return;
        }

        args.Handled = true;
        var next = ent.Comp.Mode == CMUVehicleVisionMode.NightVision
            ? CMUVehicleVisionMode.Off
            : CMUVehicleVisionMode.NightVision;
        SetMode(ent.Owner, ent.Comp, next, optics);
        SetActionStates(ent.Owner, ent.Comp, outside: true);
    }

    private void OnThermalVisionAction(
        Entity<CMUVehicleVisionOperatorComponent> ent,
        ref CMUVehicleThermalVisionActionEvent args)
    {
        if (_net.IsClient || args.Handled || args.Performer != ent.Owner ||
            !TryGetActiveOptics(ent.Owner, ent.Comp, out var optics))
        {
            return;
        }

        args.Handled = true;
        if (ent.Comp.Mode == CMUVehicleVisionMode.Thermal)
        {
            SetMode(ent.Owner, ent.Comp, ent.Comp.ModeBeforeThermal, optics);
            SetActionStates(ent.Owner, ent.Comp, outside: true);
            return;
        }

        if (ent.Comp.ThermalReadyAt > _timing.CurTime)
        {
            var remaining = (int) Math.Ceiling((ent.Comp.ThermalReadyAt - _timing.CurTime).TotalSeconds);
            _popup.PopupEntity(
                Loc.GetString("cmu-vehicle-thermal-cooldown", ("seconds", remaining)),
                ent.Owner,
                ent.Owner,
                PopupType.SmallCaution);
            return;
        }

        if (optics.ThermalDuration <= TimeSpan.Zero || optics.ThermalCooldown <= TimeSpan.Zero)
        {
            _popup.PopupEntity(Loc.GetString("cmu-vehicle-thermal-unavailable"), ent.Owner, ent.Owner, PopupType.SmallCaution);
            return;
        }

        ent.Comp.ModeBeforeThermal = ent.Comp.Mode;
        ent.Comp.ThermalUntil = _timing.CurTime + optics.ThermalDuration;
        ent.Comp.ThermalReadyAt = _timing.CurTime + optics.ThermalCooldown;
        ent.Comp.ThermalActionWasReady = false;
        SetMode(ent.Owner, ent.Comp, CMUVehicleVisionMode.Thermal, optics);
        SetActionStates(ent.Owner, ent.Comp, outside: true);
    }

    private bool TryGetActiveOptics(
        EntityUid user,
        CMUVehicleVisionOperatorComponent vision,
        out CMUVehicleOpticsComponent optics)
    {
        optics = default!;
        if (!TryComp(user, out VehicleViewToggleComponent? view) ||
            !view.IsOutside ||
            view.OutsideTarget != vision.Vehicle ||
            !TryComp(vision.Vehicle, out CMUVehicleOpticsComponent? found) ||
            found == null)
        {
            return false;
        }

        optics = found;
        return true;
    }

    private void SetMode(
        EntityUid user,
        CMUVehicleVisionOperatorComponent vision,
        CMUVehicleVisionMode mode,
        CMUVehicleOpticsComponent optics)
    {
        if (mode == CMUVehicleVisionMode.Off)
        {
            RestoreVision(user, vision);
            return;
        }

        if (!vision.CapturedNightVision)
        {
            vision.PreviousNightVision = _nightVision.CaptureSettings(user);
            vision.CapturedNightVision = true;
        }

        vision.Mode = mode;
        var thermal = mode == CMUVehicleVisionMode.Thermal;
        _nightVision.SetVehicleVision(
            user,
            thermal ? optics.ThermalTint : null,
            thermal ? optics.ThermalOverlay : optics.NightVisionOverlay,
            thermal && optics.ThermalSeeThroughContainers);
    }

    private void RestoreVision(EntityUid user, CMUVehicleVisionOperatorComponent vision)
    {
        if (vision.CapturedNightVision)
            _nightVision.RestoreSettings(user, vision.PreviousNightVision);

        vision.CapturedNightVision = false;
        vision.PreviousNightVision = null;
        vision.Mode = CMUVehicleVisionMode.Off;
        vision.ThermalUntil = TimeSpan.Zero;
    }

    private void SetActionStates(EntityUid user, CMUVehicleVisionOperatorComponent vision, bool outside)
    {
        if (vision.NightVisionAction is { } night && Exists(night))
        {
            _actions.SetEnabled(night, outside);
            _actions.SetToggled(night, vision.Mode == CMUVehicleVisionMode.NightVision);
        }

        if (vision.ThermalVisionAction is { } thermal && Exists(thermal))
        {
            var canToggleThermal = vision.Mode == CMUVehicleVisionMode.Thermal ||
                                   vision.ThermalReadyAt <= _timing.CurTime;
            _actions.SetEnabled(thermal, outside && canToggleThermal);
            _actions.SetToggled(thermal, vision.Mode == CMUVehicleVisionMode.Thermal);
        }
    }

    private void RemoveActions(EntityUid user, CMUVehicleVisionOperatorComponent vision)
    {
        RemoveAction(user, vision.NightVisionAction);
        RemoveAction(user, vision.ThermalVisionAction);
        vision.NightVisionAction = null;
        vision.ThermalVisionAction = null;
    }

    private void RemoveAction(EntityUid user, EntityUid? action)
    {
        if (action is not { } actionUid || !Exists(actionUid))
            return;

        _actions.RemoveAction(user, actionUid);
        QueueDel(actionUid);
    }
}
