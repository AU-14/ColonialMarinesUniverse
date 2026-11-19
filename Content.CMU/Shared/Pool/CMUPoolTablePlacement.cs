using Content.Shared.Construction;
using Content.Shared.Construction.Conditions;
using Content.Shared.Maps;
using Content.Shared.Physics;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared.CMU14.Pool;

[UsedImplicitly]
[DataDefinition]
public sealed partial class CMUPoolTablePlacement : IConstructionCondition
{
    public bool RecheckOnCompletion => true;

    public bool Condition(EntityUid user, EntityCoordinates location, Direction direction)
    {
        var entities = IoCManager.Resolve<IEntityManager>();
        var turf = entities.System<TurfSystem>();
        if (!turf.TryGetTileRef(location, out var center)
            || !entities.TryGetComponent<MapGridComponent>(center.Value.GridUid, out var grid))
            return false;

        var gridUid = center.Value.GridUid;
        var transforms = entities.System<SharedTransformSystem>();
        var gridDirection = (direction.ToAngle()
            + transforms.GetWorldRotation(location.EntityId)
            - transforms.GetWorldRotation(gridUid)).GetCardinalDir();
        var axis = gridDirection is Direction.East or Direction.West
            ? new Vector2i(0, 1)
            : new Vector2i(1, 0);

        var map = entities.System<SharedMapSystem>();
        for (var offset = -1; offset <= 1; offset++)
        {
            var indices = center.Value.GridIndices + axis * offset;
            if (!map.TryGetTileRef(gridUid, grid, indices, out var tile)
                || turf.IsSpace(tile)
                || !turf.GetContentTileDefinition(tile).Sturdy
                || turf.IsTileBlocked(tile, CollisionGroup.TableMask))
                return false;
        }

        return true;
    }

    public ConstructionGuideEntry GenerateGuideEntry() => new()
    {
        Localization = "cmu-pool-table-placement-blocked",
    };
}
