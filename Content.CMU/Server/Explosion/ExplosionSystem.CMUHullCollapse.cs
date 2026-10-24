using Content.Shared.CMU14.Ship;
using Content.Shared.Explosion;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server.Explosion.EntitySystems;

public sealed partial class ExplosionSystem
{
    private const string CMUSpaceTile = "Space";

    /// <summary>
    ///     On a deck with <see cref="CMUHullCollapsingMapComponent"/>, any floor tile an explosion breaks goes straight
    ///     to space, indestructible or not, the way multi-Z grates do. Returns false off such decks, so the explosion
    ///     breaks tiles as usual.
    /// </summary>
    private bool CMUTryCollapseHullTile(
        TileRef tileRef,
        float effectiveIntensity,
        int maxTileBreak,
        List<(Vector2i GridIndices, Tile Tile)> damagedTiles,
        ExplosionPrototype type)
    {
        var map = Transform(tileRef.GridUid).MapUid ?? tileRef.GridUid;
        if (!TryComp(map, out CMUHullCollapsingMapComponent? collapsing))
            return false;

        // Explosions told not to break tiles still don't.
        if (maxTileBreak <= 0 || tileRef.Tile.IsEmpty)
            return true;

        var tileDef = (ContentTileDefinition) _tileDefinitionManager[tileRef.Tile.TypeId];
        if (tileDef.ID == CMUSpaceTile || tileDef.CMUFallThrough)
            return true;

        if (!_robustRandom.Prob(type.TileBreakChance(effectiveIntensity)))
            return true;

        if (_robustRandom.Prob(collapsing.DebrisChance))
            Spawn(collapsing.Debris, _map.GridTileToLocal(tileRef.GridUid, Comp<MapGridComponent>(tileRef.GridUid), tileRef.GridIndices));

        damagedTiles.Add((tileRef.GridIndices, new Tile(_tileDefinitionManager[CMUSpaceTile].TileId)));
        return true;
    }
}
