using System.Numerics;
using Content.Client.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Robust.Client.Graphics;
using Robust.Shared.Map;

namespace Content.Client.Atmos.Overlays;

public sealed partial class GasTileHeatBlurOverlay
{
    private readonly List<CMUHeatGrid> _cmuHeatGrids = new();
    private readonly List<CMUHeatTile> _cmuHeatTiles = new();
    private Action? _cmuDrawHeatMask;
    private DrawingHandleWorld? _cmuHeatWorldHandle;
    private Vector2i _cmuHeatTargetSize;

    /// <summary>
    /// Prepares this viewport's heat geometry before allocating or clearing a render target.
    /// The prepared lists belong to this synchronous draw call and are rebuilt for every camera.
    /// </summary>
    internal bool CMUBeforeDrawHeat(
        IClydeViewport viewport,
        MapId mapId,
        Box2 worldAabb,
        Box2Rotated worldBounds,
        DrawingHandleWorld worldHandle)
    {
        _cmuHeatGrids.Clear();
        _cmuHeatTiles.Clear();
        _intersectingGrids.Clear();
        if (mapId == MapId.Nullspace)
            return false;

        var overlayQuery = _entManager.GetEntityQuery<GasTileOverlayComponent>();
        var worldToViewportLocal = viewport.GetWorldToLocalMatrix();
        _maps.FindGridsIntersecting(mapId, worldAabb, ref _intersectingGrids);
        foreach (var grid in _intersectingGrids)
        {
            if (!overlayQuery.TryGetComponent(grid.Owner, out var comp))
                continue;

            var gridEntToWorld = _xformSys.GetWorldMatrix(grid.Owner);
            var gridEntToViewportLocal = gridEntToWorld * worldToViewportLocal;
            if (!Matrix3x2.Invert(gridEntToViewportLocal, out var viewportLocalToGridEnt))
                continue;

            var firstTile = _cmuHeatTiles.Count;
            var worldToGridLocal = _xformSys.GetInvWorldMatrix(grid.Owner);
            var floatBounds = worldToGridLocal.TransformBox(worldBounds).Enlarged(grid.Comp.TileSize);
            var localBounds = new Box2i(
                (int) MathF.Floor(floatBounds.Left),
                (int) MathF.Floor(floatBounds.Bottom),
                (int) MathF.Ceiling(floatBounds.Right),
                (int) MathF.Ceiling(floatBounds.Top));

            foreach (var chunk in comp.Chunks.Values)
            {
                var enumerator = new GasChunkEnumerator(chunk);
                while (enumerator.MoveNext(out var tileGas))
                {
                    var tilePosition = chunk.Origin + (enumerator.X, enumerator.Y);
                    if (!localBounds.Contains(tilePosition))
                        continue;

                    var strength = GetHeatDistortionStrength(tileGas.ByteGasTemperature);
                    if (strength <= 0f)
                        continue;

                    _cmuHeatTiles.Add(new CMUHeatTile(
                        Box2.CenteredAround(tilePosition + grid.Comp.TileSizeHalfVector,
                            grid.Comp.TileSizeVector * ShaderSpilling),
                        new Color(strength, 0f, 0f)));
                }
            }

            // Keep cold grids too: the original shader uses the last invertible gas grid's
            // matrix, even when that grid contributes no hot tiles to the mask.
            _cmuHeatGrids.Add(new CMUHeatGrid(gridEntToViewportLocal, viewportLocalToGridEnt,
                firstTile, _cmuHeatTiles.Count));
        }

        // BeforeDraw=false also prevents Clyde's subsequent SCREEN_TEXTURE copy.
        // Do not create a cache entry, change drawing state, or clear a target on this path.
        if (_cmuHeatTiles.Count == 0)
            return false;

        var res = _resources.GetForViewport(viewport, static _ => new CachedResources());
        var target = viewport.RenderTarget;
        if (res.HeatTarget?.Texture.Size != target.Size)
        {
            res.HeatTarget?.Dispose();
            res.HeatTarget = _clyde.CreateRenderTarget(
                target.Size,
                new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
                name: nameof(GasTileHeatBlurOverlaySystem));
        }

        worldHandle.UseShader(_proto.Index(UnshadedShader).Instance());
        _cmuHeatWorldHandle = worldHandle;
        _cmuHeatTargetSize = res.HeatTarget.Size;
        try
        {
            worldHandle.RenderInRenderTarget(res.HeatTarget,
                _cmuDrawHeatMask ??= CMUDrawHeatMask, new Color(0, 0, 0, 0));
        }
        finally
        {
            _cmuHeatWorldHandle = null;
        }

        return true;
    }

    private void CMUDrawHeatMask()
    {
        var worldHandle = _cmuHeatWorldHandle!;
        var uvToUi = Matrix3Helpers.CreateScale(_cmuHeatTargetSize.X, -_cmuHeatTargetSize.Y);
        foreach (var grid in _cmuHeatGrids)
        {
            // Anchor distortion to grid coordinates so camera movement does not make it shimmer.
            var uvToGridEnt = uvToUi * grid.ViewportLocalToGrid;
            _shader.SetParameter("grid_ent_from_viewport_local", uvToGridEnt);
            worldHandle.SetTransform(grid.GridToViewportLocal);
            for (var index = grid.FirstTile; index < grid.EndTile; index++)
            {
                var tile = _cmuHeatTiles[index];
                worldHandle.DrawTextureRect(_heatGradientTexture, tile.Bounds, tile.Modulate);
            }
        }
    }

    private readonly record struct CMUHeatGrid(
        Matrix3x2 GridToViewportLocal,
        Matrix3x2 ViewportLocalToGrid,
        int FirstTile,
        int EndTile);

    private readonly record struct CMUHeatTile(Box2 Bounds, Color Modulate);
}
