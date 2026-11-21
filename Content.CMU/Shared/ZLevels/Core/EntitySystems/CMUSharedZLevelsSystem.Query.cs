using System.Numerics;
using Content.Shared.CMU14.ZLevelBuilding;
using Content.Shared.CMU14.ZLevelBuilding.Pit;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Light.Components;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared.CMU14.ZLevels.Core.EntitySystems;

/// <summary>
/// Which meaning of "covered from above" <see cref="CMUSharedZLevelsSystem.IsCeiled"/> answers.
/// </summary>
public enum CMUZCeilingMode : byte
{
    /// <summary>
    /// The roof a person feels overhead: the roof bitmask of this level (ignored on generated stone),
    /// else a non-opening tile on the level above; a pit on the level above always means "open".
    /// </summary>
    Roof,

    /// <summary>The level above has a tile at this X/Y that something could stand on (not fall-through).</summary>
    FallThrough,

    /// <summary>The level above has a tile at this X/Y that is not an opening (not empty, not transparent).</summary>
    Opening,
}

/// <summary>
/// The Z-level query facade: one place for every "is there something above / is this a hole" question.
/// </summary>
/// <remarks>
/// <para>Every meaning in use, and where it lives:</para>
/// <list type="table">
///   <listheader>
///     <term>Meaning</term>
///     <description>Definition, owner, and the callers it serves</description>
///   </listheader>
///   <item>
///     <term>Opening tile</term>
///     <description>Empty, or <c>Transparent</c>.
///     <c>CMUZLevelOpeningCache.IsOpeningTile</c>. Light, projections,
///     view probes, roof writing, and <see cref="CMUZCeilingMode.Opening"/>.</description>
///   </item>
///   <item>
///     <term>Fall-through tile</term>
///     <description>Empty, or <c>cmuFallThrough</c> (sealed open air). Grates and lattices still hold you up.
///     <see cref="CMUZLevelOpeningCache.IsFallThroughTile"/>. Z-physics, <c>HasTileAbove</c>,
///     and <see cref="CMUZCeilingMode.FallThrough"/>.</description>
///   </item>
///   <item>
///     <term>Look-up tile</term>
///     <description>Empty, or <c>Transparent</c> without <c>cmuBlocksLookUp</c>.
///     <see cref="CMUZLevelOpeningCache.CanLookUpThrough"/>. Looking up from below only.</description>
///   </item>
///   <item>
///     <term>Entity opening</term>
///     <description>An anchored <see cref="CMUZLevelFallThroughComponent"/> on the tile: a pit, an open
///     hatch, a rope or ladder down. The tile under it is usually solid, so no tile predicate sees it.
///     <c>HasEntityOpening</c>.</description>
///   </item>
///   <item>
///     <term>Roof (<see cref="CMUZCeilingMode.Roof"/>)</term>
///     <description>This level's <see cref="RoofComponent"/> bitmask (tile flags only, no roof entities), skipped
///     when this level is generated non-localized stone, whose flags mean coverage by ANY level above, even distant
///     bedrock. Then a non-opening tile on the level above. A <see cref="PitComponent"/> on the level above vetoes
///     both. Lifted unchanged from the deployable ladder.</description>
///   </item>
///   <item>
///     <term>Ceiling (<see cref="CMUZCeilingMode.FallThrough"/>)</term>
///     <description>The level above has a non-fall-through tile (same as <c>HasTileAbove</c>).</description>
///   </item>
///   <item>
///     <term>Ceiling (<see cref="CMUZCeilingMode.Opening"/>)</term>
///     <description>The level above has a non-opening tile. Grates count as open here, as a ceiling in FallThrough.</description>
///   </item>
///   <item>
///     <term>Roof writers (STATE-05)</term>
///     <description>The two writers of the roof bitmask disagree, intentionally not reconciled.
///     <c>CMUSharedRoofSystem</c> (on a tile change) marks the levels below with <c>!IsOpeningTile</c> of the changed tile
///     and carries coverage further down with <c>!IsFallThroughTile</c> of each lower level's tile.
///     The server <c>CMURoofSystem</c> (on a network update) uses only <c>!IsOpeningTile</c>.
///     So a transparent but solid floor (a grate) between two levels can be "roofed" for the lower level after a tile
///     edit but not after a network rebuild. Changing either one changes lighting and weather on every map.</description>
///   </item>
/// </list>
/// <para>
/// Tile positions on another level are the same world X/Y (<see cref="TryGetSameXY"/>): level maps are grids at the
/// map origin, so tile indices line up between levels.
/// </para>
/// </remarks>
public abstract partial class CMUSharedZLevelsSystem
{
    /// <summary>
    /// Whether the position on <paramref name="map"/> is covered from the level above, in the sense of
    /// <paramref name="mode"/>. False when there is no level above (except a Roof-mode roof flag on this level).
    /// </summary>
    [PublicAPI]
    public bool IsCeiled(EntityUid map, Vector2 worldPos, CMUZCeilingMode mode)
    {
        switch (mode)
        {
            case CMUZCeilingMode.Roof:
                return IsRoofCeiled(map, worldPos);
            case CMUZCeilingMode.FallThrough:
            case CMUZCeilingMode.Opening:
                if (!TryMapUp(map, out var above) ||
                    !TryResolveMovementGrid(above.Value.Owner, worldPos, out var aboveGridUid, out var aboveGrid) ||
                    !_map.TryGetTileRef(aboveGridUid, aboveGrid, worldPos, out var tileRef))
                {
                    return false;
                }

                return mode == CMUZCeilingMode.FallThrough
                    ? !CMUZLevelOpeningCache.IsFallThroughTile(tileRef.Tile, TilDefMan)
                    : !CMUZLevelOpeningCache.IsOpeningTile(tileRef.Tile, TilDefMan);
            default:
                return false;
        }
    }

    /// <summary>
    /// Whether an anchored entity opening (<see cref="CMUZLevelFallThroughComponent"/>: a pit, an open
    /// hatch) is on the tile at this position. Ignores the tile itself.
    /// </summary>
    [PublicAPI]
    public bool HasEntityOpening(EntityUid map, Vector2 worldPos)
    {
        if (!TryResolveMovementGrid(map, worldPos, out var gridUid, out var grid))
            return false;

        return HasEntityOpening((gridUid, grid), _map.WorldToTile(gridUid, grid, worldPos));
    }

    /// <summary>
    /// Whether an anchored entity opening (<see cref="CMUZLevelFallThroughComponent"/>) is on this grid tile.
    /// </summary>
    [PublicAPI]
    public bool HasEntityOpening(Entity<MapGridComponent> grid, Vector2i tile)
    {
        return HasFallThroughAt(grid.Owner, grid.Comp, tile);
    }

    /// <summary>
    /// The same world X/Y on the level <paramref name="offset"/> away from <paramref name="map"/>.
    /// </summary>
    [PublicAPI]
    public bool TryGetSameXY(EntityUid map, int offset, Vector2 worldPos, out MapCoordinates coords)
    {
        return TryProjectToZMap(map, offset, worldPos, out coords, out _);
    }

    private bool IsRoofCeiled(EntityUid map, Vector2 worldPos)
    {
        Entity<MapGridComponent>? upper = null;
        if (TryMapUp(map, out var above) && _gridQuery.TryComp(above.Value.Owner, out var aboveGrid))
            upper = (above.Value.Owner, aboveGrid);

        // Level maps are grids at the map origin, so one index serves this level and the one above.
        Vector2i tile;
        if (_gridQuery.TryComp(map, out var grid))
            tile = _map.WorldToTile(map, grid, worldPos);
        else if (upper is { } up)
            tile = _map.WorldToTile(up.Owner, up.Comp, worldPos);
        else
            tile = new Vector2i((int) MathF.Floor(worldPos.X), (int) MathF.Floor(worldPos.Y));

        // a pit dug up from below leaves the floor tile above in place, but it's still a hole to climb through
        if (upper is { } pitGrid && HasPitAt(pitGrid, tile))
            return false;

        // Generated cave roof flags mean coverage by ANY level above, including distant bedrock.
        // Only the immediately adjacent ceiling determines whether a ladder fits inside a cavern.
        var generated = TryComp<ZGeneratedStoneComponent>(map, out var stone) && !stone.LocalizedToAuthoredLevel;
        if (!generated && TryComp<RoofComponent>(map, out var roof) && HasDirectRoofFlag(roof, tile))
            return true;

        if (upper is not { } upperGrid)
            return false;

        return !CMUZLevelOpeningCache.IsOpeningTile(upperGrid, tile, _map, TilDefMan);
    }

    // Tile flags only, the same as SharedRoofSystem.IsRooved(grid, index, queryEntities: false, entityRoof: false).
    private static bool HasDirectRoofFlag(RoofComponent roof, Vector2i tile)
    {
        var chunkOrigin = SharedMapSystem.GetChunkIndices(tile, RoofComponent.ChunkSize);
        if (!roof.Data.TryGetValue(chunkOrigin, out var bitMask))
            return false;

        var chunkRelative = SharedMapSystem.GetChunkRelative(tile, RoofComponent.ChunkSize);
        var bitFlag = (ulong) 1 << (chunkRelative.X + chunkRelative.Y * RoofComponent.ChunkSize);
        return (bitMask & bitFlag) == bitFlag;
    }

    private bool HasPitAt(Entity<MapGridComponent> grid, Vector2i tile)
    {
        var anchored = _map.GetAnchoredEntities(grid.Owner, grid.Comp, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (HasComp<PitComponent>(uid))
                return true;
        }

        return false;
    }
}
