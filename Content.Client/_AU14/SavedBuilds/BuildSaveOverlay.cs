// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.Client._AU14.SavedBuilds;

/// <summary>
/// World-space overlay for the saved-build selection: draws the live range box around the player,
/// any committed range boxes, and a translucent fill over every entity the server has confirmed is
/// saveable (resolved from the soft-whitelist). Reads its state from <see cref="BuildSaveModeSystem"/>.
/// </summary>
public sealed class BuildSaveOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private static readonly Color LiveBoxColor = new Color(0.3f, 0.8f, 1f, 0.9f);
    private static readonly Color CommittedBoxColor = new Color(0.3f, 1f, 0.4f, 0.9f);
    private static readonly Color HighlightColor = new Color(1f, 0.85f, 0.2f, 0.35f);
    private static readonly Color TileHighlightColor = new Color(0.25f, 1f, 0.35f, 0.45f);
    private const float LineThickness = 0.08f;

    private readonly BuildSaveModeSystem _mode;
    private readonly IPlayerManager _player;
    private readonly IMapManager _mapManager;
    private readonly SharedMapSystem _mapSystem;
    private readonly SharedTransformSystem _transform;
    private readonly IEntityManager _entMan;

    public BuildSaveOverlay(
        BuildSaveModeSystem mode,
        IPlayerManager player,
        IMapManager mapManager,
        SharedMapSystem mapSystem,
        SharedTransformSystem transform,
        IEntityManager entMan)
    {
        _mode = mode;
        _player = player;
        _mapManager = mapManager;
        _mapSystem = mapSystem;
        _transform = transform;
        _entMan = entMan;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!_mode.Active)
            return;

        var handle = args.WorldHandle;

        // Committed boxes.
        foreach (var box in _mode.CommittedBoxes)
        {
            var coords = _entMan.GetCoordinates(box.Center);
            if (!coords.IsValid(_entMan))
                continue;
            DrawBox(handle, _transform.ToMapCoordinates(coords), box.Radius, CommittedBoxColor);
        }

        // Live box around the player at the current radius.
        if (_player.LocalEntity is { } player && _entMan.EntityExists(player))
        {
            var playerMap = _transform.GetMapCoordinates(player);
            if (playerMap.MapId == args.MapId)
                DrawBox(handle, playerMap, _mode.Radius, LiveBoxColor);
        }

        // Everything below is per-highlight work, and a big selection (hundreds of entities, thousands of
        // tiles) is mostly off-screen at any moment. Cull to the viewport first: without this the overlay
        // paid a transform plus a draw call for every single highlight every frame, which is what made a
        // large range selection stutter. Enlarged slightly so a highlight straddling the edge still draws.
        var bounds = args.WorldAABB.Enlarged(1f);

        // Highlight resolved entities.
        foreach (var uid in _mode.Highlighted)
        {
            if (!_entMan.EntityExists(uid))
                continue;
            var map = _transform.GetMapCoordinates(uid);
            if (map.MapId != args.MapId || !bounds.Contains(map.Position))
                continue;
            var pos = map.Position;
            handle.DrawRect(new Box2(pos - new Vector2(0.5f, 0.5f), pos + new Vector2(0.5f, 0.5f)), HighlightColor);
        }

        // Highlight resolved tiles separately from entities. Grid lookups are memoised for the duration of
        // the draw because a tile selection is overwhelmingly made up of runs on the same few grids.
        EntityUid? cachedGridUid = null;
        MapGridComponent? cachedGrid = null;
        NetEntity cachedNet = default;

        foreach (var tile in _mode.HighlightedTiles)
        {
            if (cachedGrid == null || tile.Grid != cachedNet)
            {
                cachedNet = tile.Grid;
                cachedGridUid = null;
                cachedGrid = null;

                if (_entMan.TryGetEntity(tile.Grid, out var lookedUp) &&
                    _entMan.TryGetComponent<MapGridComponent>(lookedUp, out var gridComp))
                {
                    cachedGridUid = lookedUp;
                    cachedGrid = gridComp;
                }
            }

            if (cachedGridUid is not { } gridUid || cachedGrid is not { } grid)
                continue;

            var center = _mapSystem.GridTileToWorld(gridUid, grid, new Vector2i(tile.X, tile.Y));
            if (center.MapId != args.MapId || !bounds.Contains(center.Position))
                continue;

            var half = grid.TileSize * 0.5f;
            handle.DrawRect(new Box2(center.Position - new Vector2(half, half), center.Position + new Vector2(half, half)), TileHighlightColor);
        }
    }

    /// <summary>Draws a square outline (size 2*radius+1 tiles) centred on the tile under <paramref name="center"/>.</summary>
    private void DrawBox(DrawingHandleWorld handle, MapCoordinates center, int radius, Color color)
    {
        if (!_mapManager.TryFindGridAt(center, out var gridUid, out var grid))
        {
            // Off-grid: fall back to a plain world-space square around the point.
            var half = radius + 0.5f;
            DrawOutline(handle,
                center.Position + new Vector2(-half, -half),
                center.Position + new Vector2(half, -half),
                center.Position + new Vector2(half, half),
                center.Position + new Vector2(-half, half),
                color);
            return;
        }

        var centerTile = _mapSystem.CoordinatesToTile(gridUid, grid, center);
        var ts = grid.TileSize;
        var minLocal = new Vector2((centerTile.X - radius) * ts, (centerTile.Y - radius) * ts);
        var maxLocal = new Vector2((centerTile.X + radius + 1) * ts, (centerTile.Y + radius + 1) * ts);

        var p00 = _transform.ToMapCoordinates(new EntityCoordinates(gridUid, minLocal)).Position;
        var p10 = _transform.ToMapCoordinates(new EntityCoordinates(gridUid, new Vector2(maxLocal.X, minLocal.Y))).Position;
        var p11 = _transform.ToMapCoordinates(new EntityCoordinates(gridUid, maxLocal)).Position;
        var p01 = _transform.ToMapCoordinates(new EntityCoordinates(gridUid, new Vector2(minLocal.X, maxLocal.Y))).Position;
        DrawOutline(handle, p00, p10, p11, p01, color);
    }

    private static void DrawOutline(DrawingHandleWorld handle, Vector2 p00, Vector2 p10, Vector2 p11, Vector2 p01, Color color)
    {
        DrawSegment(handle, p00, p10, color);
        DrawSegment(handle, p10, p11, color);
        DrawSegment(handle, p11, p01, color);
        DrawSegment(handle, p01, p00, color);
    }

    private static void DrawSegment(DrawingHandleWorld handle, Vector2 from, Vector2 to, Color color)
    {
        var delta = to - from;
        var length = delta.Length();
        if (length <= 0f)
            return;

        var half = LineThickness * 0.5f;
        var mid = (from + to) * 0.5f;
        var angle = delta.ToWorldAngle();
        var rect = new Box2(-length / 2f, -half, length / 2f, half);
        handle.DrawRect(new Box2Rotated(rect.Translated(mid), angle, mid), color);
    }
}
