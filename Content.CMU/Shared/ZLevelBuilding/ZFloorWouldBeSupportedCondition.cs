using Content.Shared.Construction;
using Content.Shared.Construction.Conditions;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared.CMU14.ZLevelBuilding;

/// <summary>
/// Asks whether a floor laid on <see cref="Tile"/> of <see cref="Grid"/> would be held up by the structural support
/// graph. The server answers it (<c>ZLevelSupportSystem</c>). Nothing answers it on the client, so construction
/// ghosts stay permissive and the server decides when the build starts and again when it completes.
/// </summary>
[ByRefEvent]
public struct ZFloorSupportQueryEvent
{
    public readonly EntityUid Map;
    public readonly EntityUid Grid;
    public readonly Vector2i Tile;

    public bool Allowed = true;

    public ZFloorSupportQueryEvent(EntityUid map, EntityUid grid, Vector2i tile)
    {
        Map = map;
        Grid = grid;
        Tile = tile;
    }
}

/// <summary>
/// A construction condition for the plating recipe. On an upper level it refuses a floor over an empty or
/// fall-through tile that nothing would hold up, so no materials are spent on a floor that collapses five seconds
/// later (GraphAudit FLOW-03). Floors on solid ground, on the ground level and below, and on supported spots pass.
/// </summary>
[UsedImplicitly]
[DataDefinition]
public sealed partial class ZFloorWouldBeSupported : IConstructionCondition
{
    public bool RecheckOnCompletion => true;

    public bool Condition(EntityUid user, EntityCoordinates location, Direction direction)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();

        // location.EntityId is the grid (or map-grid) the ghost is over; walk to its grid and map.
        if (!entMan.TryGetComponent(location.EntityId, out TransformComponent? xform) || xform.MapUid is not { } mapUid)
            return true; // can't resolve the map - don't block.

        var gridUid = entMan.HasComponent<MapGridComponent>(location.EntityId) ? location.EntityId : xform.GridUid;
        if (gridUid is not { } grid || !entMan.TryGetComponent(grid, out MapGridComponent? gridComp))
            return true; // no grid to lay a floor on - don't block.

        var tile = entMan.System<SharedMapSystem>().TileIndicesFor(grid, gridComp, location);
        var ev = new ZFloorSupportQueryEvent(mapUid, grid, tile);
        entMan.EventBus.RaiseEvent(EventSource.Local, ref ev);
        return ev.Allowed;
    }

    public ConstructionGuideEntry GenerateGuideEntry()
    {
        return new ConstructionGuideEntry
        {
            Localization = "cmu-construction-step-condition-zfloor-would-be-supported",
        };
    }
}
