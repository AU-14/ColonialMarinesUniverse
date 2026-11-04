using System.Numerics;
using Content.Shared._RMC14.Teleporter;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client._RMC14.Teleporter;

public sealed partial class RMCTeleporterViewerOverlay
{
    private const int TilePixels = EyeManager.PixelsPerMeter;

    private readonly SharedMapSystem _map;
    private readonly HashSet<Vector2i> _projectedTileIndices = new();

    [Dependency] private IResourceCache _resources = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;

    private bool PrepareProjection(
        DrawingHandleWorld handle,
        RMCTeleporterViewerComponent viewer,
        EntityUid remoteViewer,
        Box2 remoteBounds,
        Vector2 positionDifference,
        out EntityUid gridUid,
        out MapGridComponent grid)
    {
        _projectedTileIndices.Clear();
        gridUid = default;
        grid = default!;

        if (_transform.GetGrid(remoteViewer) is not { } remoteGridUid ||
            !_entity.TryGetComponent(remoteGridUid, out MapGridComponent? remoteGrid))
        {
            return false;
        }

        gridUid = remoteGridUid;
        grid = remoteGrid;
        var foundTile = false;
        var minX = int.MaxValue;
        var minY = int.MaxValue;
        foreach (var tileRef in _map.GetTilesIntersecting(gridUid, grid, remoteBounds, ignoreEmpty: false))
        {
            foundTile = true;
            minX = Math.Min(minX, tileRef.X);
            minY = Math.Min(minY, tileRef.Y);
        }

        if (!foundTile)
            return false;

        var gridMatrix = _transform.GetWorldMatrix(gridUid);
        foreach (var tileRef in _map.GetTilesIntersecting(gridUid, grid, remoteBounds, ignoreEmpty: false))
        {
            if (!IsProjectionTile(viewer.ProjectionTileMask, tileRef.X - minX, tileRef.Y - minY))
                continue;

            _projectedTileIndices.Add(tileRef.GridIndices);
            if (!viewer.ProjectTiles)
                continue;

            if (!_tiles.TryGetDefinition(tileRef.Tile.TypeId, out var definition) ||
                definition.Sprite is not { } spritePath)
            {
                continue;
            }

            var texture = _resources.GetResource<TextureResource>(spritePath).Texture;
            var variant = Math.Min(tileRef.Tile.Variant, (byte) Math.Max(0, definition.Variants - 1));
            var sourceRegion = new UIBox2(
                variant * TilePixels,
                0,
                (variant + 1) * TilePixels,
                TilePixels);
            var tileSize = grid.TileSize;
            var localBounds = Box2.FromDimensions(
                new Vector2(tileRef.X * tileSize, tileRef.Y * tileSize),
                new Vector2(tileSize, tileSize));
            var projectedBounds = gridMatrix.TransformBox(localBounds).Translated(-positionDifference);
            var rotationMirroring = definition.AllowRotationMirror ? tileRef.Tile.RotationMirroring : 0;

            if (rotationMirroring == 0)
            {
                handle.DrawTextureRectRegion(texture, projectedBounds, subRegion: sourceRegion);
                continue;
            }

            var center = projectedBounds.Center;
            var transform = Matrix3x2.CreateTranslation(-center);
            if (rotationMirroring >= 4)
                transform *= Matrix3x2.CreateScale(-1, 1);
            transform *= Matrix3x2.CreateRotation(rotationMirroring % 4 * MathF.PI / 2);
            transform *= Matrix3x2.CreateTranslation(center);

            handle.SetTransform(transform);
            handle.DrawTextureRectRegion(texture, projectedBounds, subRegion: sourceRegion);
            handle.SetTransform(Matrix3x2.Identity);
        }

        return true;
    }

    private bool IsProjectedPosition(
        RMCTeleporterViewerComponent viewer,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2 worldPosition)
    {
        if (viewer.ProjectionTileMask.Length == 0)
            return true;

        return _map.TryGetTileRef(gridUid, grid, worldPosition, out var tileRef) &&
               _projectedTileIndices.Contains(tileRef.GridIndices);
    }

    private static bool IsProjectionTile(string mask, int x, int y)
    {
        if (mask.Length == 0)
            return true;

        var width = mask.IndexOf('/');
        if (width < 0)
            width = mask.Length;

        var index = y * (width + 1) + x;
        return x >= 0 &&
               x < width &&
               y >= 0 &&
               index >= 0 &&
               index < mask.Length &&
               mask[index] == '#';
    }
}
