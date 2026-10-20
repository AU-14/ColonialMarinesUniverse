using Content.Shared._RMC14.Rules;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._RMC14.CrashLand;

// CMU14 class
public abstract partial class SharedCrashLandSystem
{
    private const int FootprintAttempts = 250;

    // the single-tile picker only checked where the corner lands, so lifeboats near the planet edge hung off
    // into the void. this checks every hull tile fits; the location is the tile the shuttle's (0,0) tile covers
    public bool TryGetCrashLandLocation(EntityUid shuttle, out EntityCoordinates location)
    {
        location = default;
        if (!TryComp<MapGridComponent>(shuttle, out var shuttleGrid))
            return TryGetCrashLandLocation(out location);

        var footprint = new List<Vector2i>();
        var shuttleTiles = _mapSystem.GetAllTiles(shuttle, shuttleGrid);
        while (shuttleTiles.MoveNext(out var tile))
        {
            footprint.Add(tile.Value.GridIndices);
        }

        if (footprint.Count == 0)
            return TryGetCrashLandLocation(out location);

        var planets = EntityQueryEnumerator<RMCPlanetComponent, MapGridComponent>();
        while (planets.MoveNext(out var planet, out _, out var planetGrid))
        {
            // roll inside the planet's real bounds instead of a fixed box that runs past small maps
            var (minX, maxX, minY, maxY) = PlanetTileBounds(planet, planetGrid);

            for (var i = 0; i < FootprintAttempts; i++)
            {
                var anchor = new Vector2i(_random.Next(minX, maxX), _random.Next(minY, maxY));
                if (!_mapSystem.TryGetTileRef(planet, planetGrid, anchor, out var anchorRef) ||
                    !IsLandableTile((planet, planetGrid), anchorRef) ||
                    !FootprintFits((planet, planetGrid), anchor, footprint))
                {
                    continue;
                }

                location = _mapSystem.GridTileToLocal(planet, planetGrid, anchor);
                return true;
            }
        }

        // nowhere fits the whole hull; keep the old behaviour rather than silently cancelling the crash
        return TryGetCrashLandLocation(out location);
    }

    // max is exclusive, ready for _random.Next
    private (int MinX, int MaxX, int MinY, int MaxY) PlanetTileBounds(EntityUid planet, MapGridComponent grid)
    {
        var bounds = grid.LocalAABB;
        if (bounds.Width > 0 && bounds.Height > 0)
        {
            return ((int) MathF.Floor(bounds.Left), (int) MathF.Ceiling(bounds.Right),
                (int) MathF.Floor(bounds.Bottom), (int) MathF.Ceiling(bounds.Top));
        }

        // the engine never builds LocalAABB for planet maps (grid on the map entity, no chunk fixtures),
        // so rolling in the ±200 fallback box mostly missed small planets. walk the tiles instead
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        var tiles = _mapSystem.GetAllTilesEnumerator(planet, grid);
        while (tiles.MoveNext(out var tile))
        {
            var index = tile.Value.GridIndices;
            minX = Math.Min(minX, index.X);
            minY = Math.Min(minY, index.Y);
            maxX = Math.Max(maxX, index.X);
            maxY = Math.Max(maxY, index.Y);
        }

        if (maxX < minX)
            return (-200, 200, -200, 200);

        return (minX, maxX + 1, minY, maxY + 1);
    }

    private bool FootprintFits(Entity<MapGridComponent> planet, Vector2i anchor, List<Vector2i> footprint)
    {
        foreach (var offset in footprint)
        {
            if (!_mapSystem.TryGetTileRef(planet, planet.Comp, anchor + offset, out var tileRef) ||
                tileRef.Tile.IsEmpty ||
                _turf.IsSpace(tileRef))
            {
                return false;
            }
        }

        return true;
    }
}
