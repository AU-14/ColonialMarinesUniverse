using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Robust.Shared.Map.Components;

public static class MapGridComponentCompatibilityExtensions
{
    public static Vector2i TileIndicesFor(this MapGridComponent grid, EntityCoordinates coords)
    {
        var map = IoCManager.Resolve<IEntityManager>().System<SharedMapSystem>();
        return map.TileIndicesFor(grid.Owner, grid, coords);
    }

    public static IEnumerable<EntityUid> GetAnchoredEntities(this MapGridComponent grid, Vector2i pos)
    {
        var map = IoCManager.Resolve<IEntityManager>().System<SharedMapSystem>();
        return map.GetAnchoredEntities(grid.Owner, grid, pos);
    }
}
