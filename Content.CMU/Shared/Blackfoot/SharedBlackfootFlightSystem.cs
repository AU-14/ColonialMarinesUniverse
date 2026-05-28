using Content.Shared.Vehicle.Components;

namespace Content.Shared._CMU14.Blackfoot;

public sealed partial class SharedBlackfootFlightSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlackfootFlightComponent, VehicleCanRunEvent>(OnVehicleCanRun);
    }

    private void OnVehicleCanRun(Entity<BlackfootFlightComponent> ent, ref VehicleCanRunEvent args)
    {
        if (!args.CanRun)
            return;

        args.CanRun = CanRunInState(ent.Comp.State, HasTowConnection(ent.Owner));
    }

    public static bool CanRunInState(BlackfootFlightState state, bool hasTowConnection)
    {
        return state switch
        {
            BlackfootFlightState.Stowed or BlackfootFlightState.Grounded or BlackfootFlightState.Crashed => hasTowConnection,
            BlackfootFlightState.VTOL or BlackfootFlightState.Flight => true,
            _ => false,
        };
    }

    private bool HasTowConnection(EntityUid vehicle)
    {
        return TryComp(vehicle, out BlackfootTowComponent? tow) &&
            (tow.TowVehicle != null || tow.TowedEntity != null);
    }
}
