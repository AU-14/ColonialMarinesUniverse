using System.Numerics;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Maps;
using Robust.Shared.Map;

namespace Content.Shared.CMU14.ZLevels.Core.EntitySystems;

public abstract partial class CMUSharedZLevelsSystem
{
    /// <summary>
    /// An upper-deck stair opening belongs to the stairwell beneath it, even when its floor tile is missing.
    /// Plain holes and wall tops do not borrow another deck's environment.
    /// </summary>
    public bool TryGetStairEnvironment(MapCoordinates coordinates, out EntityCoordinates environment)
    {
        environment = default;
        if (!_map.TryGetMap(coordinates.MapId, out var map)
            || !TryMapDown(map.Value, out var below)
            || !TryResolveMovementGrid(below.Value.Owner, coordinates.Position, out var gridUid, out var grid))
        {
            return false;
        }

        var tile = _map.WorldToTile(gridUid, grid, coordinates.Position);
        if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty
            || ((ContentTileDefinition) TilDefMan[tileRef.Tile.TypeId]).MapAtmosphere)
        {
            return false;
        }

        var gridLocal = _map.WorldToLocal(gridUid, grid, coordinates.Position) / grid.TileSize;
        for (var x = -1; x <= 1; x++)
        for (var y = -1; y <= 1; y++)
        {
            var candidate = tile + new Vector2i(x, y);
            var local = gridLocal - new Vector2(candidate.X, candidate.Y);
            if (!CanReachHighGroundSupport(local))
                continue;

            var anchored = _map.GetAnchoredEntities(gridUid, grid, candidate);
            while (anchored.MoveNext(out var uid))
            {
                if (!TryComp<CMUZLevelHighGroundComponent>(uid, out var ramp) || !ramp.Stick
                    || ramp.SupportOnlyFromAbove || ramp.HeightCurve.Count < 2
                    || !TryGetHighGroundCurveT(GetHighGroundGridDirection(uid.Value), ramp, local, x == 0 && y == 0, out _))
                {
                    continue;
                }

                var first = ramp.HeightCurve[0];
                var last = ramp.HeightCurve[^1];
                if (Math.Max(first, last) < 0.95f || Math.Abs(first - last) < 0.01f)
                    continue;

                environment = _transform.ToCoordinates(gridUid, new MapCoordinates(coordinates.Position, Transform(gridUid).MapID));
                return true;
            }
        }

        return false;
    }
}
