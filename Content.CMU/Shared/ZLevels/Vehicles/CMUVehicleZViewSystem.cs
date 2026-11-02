using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Map;

namespace Content.Shared.CMU14.ZLevels.Vehicles;

public sealed class CMUVehicleZViewSystem : EntitySystem
{
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    [Dependency] private EntityLookupSystem _lookup = default!;

    private readonly HashSet<Entity<CMUZLevelViewerComponent>> _crew = new();

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
        SubscribeLocalEvent<VehicleComponent, MoveEvent>(OnHullMove);
    }

    // a roof over the hull ends the crew's look up
    private void OnHullMove(Entity<VehicleComponent> ent, ref MoveEvent args)
    {
        if (!TryComp(ent, out VehicleInteriorComponent? interior) ||
            interior.MapId == MapId.Nullspace ||
            !_zLevels.HasOpaqueAbove(ent))
        {
            return;
        }

        _crew.Clear();
        _lookup.GetEntitiesOnMap(interior.MapId, _crew);
        foreach (var crew in _crew)
        {
            _zLevels.TryDisableLookUp(crew);
        }
    }

    // nobody thrown out of a hull should keep looking up
    private void OnOccupantEjected(Entity<CMUZLevelViewerComponent> ent, ref CMUVehicleOccupantEjectedEvent args)
    {
        _zLevels.TryDisableLookUp(ent);
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
