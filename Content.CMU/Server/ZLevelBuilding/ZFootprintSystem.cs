using Content.Shared.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.CMU14.ZLevels.Ordnance;
using Content.Shared.Maps;
using Content.Shared.Physics;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.ZLevelBuilding;

/// <summary>
/// What every tile of a footprint must satisfy for <see cref="ZFootprintSystem.ValidateFootprint"/>.
/// </summary>
public sealed class ZFootprintRules
{
    /// <summary>Impassable at any height, as the dropship landing checks use.</summary>
    public const CollisionGroup DefaultBlockMask = CollisionGroup.Impassable |
                                                   CollisionGroup.LowImpassable |
                                                   CollisionGroup.MidImpassable |
                                                   CollisionGroup.HighImpassable;

    /// <summary>The tile exists and is something to stand on (not empty, not fall-through).</summary>
    public bool RequireSolidTile { get; init; } = true;

    /// <summary>
    /// No hole to the level below or view through the floor: not an opening tile (empty or transparent, so grates
    /// fail), not a fall-through tile, and no entity opening (a pit, an open hatch).
    /// </summary>
    public bool RejectOpenings { get; init; } = true;

    /// <summary>Hard fixtures on these layers block the tile (<c>TurfSystem.IsTileBlocked</c>). None skips the check.</summary>
    public CollisionGroup BlockMask { get; init; } = DefaultBlockMask;

    /// <summary>If set, each tile must be covered from the level above in this sense.</summary>
    public CMUZCeilingMode? RequireCeiling { get; init; }

    /// <summary>Each tile must be open to the sky through every level above (a descent from above).</summary>
    public bool RequireOpenSky { get; init; }

    /// <summary>
    /// Each tile must be one the support solver would hold up (<c>ZLevelSupportSystem.WouldBeSupported</c>, span 1,
    /// not vertical). Above ground only: on a level at depth 0 or below, or off the z-network, nothing collapses, so
    /// the rule passes.
    /// <para>
    /// With the default <see cref="RequireSolidTile"/>, an empty or fall-through tile already fails, so this rule then
    /// judges floors that already exist (a player floor with its marker). To judge a floor that is still to be laid,
    /// validate before laying it with <see cref="RequireSolidTile"/> and <see cref="RejectOpenings"/> off, as
    /// <c>TryPlaceStructureOnLevel</c> does.
    /// </para>
    /// </summary>
    public bool RequireSupport { get; init; }
}

/// <summary>
/// ARCH-03: the reusable NxM footprint check for placing a structure on a z-level grid. Fixed facing only: the
/// footprint is the axis-aligned rectangle <c>origin .. origin + size - 1</c> in grid tiles, no rotation.
/// </summary>
/// <remarks>
/// The per-tile predicates are lifted from the bespoke dropship checks (<c>MultiDeckDropshipSystem.Landing</c>,
/// <c>DropshipTacticalLandSystem.UpdateFootprint</c>), which are not migrated to it.
/// Server-side because the support rule needs the server support solver.
/// </remarks>
public sealed class ZFootprintSystem : EntitySystem
{
    // The dropship checks' blocking area: any real overlap blocks, rounding noise does not.
    private const float MinimumBlockingArea = 0.001f;

    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;
    [Dependency] private CMUTopDownOrdnanceSystem _topDown = default!;
    [Dependency] private ITileDefinitionManager _tileDefinitions = default!;
    [Dependency] private ZLevelSupportSystem _support = default!;

    /// <summary>
    /// Whether every tile of the footprint on <paramref name="grid"/> (on <paramref name="map"/>) passes
    /// <paramref name="rules"/>. When <paramref name="blocked"/> is given it receives every failing tile, not just
    /// the first. A non-positive size fails with nothing reported.
    /// </summary>
    [PublicAPI]
    public bool ValidateFootprint(
        EntityUid map,
        Entity<MapGridComponent> grid,
        Vector2i origin,
        Vector2i size,
        ZFootprintRules rules,
        ISet<Vector2i>? blocked = null)
    {
        if (size.X <= 0 || size.Y <= 0)
            return false;

        MapId? mapId = rules.RequireOpenSky && TryComp<MapComponent>(map, out var mapComp) ? mapComp.MapId : null;
        var checkSupport = rules.RequireSupport && TryComp<CMUZLevelMapComponent>(map, out var zMap) && zMap.Depth > 0;
        var valid = true;
        for (var x = 0; x < size.X; x++)
        {
            for (var y = 0; y < size.Y; y++)
            {
                var tile = origin + new Vector2i(x, y);
                if (IsTileValid(map, grid, tile, rules, mapId, checkSupport))
                    continue;

                valid = false;
                if (blocked == null)
                    return false;

                blocked.Add(tile);
            }
        }

        return valid;
    }

    private bool IsTileValid(EntityUid map, Entity<MapGridComponent> grid, Vector2i tile, ZFootprintRules rules, MapId? mapId, bool checkSupport)
    {
        var hasTile = _map.TryGetTileRef(grid.Owner, grid.Comp, tile, out var tileRef) && !tileRef.Tile.IsEmpty;

        if (rules.RequireSolidTile &&
            (!hasTile || CMUZLevelOpeningCache.IsFallThroughTile(tileRef.Tile, _tileDefinitions)))
        {
            return false;
        }

        if (rules.RejectOpenings &&
            (!hasTile ||
             CMUZLevelOpeningCache.IsOpeningTile(tileRef.Tile, _tileDefinitions) ||
             CMUZLevelOpeningCache.IsFallThroughTile(tileRef.Tile, _tileDefinitions) ||
             _zLevels.HasEntityOpening(grid, tile)))
        {
            return false;
        }

        if (rules.BlockMask != CollisionGroup.None &&
            _turf.IsTileBlocked(grid.Owner, tile, rules.BlockMask, grid.Comp, minIntersectionArea: MinimumBlockingArea))
        {
            return false;
        }

        if (checkSupport && !_support.WouldBeSupported(map, grid, tile, span: 1, vertical: false))
            return false;

        if (rules.RequireCeiling == null && !rules.RequireOpenSky)
            return true;

        var worldPos = _map.GridTileToWorldPos(grid.Owner, grid.Comp, tile);
        if (rules.RequireCeiling is { } mode && !_zLevels.IsCeiled(map, worldPos, mode))
            return false;

        if (rules.RequireOpenSky &&
            (mapId is not { } id || !_topDown.IsOpenToSky(new MapCoordinates(worldPos, id))))
        {
            return false;
        }

        return true;
    }
}
