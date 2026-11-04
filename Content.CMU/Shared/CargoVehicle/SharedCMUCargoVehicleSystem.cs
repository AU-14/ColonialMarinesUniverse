using Content.Shared.Vehicle;

namespace Content.Shared.CMU14.CargoVehicle;

public sealed class SharedCMUCargoVehicleSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUCargoVehicleComponent, VehicleCanRunEvent>(OnVehicleCanRun);
    }

    private void OnVehicleCanRun(Entity<CMUCargoVehicleComponent> ent, ref VehicleCanRunEvent args)
    {
        if (ent.Comp.ArmingMode == CMUCargoVehicleArmingMode.Automatic)
            args.CanRun = false;
    }
}
