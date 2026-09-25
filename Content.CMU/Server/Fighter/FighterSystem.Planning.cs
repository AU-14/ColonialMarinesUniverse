using System.Numerics;
using Content.Shared.CMU14.Fighter;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Fighter;

public sealed partial class FighterSystem
{
    private void OnPlan(FighterPlanEvent ev, EntitySessionEventArgs args)
    {
        if (!TrySetPlan(args.SenderSession.AttachedEntity, ev.Entry, ev.Exit) && args.SenderSession.AttachedEntity is { } user)
            _popup.PopupEntity(Loc.GetString("cmu-fighter-invalid-route"), user, user);
    }

    public bool TrySetPlan(EntityUid? player, Vector2 entry, Vector2 exit)
    {
        if (!TryGetSeat(player, out var seat, out var aircraft) || !seat.Comp.Pilot ||
            !FighterFlight.TryPlan(aircraft.Comp, entry, exit))
            return false;
        CancelQueuedFire(seat);
        Dirty(aircraft);
        return true;
    }

    private void OnSettings(FighterSettingsEvent ev, EntitySessionEventArgs args) =>
        TrySetSettings(args.SenderSession.AttachedEntity, ev.Height, ev.Speed);

    public bool TrySetSettings(EntityUid? player, float height, float speed)
    {
        if (!TryGetSeat(player, out var seat, out var aircraft) || !seat.Comp.Pilot ||
            !FighterFlight.TrySettings(aircraft.Comp, height, speed))
            return false;
        Dirty(aircraft);
        return true;
    }

    private void BuildChart(EntityUid terrain, FighterAircraftComponent aircraft, FighterChartComponent chart)
    {
        Entity<MapGridComponent>? selected = null;
        var area = 0f;
        if (TryComp(terrain, out MapGridComponent? mapGrid))
        {
            selected = (terrain, mapGrid);
            var mapBounds = TerrainBounds((terrain, mapGrid));
            area = mapBounds.Width * mapBounds.Height;
        }
        foreach (var candidate in _map.GetAllGrids(Transform(terrain).MapID))
        {
            var box = candidate.Comp.LocalAABB;
            if (box.Width * box.Height <= area)
                continue;
            area = box.Width * box.Height;
            selected = candidate;
        }
        if (selected is not { } grid)
            return;

        var matrix = _transform.GetWorldMatrix(grid);
        var local = TerrainBounds(grid);
        if (local.Width <= 0 || local.Height <= 0)
            return;
        var min = new Vector2(float.MaxValue);
        var max = new Vector2(float.MinValue);
        foreach (var corner in new[] { local.BottomLeft, local.BottomRight, local.TopLeft, local.TopRight })
        {
            var world = Vector2.Transform(corner, matrix);
            min = Vector2.Min(min, world);
            max = Vector2.Max(max, world);
        }
        aircraft.Battlefield = new Box2(min, max);
        aircraft.Home = aircraft.Battlefield.Center;
        aircraft.AirspaceRadius = Math.Max(aircraft.Battlefield.Width, aircraft.Battlefield.Height) * .8f + 90;
        var resolution = FighterChartComponent.Resolution;
        chart.Terrain = new byte[resolution * resolution];
        var mapId = Transform(terrain).MapID;
        for (var y = 0; y < resolution; y++)
        for (var x = 0; x < resolution; x++)
        {
            var point = min + (max - min) * new Vector2((x + .5f) / resolution, (y + .5f) / resolution);
            var tile = _map.GetTileRef(grid, new MapCoordinates(point, mapId)).Tile;
            if (tile.IsEmpty)
                continue;
            var name = _tiles[tile.TypeId].Name;
            chart.Terrain[y * resolution + x] = (byte) (
                name.Contains("water", StringComparison.OrdinalIgnoreCase) || name.Contains("river", StringComparison.OrdinalIgnoreCase) ? 2 :
                name.Contains("grass", StringComparison.OrdinalIgnoreCase) || name.Contains("dirt", StringComparison.OrdinalIgnoreCase) ? 3 :
                name.Contains("road", StringComparison.OrdinalIgnoreCase) || name.Contains("asphalt", StringComparison.OrdinalIgnoreCase) ? 4 : 1);
        }
    }

    private Box2 TerrainBounds(Entity<MapGridComponent> grid)
    {
        // Map grids have no physics fixtures, so their cached collision AABB may be empty.
        if (grid.Comp.LocalAABB.Width > 0 && grid.Comp.LocalAABB.Height > 0)
            return grid.Comp.LocalAABB;
        var min = new Vector2(float.MaxValue);
        var max = new Vector2(float.MinValue);
        var found = false;
        foreach (var tile in _map.GetAllTiles(grid.Owner, grid.Comp))
        {
            var point = (Vector2) tile.GridIndices * grid.Comp.TileSize;
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point + grid.Comp.TileSizeVector);
            found = true;
        }
        return found ? new Box2(min, max) : default;
    }
}
