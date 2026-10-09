using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.OnCollide;
using Content.Shared._RMC14.Xenonids.Spray;
using Content.Shared._RMC14.Xenonids.Despoiler;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.Maps;
using Content.Shared.Projectiles;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private TurfSystem _turf = default!;
    private readonly Dictionary<(EntityUid Grid, Vector2i Tile), bool> _groundCache = new();
    private readonly HashSet<(EntityUid Grid, Vector2i Tile)> _acidTiles = new();

    private void RefreshAcidTiles()
    {
        _acidTiles.Clear();
        // RMC's short-lived spray splatters are static sensors, but are not anchored.
        // Index them once per frame, not once for every candidate route tile.
        var query = EntityQueryEnumerator<DamageOnCollideComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var acid, out var transform))
        {
            if (!acid.Acidic || HasComp<ProjectileComponent>(uid) || _mobs.IsAlive(uid) ||
                !TrySquadCoordinates(transform.Coordinates, out var point) ||
                !TryComp<MapGridComponent>(point.EntityId, out var grid))
                continue;
            _acidTiles.Add((point.EntityId, _maps.CoordinatesToTile(point.EntityId, grid, point)));
        }
    }

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
        if (_acidTiles.Contains(key))
            return false;
        if (_groundCache.TryGetValue(key, out var safe))
            return safe;
        _groundCache[key] = false;
        if (!_maps.TryGetTileRef(point.EntityId, grid, indices, out var tile) || _turf.IsSpace(tile))
            return false;
        // RMC water is walkable; its contact system owns the slowdown. Solid banks and
        // map boundaries are rejected by BodyFits, just like walls on ordinary maps.
        if (TryComp<CMUExpeditionMapComponent>(point.EntityId, out var expedition))
        {
            var plan = expedition.Plan;
            if (!expedition.Ready || indices.X < 1 || indices.Y < 1 || indices.X >= plan.Size - 1 || indices.Y >= plan.Size - 1 ||
                plan.Terrain[plan.Index(indices.X, indices.Y)] == CMUExpeditionTerrain.Cliff)
                return false;
            foreach (var fire in plan.FirePockets)
                if (Math.Abs(indices.X - fire.X) <= 3 && Math.Abs(indices.Y - fire.Y) <= 3)
                    return false;
        }
        var anchored = _maps.GetAnchoredEntitiesEnumerator(point.EntityId, grid, indices);
        while (anchored.MoveNext(out var entity))
            if (HasComp<TileFireComponent>(entity) || HasComp<XenoAcidSplatterComponent>(entity) ||
                HasComp<XenoDespoilerLingeringAcidComponent>(entity) || HasComp<XenoDespoilerAcidSprayComponent>(entity))
                return false;
        _groundCache[key] = true;
        return true;
    }
}
