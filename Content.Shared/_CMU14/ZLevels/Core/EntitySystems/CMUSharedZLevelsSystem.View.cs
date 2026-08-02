using System.Collections.Generic;
using System.Numerics;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._RMC14.Xenonids.Construction.Nest;
using Content.Shared.Actions;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._CMU14.ZLevels.Core.EntitySystems;

public abstract partial class CMUSharedZLevelsSystem
{
    [Dependency] protected ITileDefinitionManager TilDefMan = default!;

    private readonly List<(Vector2 Center, float Distance)> _distanceOpeningCandidates = new();
    private readonly List<Entity<MapGridComponent>> _openingGridScratch = new();
    private readonly CMUZLevelOpeningCache _sharedOpeningCache = new();

    private void InitView()
    {
        SubscribeLocalEvent<CMUZLevelViewerComponent, MoveEvent>(OnViewerMove);
        SubscribeLocalEvent<CMUZLevelViewerComponent, CMUToggleZLevelLookUpAction>(OnToggleLookUp);
    }

    protected void InvalidateSharedOpeningCache(EntityUid gridUid)
    {
        _sharedOpeningCache.RemoveGrid(gridUid);
    }

    protected void InvalidateSharedOpeningCache(ref TileChangedEvent args)
    {
        _sharedOpeningCache.InvalidateTiles(args.Entity, args.Changes);
    }

    protected virtual void OnViewerMove(Entity<CMUZLevelViewerComponent> ent, ref MoveEvent args)
    {
        if (!ent.Comp.LookUp)
            return;

        if (!HasOpaqueAbove(ent))
            return;

        TryDisableLookUp(ent);
    }

    private void OnToggleLookUp(Entity<CMUZLevelViewerComponent> ent, ref CMUToggleZLevelLookUpAction args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        // AU14 (building overhaul): three-state cycle. Press 1: faint upper ghost (rooftop awareness).
        // Press 2: full look up (view + aim shift, original behaviour). Press 3: back to normal.
        if (ent.Comp.LookUp)
        {
            ent.Comp.LookUp = false;
            ent.Comp.FaintUp = false;
            DirtyField(ent, ent.Comp, nameof(CMUZLevelViewerComponent.LookUp));
            DirtyField(ent, ent.Comp, nameof(CMUZLevelViewerComponent.FaintUp));
            _popup.PopupClient(Loc.GetString("cmu-zlevel-look-up-disabled"), ent, ent, PopupType.SmallCaution);
            return;
        }

        if (HasComp<XenoNestedComponent>(ent))
        {
            _popup.PopupClient(Loc.GetString("cmu-zlevel-look-up-nested"), ent, ent, PopupType.SmallCaution);
            return;
        }

        if (!ent.Comp.FaintUp)
        {
            // Normal -> faint. No opaque-above gate here: the renderer re-checks the ceiling every frame
            // and simply draws nothing while one is overhead, so the mode can stay latched while moving.
            ent.Comp.FaintUp = true;
            DirtyField(ent, ent.Comp, nameof(CMUZLevelViewerComponent.FaintUp));
            _popup.PopupClient(Loc.GetString("cmu-zlevel-faint-up-enabled"), ent, ent, PopupType.SmallCaution);
            return;
        }

        // Faint -> full look up (original gates apply; on failure we stay in faint mode).
        if (HasOpaqueAbove(ent))
        {
            _popup.PopupClient(Loc.GetString("cmu-zlevel-look-up-fail"), ent, ent, PopupType.SmallCaution);
            return;
        }

        ent.Comp.LookUp = true;
        DirtyField(ent, ent.Comp, nameof(CMUZLevelViewerComponent.LookUp));

        var ev = new CMUZLevelLookUpEnabledEvent();
        RaiseLocalEvent(ent, ev);

        _popup.PopupClient(Loc.GetString("cmu-zlevel-look-up-enabled"), ent, ent, PopupType.SmallCaution);
    }

    public bool TryDisableLookUp(EntityUid uid)
    {
        if (!TryComp<CMUZLevelViewerComponent>(uid, out var viewer) ||
            !viewer.LookUp)
        {
            return false;
        }

        viewer.LookUp = false;
        DirtyField(uid, viewer, nameof(CMUZLevelViewerComponent.LookUp));
        return true;
    }

    public Entity<CMUZLevelViewerComponent> EnsureZLevelViewer(EntityUid uid)
    {
        return (uid, EnsureComp<CMUZLevelViewerComponent>(uid));
    }

    public bool HasOpaqueAbove(EntityUid ent, Entity<CMUZLevelMapComponent?>? currentMapUid = null)
    {
        currentMapUid ??= Transform(ent).MapUid;

        if (currentMapUid is null)
            return false;

        return HasOpaqueAbove(currentMapUid.Value, _transform.GetWorldPosition(ent));
    }

    public bool HasOpaqueAbove(Entity<CMUZLevelMapComponent?> currentMapUid, Vector2 worldPosition)
    {
        if (!TryMapUp(currentMapUid, out var mapAboveUid))
            return false;

        if (!TryGetMapCoordinates(mapAboveUid.Value, worldPosition, out var aboveMapCoordinates) ||
            !_map.TryFindGridAt(aboveMapCoordinates, out var gridUid, out var mapAboveGrid))
            return false;

        return !CMUZLevelOpeningCache.IsOpeningTile(gridUid, mapAboveGrid, worldPosition, _map, TilDefMan);
    }

    public bool HasZLevelEye(CMUZLevelViewerComponent viewer, EntityUid targetMap)
    {
        foreach (var eye in viewer.Eyes)
        {
            if (_xformQuery.TryComp(eye, out var eyeXform) &&
                eyeXform.MapUid == targetMap)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryFindOpeningNear(EntityUid map, Vector2 position, float radius, out Vector2 openingPosition)
    {
        openingPosition = default;

        if (!_gridQuery.TryComp(map, out var grid))
        {
            openingPosition = position;
            return true;
        }

        if (!_mapQuery.TryComp(map, out var mapComp))
            return false;

        return _sharedOpeningCache.TryFindNearestOpeningCenterNear(
            mapComp.MapId,
            position,
            radius,
            out openingPosition,
            _openingGridScratch,
            _mapManager,
            _map,
            _transform,
            TilDefMan,
            edgeOnly: false);
    }

    /// <summary>
    /// Gets the shortest distance between positions on adjacent z-levels by routing through an opening.
    /// </summary>
    public bool TryGetDistanceViaAdjacentLevelOpening(
        EntityUid firstMap,
        Vector2 firstPosition,
        EntityUid secondMap,
        Vector2 secondPosition,
        float searchRadius,
        out float distance)
    {
        distance = 0f;

        if (!float.IsFinite(searchRadius) ||
            searchRadius < 0f ||
            !_zMapQuery.TryComp(firstMap, out var firstZMap) ||
            !_zMapQuery.TryComp(secondMap, out var secondZMap) ||
            !firstZMap.NetworkUid.IsValid() ||
            firstZMap.NetworkUid != secondZMap.NetworkUid ||
            Math.Abs(firstZMap.Depth - secondZMap.Depth) != 1)
        {
            return false;
        }

        var openingMap = firstZMap.Depth > secondZMap.Depth ? firstMap : secondMap;
        if (!_mapQuery.TryComp(openingMap, out var openingMapComp))
            return false;

        _distanceOpeningCandidates.Clear();
        _sharedOpeningCache.FindOpeningCentersNear(
            openingMapComp.MapId,
            firstPosition,
            searchRadius,
            _distanceOpeningCandidates,
            _openingGridScratch,
            _mapManager,
            _map,
            _transform,
            TilDefMan,
            edgeOnly: false);

        var shortestDistance = float.PositiveInfinity;
        foreach (var (opening, firstDistance) in _distanceOpeningCandidates)
        {
            var routeDistance = firstDistance + Vector2.Distance(opening, secondPosition);
            shortestDistance = MathF.Min(shortestDistance, routeDistance);
        }

        if (float.IsPositiveInfinity(shortestDistance))
            return false;

        distance = shortestDistance;
        return true;
    }

    public bool TryFindZShotOpening(
        EntityUid sourceMap,
        EntityUid targetMap,
        int offset,
        Vector2 from,
        Vector2 to,
        out Vector2 opening,
        bool preferOpeningAwayFromSource = false,
        float maxSourceDistanceFromOpeningEdgeTiles = float.PositiveInfinity)
    {
        opening = default;
        if (offset == 0)
            return false;

        if (TryFindSourceZStairOpening(sourceMap, offset, from, to, out opening))
            return true;

        var openingMap = offset < 0 ? sourceMap : targetMap;
        var openingGridUid = openingMap;
        if (!_gridQuery.TryComp(openingGridUid, out var grid))
        {
            if (!_mapQuery.TryComp(openingMap, out var mapComp) ||
                !_mapManager.TryFindGridAt(mapComp.MapId, from, out openingGridUid, out grid))
            {
                return false;
            }
        }

        if (grid == null)
            return false;

        var sourceTile = preferOpeningAwayFromSource
            ? _map.WorldToTile(openingGridUid, grid, from)
            : default;
        var fallbackOpening = Vector2.Zero;
        var hasFallbackOpening = false;
        var maxSourceDistanceFromOpeningCenter = float.IsPositiveInfinity(maxSourceDistanceFromOpeningEdgeTiles)
            ? float.PositiveInfinity
            : grid.TileSize * (0.5f + Math.Max(0f, maxSourceDistanceFromOpeningEdgeTiles));
        var maxSourceDistanceSquared = maxSourceDistanceFromOpeningCenter * maxSourceDistanceFromOpeningCenter;
        var selectedOpening = Vector2.Zero;

        bool TryUseOpeningTile(Vector2i tile)
        {
            Vector2 openingCenter;
            if (_map.TryGetTileRef(openingGridUid, grid, tile, out var tileRef) &&
                CMUZLevelOpeningCache.IsOpeningTile(tileRef.Tile, TilDefMan))
            {
                openingCenter = _map.ToCenterCoordinates(openingGridUid, tile, grid).Position;
            }
            else if (!TryFindZStairOpening(openingMap, sourceMap, openingGridUid, grid, tile, offset, out openingCenter))
            {
                return false;
            }

            if (Vector2.DistanceSquared(from, openingCenter) > maxSourceDistanceSquared)
                return false;

            if (preferOpeningAwayFromSource &&
                tile == sourceTile)
            {
                if (!hasFallbackOpening)
                {
                    fallbackOpening = openingCenter;
                    hasFallbackOpening = true;
                }

                return false;
            }

            selectedOpening = openingCenter;
            return true;
        }

        var localFrom = _map.WorldToLocal(openingGridUid, grid, from) / grid.TileSize;
        var localTo = _map.WorldToLocal(openingGridUid, grid, to) / grid.TileSize;
        var localDelta = localTo - localFrom;
        var currentTile = new Vector2i((int) MathF.Floor(localFrom.X), (int) MathF.Floor(localFrom.Y));
        var endTile = new Vector2i((int) MathF.Floor(localTo.X), (int) MathF.Floor(localTo.Y));

        var stepX = Math.Sign(localDelta.X);
        var stepY = Math.Sign(localDelta.Y);
        var tDeltaX = stepX == 0 ? float.PositiveInfinity : MathF.Abs(1f / localDelta.X);
        var tDeltaY = stepY == 0 ? float.PositiveInfinity : MathF.Abs(1f / localDelta.Y);
        var nextBoundaryX = stepX > 0 ? currentTile.X + 1f : currentTile.X;
        var nextBoundaryY = stepY > 0 ? currentTile.Y + 1f : currentTile.Y;
        var tMaxX = stepX == 0 ? float.PositiveInfinity : (nextBoundaryX - localFrom.X) / localDelta.X;
        var tMaxY = stepY == 0 ? float.PositiveInfinity : (nextBoundaryY - localFrom.Y) / localDelta.Y;

        while (true)
        {
            if (TryUseOpeningTile(currentTile))
            {
                opening = selectedOpening;
                return true;
            }

            if (currentTile == endTile)
                break;

            if (tMaxX < tMaxY)
            {
                currentTile += new Vector2i(stepX, 0);
                tMaxX += tDeltaX;
            }
            else if (tMaxY < tMaxX)
            {
                currentTile += new Vector2i(0, stepY);
                tMaxY += tDeltaY;
            }
            else
            {
                currentTile += new Vector2i(stepX, stepY);
                tMaxX += tDeltaX;
                tMaxY += tDeltaY;
            }
        }

        if (hasFallbackOpening)
        {
            opening = fallbackOpening;
            return true;
        }

        return false;
    }

    private bool TryFindSourceZStairOpening(
        EntityUid sourceMap,
        int offset,
        Vector2 from,
        Vector2 to,
        out Vector2 opening)
    {
        opening = default;

        var delta = to - from;
        var lengthSquared = delta.LengthSquared();
        if (lengthSquared <= 0.001f)
            return false;

        var query = EntityQueryEnumerator<CMUZLevelStairsComponent, TransformComponent>();
        while (query.MoveNext(out var stairUid, out var stairs, out var xform))
        {
            if (xform.MapUid != sourceMap ||
                stairs.Offset != offset)
            {
                continue;
            }

            var requiredDelta = RequiredZStairDelta(stairs);
            var candidate = _transform.GetWorldPosition(stairUid) + new Vector2(requiredDelta.X, requiredDelta.Y);
            var progress = Vector2.Dot(candidate - from, delta) / lengthSquared;
            if (progress is < 0f or > 1f)
                continue;

            var closest = from + delta * progress;
            if (Vector2.DistanceSquared(candidate, closest) > 0.25f)
                continue;

            opening = candidate;
            return true;
        }

        return false;
    }

    private bool TryFindZStairOpening(
        EntityUid openingMap,
        EntityUid sourceMap,
        EntityUid openingGridUid,
        MapGridComponent grid,
        Vector2i tile,
        int offset,
        out Vector2 openingCenter)
    {
        openingCenter = default;

        var query = EntityQueryEnumerator<CMUZLevelStairsComponent, TransformComponent>();
        while (query.MoveNext(out var stairUid, out var stairs, out var xform))
        {
            var onOpeningMap = xform.MapUid == openingMap;
            var onSourceMap = xform.MapUid == sourceMap;
            if (!onOpeningMap && !onSourceMap)
                continue;

            var stairTile = _map.WorldToTile(openingGridUid, grid, _transform.GetWorldPosition(stairUid));
            if (onOpeningMap && stairs.Offset == -offset && stairTile == tile ||
                onSourceMap && stairs.Offset == offset && stairTile + RequiredZStairDelta(stairs) == tile)
            {
                openingCenter = _map.ToCenterCoordinates(openingGridUid, tile, grid).Position;
                return true;
            }
        }

        return false;
    }

    private static Vector2i RequiredZStairDelta(CMUZLevelStairsComponent stairs)
    {
        var delta = DirectionDelta(stairs.Direction);
        return stairs.Offset >= 0 ? delta : -delta;
    }

    private static Vector2i DirectionDelta(Direction direction)
    {
        return direction switch
        {
            Direction.North => new Vector2i(0, 1),
            Direction.South => new Vector2i(0, -1),
            Direction.East => new Vector2i(1, 0),
            Direction.West => new Vector2i(-1, 0),
            Direction.NorthEast => new Vector2i(1, 1),
            Direction.NorthWest => new Vector2i(-1, 1),
            Direction.SouthEast => new Vector2i(1, -1),
            Direction.SouthWest => new Vector2i(-1, -1),
            _ => Vector2i.Zero,
        };
    }
}

public sealed partial class CMUToggleZLevelLookUpAction : InstantActionEvent
{
}

public record struct CMUZLevelLookUpEnabledEvent;
