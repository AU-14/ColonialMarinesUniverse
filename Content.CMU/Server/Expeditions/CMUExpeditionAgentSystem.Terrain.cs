using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Water;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private TurfSystem _turf = default!;
    private readonly Dictionary<(EntityUid Grid, Vector2i Tile), bool> _groundCache = new();

    public bool TrySquadCoordinates(EntityCoordinates point, out EntityCoordinates ground)
    {
        ground = default;
        if (!_turf.TryGetTileRef(point, out var tile) || _turf.IsSpace(tile.Value))
            return false;
        ground = _transform.ToCoordinates(tile.Value.GridUid, _transform.ToMapCoordinates(point));
        return true;
    }

    private bool GroundSafe(EntityCoordinates point)
    {
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || GrenadeDanger(point) ||
            !TryComp<MapGridComponent>(point.EntityId, out var grid))
            return false;
        var indices = _maps.CoordinatesToTile(point.EntityId, grid, point);
        var key = (point.EntityId, indices);
        if (_groundCache.TryGetValue(key, out var safe))
            return safe;
        _groundCache[key] = false;
        if (!_maps.TryGetTileRef(point.EntityId, grid, indices, out var tile) || _turf.IsSpace(tile))
            return false;
        // Ordinary maps use live tiles and hazards. Generated terrain adds its water/cliff bounds.
        if (TryComp<CMUExpeditionMapComponent>(point.EntityId, out var expedition))
        {
            var plan = expedition.Plan;
            if (!expedition.Ready || indices.X < 1 || indices.Y < 1 || indices.X >= plan.Size - 1 || indices.Y >= plan.Size - 1 ||
                plan.Terrain[plan.Index(indices.X, indices.Y)] is CMUExpeditionTerrain.Water or CMUExpeditionTerrain.Cliff)
                return false;
            foreach (var fire in plan.FirePockets)
                if (Math.Abs(indices.X - fire.X) <= 3 && Math.Abs(indices.Y - fire.Y) <= 3)
                    return false;
        }
        var anchored = _maps.GetAnchoredEntitiesEnumerator(point.EntityId, grid, indices);
        while (anchored.MoveNext(out var entity))
            if (HasComp<RMCWaterComponent>(entity) || HasComp<TileFireComponent>(entity))
                return false;
        _groundCache[key] = true;
        return true;
    }
}
