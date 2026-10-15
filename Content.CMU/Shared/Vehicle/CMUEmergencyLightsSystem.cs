using Content.Shared.Actions;
using Content.Shared.Popups;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Map;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.Vehicle;

public sealed partial class CMUEmergencyLightsSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUEmergencyLightsComponent, VehicleOperatorSetEvent>(OnOperatorSet);
        SubscribeLocalEvent<CMUEmergencyLightsComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<VehicleOperatorComponent, CMUEmergencyLightsActionEvent>(OnAction);
    }

    private void OnOperatorSet(Entity<CMUEmergencyLightsComponent> ent, ref VehicleOperatorSetEvent args)
    {
        if (_net.IsClient)
            return;

        _actions.RemoveAction(ent.Comp.DriverAction);
        ent.Comp.DriverAction = null;

        if (args.NewOperator is not { } driver)
            return;

        _actions.AddAction(driver, ref ent.Comp.DriverAction, ent.Comp.Action);
        if (ent.Comp.DriverAction is { } action)
            _actions.SetToggled(action, ent.Comp.Mode > 0);
    }

    private void OnShutdown(Entity<CMUEmergencyLightsComponent> ent, ref ComponentShutdown args)
    {
        if (_net.IsClient)
            return;

        _actions.RemoveAction(ent.Comp.DriverAction);
        ent.Comp.DriverAction = null;

        if (ent.Comp.Lightbar is { } lightbar)
            QueueDel(lightbar);
        ent.Comp.Lightbar = null;
    }

    private void OnAction(Entity<VehicleOperatorComponent> ent, ref CMUEmergencyLightsActionEvent args)
    {
        if (args.Handled ||
            ent.Comp.Vehicle is not { } vehicle ||
            !TryComp(vehicle, out CMUEmergencyLightsComponent? lights) ||
            !TryComp(vehicle, out VehicleComponent? vehicleComp) ||
            vehicleComp.Operator != args.Performer)
        {
            return;
        }

        args.Handled = true;
        if (_net.IsClient)
            return;

        var mode = (lights.Mode + 1) % (lights.LightPrototypes.Count + 1);
        SetMode((vehicle, lights), mode);
        _popup.PopupEntity(Loc.GetString("cmu-emergency-lights-mode", ("mode", mode)), args.Performer, args.Performer);
    }

    public void SetMode(Entity<CMUEmergencyLightsComponent> ent, int mode)
    {
        if (_net.IsClient)
            return;

        mode = Math.Clamp(mode, 0, ent.Comp.LightPrototypes.Count);
        if (ent.Comp.Mode == mode)
            return;

        ent.Comp.Mode = mode;
        Dirty(ent);

        if (ent.Comp.DriverAction is { } action)
            _actions.SetToggled(action, mode > 0);

        if (ent.Comp.Lightbar is { } lightbar)
            QueueDel(lightbar);
        ent.Comp.Lightbar = null;

        if (mode > 0)
            ent.Comp.Lightbar = SpawnAttachedTo(ent.Comp.LightPrototypes[mode - 1], new EntityCoordinates(ent, ent.Comp.Offset));
    }
}
