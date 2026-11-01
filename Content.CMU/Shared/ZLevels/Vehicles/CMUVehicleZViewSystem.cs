using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Vehicle.Components;

namespace Content.Shared.CMU14.ZLevels.Vehicles;

public sealed class CMUVehicleZViewSystem : EntitySystem
{
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    private EntityQuery<EyeComponent> _eyeQuery;
    private EntityQuery<VehicleComponent> _vehicleQuery;
    private EntityQuery<CMUZLevelViewerComponent> _viewerQuery;

    public override void Initialize()
    {
        base.Initialize();

        _eyeQuery = GetEntityQuery<EyeComponent>();
        _vehicleQuery = GetEntityQuery<VehicleComponent>();
        _viewerQuery = GetEntityQuery<CMUZLevelViewerComponent>();

        SubscribeLocalEvent<CMUZLevelViewerComponent, CMUVehicleOccupantEjectedEvent>(OnOccupantEjected);
    }

    // nobody thrown out of a hull should keep looking up
    private void OnOccupantEjected(Entity<CMUZLevelViewerComponent> ent, ref CMUVehicleOccupantEjectedEvent args)
    {
        _zLevels.TryDisableLookUp(ent);
    }

    /// <summary>A roof over the hull blocks looking up from inside, as it does on foot.</summary>
    public bool IsLookUpBlocked(EntityUid user)
    {
        var source = GetViewSource(user);
        return source != user && _zLevels.HasOpaqueAbove(source);
    }

    /// <summary>The vehicle while the user's eye is on its hull, otherwise the user.</summary>
    public EntityUid GetViewSource(EntityUid user)
    {
        return _eyeQuery.TryComp(user, out var eye) &&
               eye.Target is { } target &&
               _vehicleQuery.HasComp(target) &&
               _viewerQuery.HasComp(target)
            ? target
            : user;
    }
}
