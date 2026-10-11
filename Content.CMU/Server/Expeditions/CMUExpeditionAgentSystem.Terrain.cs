using System.Numerics;
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
    private readonly Dictionary<(MapId Map, Vector2i Cell), List<Box2>> _fireCells = new();
    private readonly Dictionary<MapId, List<Box2>> _fireBounds = new();
    private const float FireClearance = 0.1f;

    private void RefreshFireRegions()
    {
        _fireCells.Clear();
        _fireBounds.Clear();
        // Soft ignition sensors are absent from the hard-obstacle sweep. Index their
        // actual bounds once per update, including new spreading fire and extinguished tiles.
        var query = EntityQueryEnumerator<RMCIgniteOnCollideComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var transform))
        {
            if (TerminatingOrDeleted(uid) || HasComp<ProjectileComponent>(uid) || _mobs.IsAlive(uid))
                continue;
            var position = _transform.GetWorldPosition(transform);
            var bounds = _lookup.GetWorldAABB(uid, transform);
            // RMC also checks a 0.35 m neighborhood for static bodies standing on fire.
            bounds = new Box2(Vector2.Min(bounds.BottomLeft, position - new Vector2(0.35f)),
                Vector2.Max(bounds.TopRight, position + new Vector2(0.35f)));
            if (!_fireBounds.TryGetValue(transform.MapID, out var regions))
                _fireBounds[transform.MapID] = regions = new List<Box2>();
            regions.Add(bounds);
            for (var x = (int) MathF.Floor(bounds.Left); x <= (int) MathF.Floor(bounds.Right); x++)
            for (var y = (int) MathF.Floor(bounds.Bottom); y <= (int) MathF.Floor(bounds.Top); y++)
            {
                var key = (transform.MapID, new Vector2i(x, y));
                if (!_fireCells.TryGetValue(key, out var bucket))
                    _fireCells[key] = bucket = new List<Box2>();
                bucket.Add(bounds);
            }
        }
    }

    private bool FirePointSafe(EntityCoordinates point, float radius = AgentBodyRadius) =>
        FirePassageSafe(point, point, radius);

    private bool FirePassageSafe(EntityCoordinates from, EntityCoordinates to, float radius,
        bool escapingHazard = false)
    {
        var start = _transform.ToMapCoordinates(from);
        var end = _transform.ToMapCoordinates(to);
        if (start.MapId != end.MapId)
            return false;
        if (!_fireBounds.TryGetValue(start.MapId, out var regions))
            return true;
        radius += FireClearance;
        var minimum = Vector2.Min(start.Position, end.Position) - new Vector2(radius);
        var maximum = Vector2.Max(start.Position, end.Position) + new Vector2(radius);
        var left = (int) MathF.Floor(minimum.X);
        var bottom = (int) MathF.Floor(minimum.Y);
        var right = (int) MathF.Floor(maximum.X);
        var top = (int) MathF.Floor(maximum.Y);
        // Long diagonals use the finite fire snapshot instead of scanning thousands of empty cells.
        if ((long) (right - left + 1) * (top - bottom + 1) > 256)
        {
            foreach (var region in regions)
                if (!Clear(region))
                    return false;
            return true;
        }
        for (var x = left; x <= right; x++)
        for (var y = bottom; y <= top; y++)
            if (_fireCells.TryGetValue((start.MapId, new Vector2i(x, y)), out var bucket))
                foreach (var region in bucket)
                    if (!Clear(region))
                        return false;
        return true;

        bool Clear(Box2 region) => CMUFireAvoidancePolicy.PassageSafe(start.Position, end.Position,
            region.BottomLeft, region.TopRight, radius, escapingHazard);
    }

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
        // A route may cross touching grids on the same map. Resolve each sample to the
        // real supporting grid instead of treating the original grid's edge as a wall.
        if (!TrySquadCoordinates(point, out point))
            return false;
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || GrenadeDanger(point) || !FirePointSafe(point) ||
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
        var anchored = _maps.GetAnchoredEntities(point.EntityId, grid, indices);
        while (anchored.MoveNext(out var entity))
            if (HasComp<TileFireComponent>(entity) || HasComp<XenoAcidSplatterComponent>(entity) ||
                HasComp<XenoDespoilerLingeringAcidComponent>(entity) || HasComp<XenoDespoilerAcidSprayComponent>(entity))
                return false;
        _groundCache[key] = true;
        return true;
    }
}
