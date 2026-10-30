using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.Doors;

/// <summary>
///     Tells a door whether the air beside it is unsafe: too thin on some side, or rushing past it. Shared by
///     self-sealing firelocks and self-bolting airlocks so they react to the same conditions.
/// </summary>
public sealed partial class CMUAtmosHazardSystem : EntitySystem
{
    private const string SpaceTile = "Space";

    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private ITileDefinitionManager _tileDefinitions = default!;

    /// <summary>
    ///     True if any side of this door is below <paramref name="lowPressure"/>, or the pressures around it differ by
    ///     more than <paramref name="pressureDifference"/>. Sides closed off by a wall or another airtight thing don't
    ///     count. <paramref name="includeOwnTile"/> also samples the door's own tile, for a door that's open.
    /// </summary>
    public bool IsHazardous(EntityUid uid, TransformComponent xform, float lowPressure, float pressureDifference, bool includeOwnTile)
    {
        if (xform.GridUid is not { } gridUid ||
            !HasComp<GridAtmosphereComponent>(gridUid) ||
            !TryComp(gridUid, out MapGridComponent? grid))
        {
            return false;
        }

        var pos = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates);
        var min = float.MaxValue;
        var max = float.MinValue;

        void Sample(Vector2i tile)
        {
            float pressure;
            if (_atmosphere.GetTileMixture(gridUid, xform.MapUid, tile) is { } mixture)
                pressure = mixture.Pressure;
            else if (IsSpace(gridUid, grid, tile))
                pressure = 0;
            else
                return;

            min = Math.Min(min, pressure);
            max = Math.Max(max, pressure);
        }

        if (includeOwnTile)
            Sample(pos);

        for (var i = 0; i < Atmospherics.Directions; i++)
        {
            var dir = (AtmosDirection) (1 << i);
            var adjacent = pos.Offset(dir);
            if (!HasAirtightBlocker(gridUid, grid, adjacent, dir.GetOpposite()))
                Sample(adjacent);
        }

        if (min > max)
            return false;

        return min < lowPressure || max - min > pressureDifference;
    }

    private bool IsSpace(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
            return true;

        return _tileDefinitions[tileRef.Tile.TypeId].ID == SpaceTile;
    }

    private bool HasAirtightBlocker(EntityUid gridUid, MapGridComponent grid, Vector2i tile, AtmosDirection dir)
    {
        var anchored = _map.GetAnchoredEntities(gridUid, grid, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (TryComp(uid, out AirtightComponent? airtight) &&
                airtight.AirBlocked &&
                (airtight.AirBlockedDirection & dir) == dir)
            {
                return true;
            }
        }

        return false;
    }
}
