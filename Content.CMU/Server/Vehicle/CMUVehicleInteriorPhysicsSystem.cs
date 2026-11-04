using Content.Shared._RMC14.Vehicle;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server.CMU14.Vehicle;

public sealed partial class CMUVehicleInteriorPhysicsSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VehicleComponent, VehicleInteriorLoadedEvent>(OnInteriorLoaded);
    }

    private void OnInteriorLoaded(Entity<VehicleComponent> ent, ref VehicleInteriorLoadedEvent args)
    {
        if (!TryComp<PhysicsComponent>(args.Grid, out var physics))
            return;

        // The cabin is a stationary room on its own map. Its rotation also drives the occupant's camera.
        _physics.ResetDynamics(args.Grid, physics);
        _physics.SetBodyType(args.Grid, BodyType.Static, body: physics);
    }
}
