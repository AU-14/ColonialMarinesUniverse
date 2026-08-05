using Content.Shared.Movement.Components;
using Content.Shared.Vehicle.Components;

namespace Content.Shared._RMC14.Vehicle;

public sealed partial class VehicleSystem
{
    private void OnRmcGridVehicleOperatorEntered(
        Entity<VehicleOperatorComponent> ent,
        OnVehicleEnteredEvent args)
    {
        if (args.Vehicle.Comp.MovementKind != VehicleMovementKind.Grid)
            return;

        EnsureComp<GridVehicleMoverComponent>(args.Vehicle);
        EnsureComp<GridVehicleOperatorComponent>(ent);
        RemCompDeferred<RelayInputMoverComponent>(ent);
        RemCompDeferred<MovementRelayTargetComponent>(args.Vehicle);
    }

    private void OnRmcGridVehicleOperatorExited(
        Entity<VehicleOperatorComponent> ent,
        OnVehicleExitedEvent args)
    {
        if (args.Vehicle.Comp.MovementKind != VehicleMovementKind.Grid)
            return;

        RemCompDeferred<GridVehicleOperatorComponent>(ent);
    }
}
