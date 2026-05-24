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
    private const float ZShotOpeningStep = 0.25f;

    [Dependency] protected ITileDefinitionManager TilDefMan = default!;
    private void InitView()
    {
        SubscribeLocalEvent<CMUZLevelViewerComponent, MoveEvent>(OnViewerMove);
        SubscribeLocalEvent<CMUZLevelViewerComponent, CMUToggleZLevelLookUpAction>(OnToggleLookUp);
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

        if (HasOpaqueAbove(ent))
        {
            _popup.PopupClient(Loc.GetString("cmu-zlevel-look-up-fail"), ent, ent, PopupType.SmallCaution);
            return;
        }

        ent.Comp.LookUp = !ent.Comp.LookUp;
        DirtyField(ent, ent.Comp, nameof(CMUZLevelViewerComponent.LookUp));

        if (ent.Comp.LookUp)
        {
            var ev = new CMUZLevelLookUpEnabledEvent();
            RaiseLocalEvent(ent, ev);
        }

        _popup.PopupClient(Loc.GetString(ent.Comp.LookUp
            ? "cmu-zlevel-look-up-enabled"
            : "cmu-zlevel-look-up-disabled"), ent, ent, PopupType.SmallCaution);
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

    public bool HasOpaqueAbove(EntityUid ent, Entity<CMUZLevelMapComponent?>? currentMapUid = null)
    {
        currentMapUid ??= Transform(ent).MapUid;

        if (currentMapUid is null)
            return false;

        if (!TryMapUp(currentMapUid.Value, out var mapAboveUid))
            return false;

        if (!_gridQuery.TryComp(mapAboveUid.Value, out var mapAboveGrid))
            return false;

        return !CMUZLevelOpeningCache.IsOpeningTile(mapAboveUid.Value, mapAboveGrid, _transform.GetWorldPosition(ent), _map, TilDefMan);
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

        var center = _map.WorldToTile(map, grid, position);
        var tileRadius = Math.Max(0, (int) MathF.Ceiling(radius / grid.TileSize));
        var bestDistanceSquared = radius * radius;
        var found = false;
        var gridEnt = new Entity<MapGridComponent>(map, grid);

        for (var x = -tileRadius; x <= tileRadius; x++)
        {
            for (var y = -tileRadius; y <= tileRadius; y++)
            {
                var tile = center + new Vector2i(x, y);
                if (!CMUZLevelOpeningCache.IsOpeningTile(gridEnt, tile, _map, TilDefMan))
                    continue;

                var candidate = _map.ToCenterCoordinates(map, tile, grid).Position;
                var distanceSquared = Vector2.DistanceSquared(position, candidate);
                if (distanceSquared > bestDistanceSquared)
                    continue;

                bestDistanceSquared = distanceSquared;
                openingPosition = candidate;
                found = true;
            }
        }

        return found;
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

        var delta = to - from;
        var distance = delta.Length();
        var steps = Math.Max(1, (int) MathF.Ceiling(distance / ZShotOpeningStep));
        var maxSourceDistance = maxSourceDistanceFromOpeningEdgeTiles * grid.TileSize;
        var bestSourceDistance = preferOpeningAwayFromSource ? float.MinValue : float.MaxValue;
        var found = false;

        for (var i = 0; i <= steps; i++)
        {
            var position = from + delta * (i / (float) steps);
            if (!IsZShotOpening(openingMap, grid, position))
                continue;

            var sourceDistance = Vector2.Distance(from, position);
            if (sourceDistance > maxSourceDistance)
                continue;

            if (!preferOpeningAwayFromSource)
            {
                opening = position;
                return true;
            }

            if (sourceDistance <= bestSourceDistance)
            {
                continue;
            }

            bestSourceDistance = sourceDistance;
            opening = position;
            found = true;
        }

        return found;
    }

    private bool IsZShotOpening(EntityUid mapUid, MapGridComponent grid, Vector2 position)
    {
        return CMUZLevelOpeningCache.IsOpeningTile(mapUid, grid, position, _map, TilDefMan);
    }
}

public sealed partial class CMUToggleZLevelLookUpAction : InstantActionEvent
{
}

public record struct CMUZLevelLookUpEnabledEvent;
