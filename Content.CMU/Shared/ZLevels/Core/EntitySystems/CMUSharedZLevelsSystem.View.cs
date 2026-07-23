using System.Collections.Generic;
using System.Numerics;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared.Actions;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._CMU14.ZLevels.Core.EntitySystems;

public abstract partial class CMUSharedZLevelsSystem
{
    [Dependency] protected ITileDefinitionManager TilDefMan = default!;

    private readonly CMUZLevelOpeningCache _sharedOpeningCache = new();
    private readonly List<Entity<MapGridComponent>> _openingGridScratch = new();

    public CMUZLevelOpeningCache OpeningCache => _sharedOpeningCache;

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

    protected void ClearSharedOpeningCache()
    {
        _sharedOpeningCache.Clear();
    }

    protected virtual void OnViewerMove(Entity<CMUZLevelViewerComponent> ent, ref MoveEvent args)
    {
        if (!ent.Comp.LookUp)
            return;

        if (CanLookUp(ent))
            return;

        TryDisableLookUp(ent);
    }

    private void OnToggleLookUp(Entity<CMUZLevelViewerComponent> ent, ref CMUToggleZLevelLookUpAction args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!_configuration.GetCVar(CMUZLevelsCVars.Enabled))
        {
            TryDisableLookUp(ent);
            return;
        }

        if (!CanToggleLookUp(ent.Comp.LookUp, CanLookUp(ent)))
        {
            _popup.PopupClient(Loc.GetString("cmu-zlevel-look-up-fail"), ent, ent, PopupType.SmallCaution);
            return;
        }

        var enabled = !ent.Comp.LookUp;
        SetLookUp(ent, enabled);

        _popup.PopupClient(Loc.GetString(enabled
            ? "cmu-zlevel-look-up-enabled"
            : "cmu-zlevel-look-up-disabled"), ent, ent, PopupType.SmallCaution);
    }

    public bool SetLookUp(EntityUid uid, bool enabled)
    {
        if (!TryComp<CMUZLevelViewerComponent>(uid, out var viewer))
            return false;

        return SetLookUp((uid, viewer), enabled);
    }

    private bool SetLookUp(Entity<CMUZLevelViewerComponent> ent, bool enabled)
    {
        if (enabled &&
            (!_configuration.GetCVar(CMUZLevelsCVars.Enabled) ||
             !CanLookUp(ent)))
        {
            return false;
        }

        if (ent.Comp.LookUp == enabled)
            return false;

        ent.Comp.LookUp = enabled;
        DirtyField(ent, ent.Comp, nameof(CMUZLevelViewerComponent.LookUp));

        if (enabled)
        {
            var ev = new CMUZLevelLookUpEnabledEvent();
            RaiseLocalEvent(ent, ev);
        }

        return true;
    }

    public bool TryDisableLookUp(EntityUid uid)
    {
        return SetLookUp(uid, false);
    }

    public bool CanLookUp(EntityUid ent)
    {
        var mapUid = Transform(ent).MapUid;
        var hasUpperMap = mapUid is { } map &&
                          TryMapUp(map, out _);

        return CanEnableLookUp(hasUpperMap, HasOpaqueAbove(ent));
    }

    internal static bool CanEnableLookUp(bool hasUpperMap, bool opaqueAbove)
    {
        return hasUpperMap && !opaqueAbove;
    }

    internal static bool CanToggleLookUp(bool currentlyLookingUp, bool canEnable)
    {
        return currentlyLookingUp || canEnable;
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
            if (XformQuery.TryComp(eye, out var eyeXform) &&
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
            _map,
            _transform,
            TilDefMan,
            edgeOnly: false);
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

        var openingMap = offset < 0 ? sourceMap : targetMap;
        if (!_gridQuery.TryComp(openingMap, out var grid))
            return false;

        var sourceTile = preferOpeningAwayFromSource
            ? _map.WorldToTile(openingMap, grid, from)
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
            if (_map.TryGetTileRef(openingMap, grid, tile, out var tileRef) &&
                !CMUZLevelOpeningCache.IsOpeningTile(tileRef.Tile, TilDefMan))
            {
                return false;
            }

            var openingCenter = _map.ToCenterCoordinates(openingMap, tile, grid).Position;
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

        var localFrom = _map.WorldToLocal(openingMap, grid, from) / grid.TileSize;
        var localTo = _map.WorldToLocal(openingMap, grid, to) / grid.TileSize;
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
}

public sealed partial class CMUToggleZLevelLookUpAction : InstantActionEvent
{
}

public record struct CMUZLevelLookUpEnabledEvent;
