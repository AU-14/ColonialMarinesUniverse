using System.Diagnostics.CodeAnalysis;
using Content.Shared._CMU14.ZLevels.Core;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Content.Shared._RMC14.Areas;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._CMU14.ZLevels.Ordnance;

public sealed partial class CMUTopDownOrdnanceSystem : EntitySystem
{
    [Dependency] private AreaSystem _area = default!;
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private ITileDefinitionManager _tile = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    private EntityQuery<CMUZLevelMapComponent> _zMapQuery;

    public override void Initialize()
    {
        _zMapQuery = GetEntityQuery<CMUZLevelMapComponent>();
    }

    /// <summary>
    /// Resolves an X/Y target into the top-down surfaces affected by the ordnance.
    /// Surfaces are returned from highest to lowest.
    /// </summary>
    public bool TryResolveImpactColumn(
        MapCoordinates selected,
        CMUTopDownOrdnanceKind kind,
        [NotNullWhen(true)] out CMUTopDownOrdnanceResult? result)
    {
        result = new CMUTopDownOrdnanceResult(selected);

        if (!_map.TryGetMap(selected.MapId, out var mapUid) ||
            mapUid is not { } resolvedMapUid)
        {
            result.BlockReason = CMUTopDownOrdnanceBlockReason.NoMap;
            return false;
        }

        if (!_zLevels.TryGetZNetwork(resolvedMapUid, out var network) ||
            !_zLevels.TryGetDepthBounds(network.Value, out var minDepth, out var maxDepth))
        {
            return TryResolveSingleSurface(selected, kind, result);
        }

        result.UsesZLevels = true;
        for (var depth = maxDepth; depth >= minDepth; depth--)
        {
            if (!_zLevels.TryGetMapAtDepth(network.Value, depth, out var map, out var mapComp))
                continue;

            var coordinates = new MapCoordinates(selected.Position, mapComp.MapId);
            if (!TryGetBlockingSurface(coordinates, depth, out var surface))
                continue;

            if (!CanAffect(surface.Coordinates, kind, out var blockReason))
            {
                result.BlockReason = blockReason;
                return false;
            }

            result.Surfaces.Add(surface);
        }

        if (result.Surfaces.Count > 0)
            return true;

        result.BlockReason = CMUTopDownOrdnanceBlockReason.NoSurface;
        return false;
    }

    /// <summary>
    /// Resolves the next allowed blocking surface below a surface that carving ordnance has already opened.
    /// </summary>
    public bool TryResolveNextSurfaceBelow(
        MapCoordinates current,
        CMUTopDownOrdnanceKind kind,
        [NotNullWhen(true)] out CMUTopDownOrdnanceSurface? surface,
        out CMUTopDownOrdnanceBlockReason blockReason)
    {
        return TryResolveSurfaceBelow(current, kind, true, out surface, out blockReason);
    }

    /// <summary>
    /// Resolves the next allowed blocking surface below a surface before deciding whether to carve it open.
    /// </summary>
    public bool TryResolveSurfaceBelow(
        MapCoordinates current,
        CMUTopDownOrdnanceKind kind,
        [NotNullWhen(true)] out CMUTopDownOrdnanceSurface? surface,
        out CMUTopDownOrdnanceBlockReason blockReason)
    {
        return TryResolveSurfaceBelow(current, kind, false, out surface, out blockReason);
    }

    private bool TryResolveSurfaceBelow(
        MapCoordinates current,
        CMUTopDownOrdnanceKind kind,
        bool requireCurrentOpening,
        [NotNullWhen(true)] out CMUTopDownOrdnanceSurface? surface,
        out CMUTopDownOrdnanceBlockReason blockReason)
    {
        surface = null;
        blockReason = CMUTopDownOrdnanceBlockReason.None;

        if (requireCurrentOpening && !IsOpening(current))
            return false;

        if (!_map.TryGetMap(current.MapId, out var mapUid) ||
            mapUid is not { } resolvedMapUid ||
            !_zLevels.TryGetZNetwork(resolvedMapUid, out var network) ||
            !_zLevels.TryGetDepthBounds(network.Value, out var minDepth, out _) ||
            !TryGetDepth(network.Value, resolvedMapUid, out var currentDepth))
        {
            return false;
        }

        for (var depth = currentDepth - 1; depth >= minDepth; depth--)
        {
            if (!_zLevels.TryGetMapAtDepth(network.Value, depth, out _, out var mapComp))
                continue;

            var coordinates = new MapCoordinates(current.Position, mapComp.MapId);
            if (!TryGetBlockingSurface(coordinates, depth, out var found))
                continue;

            if (!CanAffect(found.Coordinates, kind, out blockReason))
                return false;

            surface = found;
            return true;
        }

        blockReason = CMUTopDownOrdnanceBlockReason.NoSurface;
        return false;
    }

    public bool IsOpening(MapCoordinates coordinates)
    {
        if (!_mapManager.TryFindGridAt(coordinates, out var gridUid, out var grid))
            return true;

        var tile = _map.WorldToTile(gridUid, grid, coordinates.Position);
        if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef))
            return true;

        return CMUZLevelOpeningCache.IsOpeningTile(tileRef.Tile, _tile);
    }

    private bool TryResolveSingleSurface(
        MapCoordinates selected,
        CMUTopDownOrdnanceKind kind,
        CMUTopDownOrdnanceResult result)
    {
        if (!TryGetBlockingSurface(selected, 0, out var surface))
        {
            result.BlockReason = CMUTopDownOrdnanceBlockReason.NoSurface;
            return false;
        }

        if (!CanAffect(surface.Coordinates, kind, out var blockReason))
        {
            result.BlockReason = blockReason;
            return false;
        }

        result.Surfaces.Add(surface);
        return true;
    }

    private bool TryGetBlockingSurface(
        MapCoordinates coordinates,
        int depth,
        out CMUTopDownOrdnanceSurface surface)
    {
        surface = default;
        if (IsOpening(coordinates))
            return false;

        surface = new CMUTopDownOrdnanceSurface(coordinates, depth);
        return true;
    }

    private bool CanAffect(
        MapCoordinates coordinates,
        CMUTopDownOrdnanceKind kind,
        out CMUTopDownOrdnanceBlockReason blockReason)
    {
        var entityCoordinates = _transform.ToCoordinates(coordinates);
        switch (kind)
        {
            case CMUTopDownOrdnanceKind.Mortar:
                if (_area.CanMortarFire(entityCoordinates))
                {
                    blockReason = CMUTopDownOrdnanceBlockReason.None;
                    return true;
                }

                blockReason = CMUTopDownOrdnanceBlockReason.AreaBlocked;
                return false;

            case CMUTopDownOrdnanceKind.OrbitalBombardment:
                if (_area.CanOrbitalBombard(entityCoordinates, out var roofed))
                {
                    blockReason = CMUTopDownOrdnanceBlockReason.None;
                    return true;
                }

                blockReason = roofed
                    ? CMUTopDownOrdnanceBlockReason.Roofed
                    : CMUTopDownOrdnanceBlockReason.AreaBlocked;
                return false;

            default:
                blockReason = CMUTopDownOrdnanceBlockReason.AreaBlocked;
                return false;
        }
    }

    private bool TryGetDepth(Entity<CMUZLevelsNetworkComponent> network, EntityUid map, out int depth)
    {
        if (!_zMapQuery.TryComp(map, out var zMap))
        {
            depth = default;
            return false;
        }

        depth = zMap.Depth;
        return true;
    }
}

public enum CMUTopDownOrdnanceKind
{
    Mortar,
    OrbitalBombardment,
}

public enum CMUTopDownOrdnanceBlockReason
{
    None,
    NoMap,
    NoSurface,
    AreaBlocked,
    Roofed,
}

public sealed class CMUTopDownOrdnanceResult
{
    public CMUTopDownOrdnanceResult(MapCoordinates selected)
    {
        Selected = selected;
    }

    public MapCoordinates Selected { get; }

    public bool UsesZLevels;

    public CMUTopDownOrdnanceBlockReason BlockReason;

    public List<CMUTopDownOrdnanceSurface> Surfaces { get; } = new();

    public CMUTopDownOrdnanceSurface? FirstImpact => Surfaces.Count > 0 ? Surfaces[0] : null;

    public CMUTopDownOrdnanceSurface? TerminalImpact => Surfaces.Count > 0 ? Surfaces[^1] : null;

    public bool Redirected => FirstImpact is { } impact &&
                              (impact.Coordinates.MapId != Selected.MapId ||
                               (impact.Coordinates.Position - Selected.Position).LengthSquared() > 0.001f);
}

public readonly record struct CMUTopDownOrdnanceSurface(MapCoordinates Coordinates, int Depth);
