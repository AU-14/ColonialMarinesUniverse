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
            var bounds = planetGrid.LocalAABB;
            var minX = (int) MathF.Floor(bounds.Left);
            var maxX = (int) MathF.Ceiling(bounds.Right);
            var minY = (int) MathF.Floor(bounds.Bottom);
            var maxY = (int) MathF.Ceiling(bounds.Top);
            if (maxX <= minX || maxY <= minY)
            {
                // bounds not built yet, so use the same box the single-tile picker rolls in
                (minX, maxX, minY, maxY) = (-200, 200, -200, 200);
            }

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
