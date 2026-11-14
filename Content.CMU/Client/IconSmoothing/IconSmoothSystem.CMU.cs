// ReSharper disable CheckNamespace
namespace Content.Client.IconSmoothing;

public sealed partial class IconSmoothSystem
{
    // the cardinal junction this entity's own smoothing sees (north 1, south 2, east 4, west 8),
    // so overlays like weeds can line up with a window without copying its keys
    public int CMUGetCardinalJunction(EntityUid uid)
    {
        if (!_iconSmoothQuery.TryComp(uid, out var smooth) ||
            !TryComp(uid, out TransformComponent? xform) ||
            !xform.Anchored ||
            !TryComp(xform.GridUid, out Robust.Shared.Map.Components.MapGridComponent? grid))
        {
            return 0;
        }

        var gridUid = xform.GridUid.Value;
        var pos = _mapSystem.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var dirs = CardinalConnectDirs.None;
        if (MatchingEntity(uid, smooth, _mapSystem.GetAnchoredEntities(gridUid, grid, pos.Offset(Direction.North))))
            dirs |= CardinalConnectDirs.North;
        if (MatchingEntity(uid, smooth, _mapSystem.GetAnchoredEntities(gridUid, grid, pos.Offset(Direction.South))))
            dirs |= CardinalConnectDirs.South;
        if (MatchingEntity(uid, smooth, _mapSystem.GetAnchoredEntities(gridUid, grid, pos.Offset(Direction.East))))
            dirs |= CardinalConnectDirs.East;
        if (MatchingEntity(uid, smooth, _mapSystem.GetAnchoredEntities(gridUid, grid, pos.Offset(Direction.West))))
            dirs |= CardinalConnectDirs.West;

        return (int) dirs;
    }
}
