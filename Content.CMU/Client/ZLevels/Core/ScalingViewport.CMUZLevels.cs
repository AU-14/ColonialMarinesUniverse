using System.Numerics;
using System.Collections.Generic;
using Content.Client.Examine;
using Content.Shared._CMU14.ZLevels;
using Content.Client._CMU14.ZLevels.Core;
using Content.Shared._CMU14.ZLevels.Core;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Shared.Containers;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Profiling;
using Robust.Shared.Prototypes;

namespace Content.Client.Viewport;

public sealed partial class ScalingViewport
{
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private ITileDefinitionManager _tile = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private ProfManager _prof = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly ProtoId<ShaderPrototype> StencilClearShader = "StencilClear";
    private static readonly ProtoId<ShaderPrototype> StencilMaskShader = "StencilMask";
    private static readonly ProtoId<ShaderPrototype> StencilEqualDrawShader = "StencilEqualDraw";

    private CMUClientZLevelsSystem? _zLevels;
    private SharedMapSystem? _mapSystem;
    private SharedTransformSystem? _transform;
    private EntityLookupSystem? _lookup;
    private ExamineSystem? _examine;
    private SharedContainerSystem? _containers;

    private EntityQuery<TransformComponent>? _xformQuery;

    private List<Entity<MapGridComponent>> _zLevelGrids = new();
    private List<Entity<MapGridComponent>> _stairPreviewGrids = new();
    private readonly List<Vector2> _stairPreviewOrigins = new(CMUZLevelViewerComponent.MaxStairPreviewPositions);
    private readonly List<Box2> _zOpeningBounds = new();
    private readonly ZEye _zEye = new();
    private readonly ZEye _stairPreviewEye = new();
    private IClydeViewport? _stairPreviewViewport;
    private bool _drawStairPreviewComposite;

    /// <summary>
    /// We are looking for at least one empty tile on the screen.
    /// This is used to ensure that it makes sense to draw the z-planes and that they are visible.
    /// </summary>
    public bool TryFindEmptyTiles(EntityUid mapUid, IClydeViewport viewport)
    {
        return TryFindEmptyTiles(mapUid, viewport, null, out _);
    }

    private bool TryFindEmptyTiles(
        EntityUid mapUid,
        IClydeViewport viewport,
        List<Box2>? openingBounds,
        out Box2 combinedOpeningBounds,
        int maxOpeningBounds = int.MaxValue,
        bool exactOpeningBounds = false,
        Vector2 viewportToMapOffset = default)
    {
        combinedOpeningBounds = default;

        if (_xformQuery is null || !_xformQuery.Value.TryComp(mapUid, out var xform))
            return true;

        var mapId = xform.MapID;

        if (_mapSystem is null || _transform is null)
            return true;

        _zLevels ??= _entityManager.System<CMUClientZLevelsSystem>();
        var openingCache = _zLevels.OpeningCache;

        var c0 = viewport.LocalToWorld(Vector2.Zero).Position;
        var c1 = viewport.LocalToWorld(new Vector2(viewport.Size.X, 0)).Position;
        var c2 = viewport.LocalToWorld(new Vector2(0, viewport.Size.Y)).Position;
        var c3 = viewport.LocalToWorld(viewport.Size).Position;

        var minX = MathF.Min(MathF.Min(c0.X, c1.X), MathF.Min(c2.X, c3.X));
        var minY = MathF.Min(MathF.Min(c0.Y, c1.Y), MathF.Min(c2.Y, c3.Y));
        var maxX = MathF.Max(MathF.Max(c0.X, c1.X), MathF.Max(c2.X, c3.X));
        var maxY = MathF.Max(MathF.Max(c0.Y, c1.Y), MathF.Max(c2.Y, c3.Y));

        var worldAabb = new Box2(minX, minY, maxX, maxY).Translated(viewportToMapOffset);
        var foundOpening = openingCache.TryFindOpeningBounds(
            mapId,
            worldAabb,
            openingBounds,
            out combinedOpeningBounds,
            maxOpeningBounds,
            exactOpeningBounds,
            _zLevelGrids,
            _mapManager,
            _mapSystem,
            _transform,
            _tile);

        return _zLevelGrids.Count == 0 || foundOpening;
    }

    private void RenderZLevelPasses(IClydeViewport viewport)
    {
        ClearZLevelCompositeState();

        if (_eye is null ||
            !_config.GetCVar(CMUZLevelsCVars.Enabled) ||
            !_config.GetCVar(CMUZLevelsCVars.RenderEnabled))
        {
            viewport.Render();
            return;
        }

        var fallbackEye = _eye;

        using var zRenderProfile = _prof.Group("CMU Z Render");

        // Cache frequently accessed components/systems
        _xformQuery ??= _entityManager.GetEntityQuery<TransformComponent>();

        // Cache systems and components
        _zLevels ??= _entityManager.System<CMUClientZLevelsSystem>();
        _mapSystem ??= _entityManager.System<SharedMapSystem>();
        _transform ??= _entityManager.System<SharedTransformSystem>();
        _lookup ??= _entityManager.System<EntityLookupSystem>();
        _examine ??= _entityManager.System<ExamineSystem>();
        _containers ??= _entityManager.System<SharedContainerSystem>();

        if (!TryGetZLevelViewEntity(fallbackEye, out _, out var zLevelViewer, out var viewXform) ||
            viewXform.MapUid is null)
        {
            viewport.Render();
            return;
        }

        var lookUp = zLevelViewer.LookUp || zLevelViewer.StairPreviewUp ? 1 : 0;
        var maxDepth = Math.Clamp(
            _config.GetCVar(CMUZLevelsCVars.MaxRenderDepth),
            0,
            CMUSharedZLevelsSystem.MaxZLevelsBelowRendering);
        var maxOpeningRects = Math.Max(0, _config.GetCVar(CMUZLevelsCVars.MaxOpeningRectsPerPass));
        var lowestDepth = 0;
        var weatherSourceMapId = GetWeatherSourceMapId(viewXform.MapUid.Value, viewXform.MapID);
        _zOpeningBounds.Clear();
        using (var openingProfile = _prof.Group("CMU Z Opening Query"))
        {
            for (var i = 0; i >= -maxDepth; i--)
            {
                var checkingMap = viewXform.MapUid.Value;

                if (i != 0)
                {
                    if (!_zLevels.TryMapOffset(viewXform.MapUid.Value, i, out var mapUidBelow))
                        continue;

                    checkingMap = mapUidBelow.Value;
                }

                lowestDepth = i;

                var hasOpenings = i == 0
                    ? TryFindEmptyTiles(
                        checkingMap,
                        viewport,
                        _zOpeningBounds,
                        out _,
                        maxOpeningRects == 0 ? int.MaxValue : maxOpeningRects + 1)
                    : TryFindEmptyTiles(checkingMap, viewport);

                if (!hasOpenings)
                    break;
            }
        }

        //From the lowest depth to the highest, render each level
        using (var passProfile = _prof.Group("CMU Z Render Passes"))
        {
            for (var depth = lowestDepth; depth <= lookUp; depth++)
            {
                if (depth == 0)
                {
                    viewport.Eye = fallbackEye;
                }
                else
                {
                    if (!_zLevels.TryMapOffset(viewXform.MapUid.Value, depth, out _, out var mapComp))
                        continue;

                    Angle rotation = fallbackEye.Rotation * -1;
                    var offset = rotation.ToWorldVec() * CMUClientZLevelsSystem.ZLevelOffset * depth;
                    var renderPosition = fallbackEye.Position.Position;
                    var fovPosition = renderPosition;
                    var eyeOffset = fallbackEye.Offset + offset;
                    var separateStairPreview = depth == 1 &&
                        zLevelViewer.StairPreviewUp &&
                        !zLevelViewer.LookUp;

                    if (separateStairPreview)
                    {
                        SetStairPreviewOrigins(zLevelViewer);
                        if (_stairPreviewOrigins.Count == 0)
                            continue;

                        fovPosition = _stairPreviewOrigins[0];
                        eyeOffset += renderPosition - fovPosition;
                    }

                    _zEye.LowestDepth = lowestDepth;
                    _zEye.Depth = depth;
                    _zEye.HighestDepth = lookUp;
                    _zEye.BaseMapId = viewXform.MapID;
                    _zEye.WeatherSourceMapId = weatherSourceMapId;
                    _zEye.Position = new MapCoordinates(fovPosition, mapComp.MapId);
                    _zEye.DrawFov = fallbackEye.DrawFov && depth >= 0;
                    _zEye.DrawLight = fallbackEye.DrawLight;
                    _zEye.Offset = eyeOffset;
                    _zEye.Rotation = fallbackEye.Rotation;
                    _zEye.Scale = fallbackEye.Scale;
                    _zEye.ConfigureVisibleEntityIndicators(
                        _config.GetCVar(CMUZLevelsCVars.VisibleEntityIndicators) && depth == 1 && !separateStairPreview,
                        _zOpeningBounds);

                    if (separateStairPreview)
                    {
                        RenderStairPreviewComposite(viewport, _zEye);
                        continue;
                    }

                    viewport.Eye = _zEye;
                }

                viewport.ClearColor = depth == lowestDepth ? Color.Black : null;
                viewport.Render();
            }
        }

        // Restore the Eye
        Eye = fallbackEye;
        viewport.Eye = Eye;
    }

    private bool TryGetZLevelViewEntity(
        IEye fallbackEye,
        out EntityUid viewEntity,
        out CMUZLevelViewerComponent viewer,
        out TransformComponent xform)
    {
        viewEntity = default;
        viewer = default!;
        xform = default!;

        var query = _entityManager.EntityQueryEnumerator<EyeComponent>();
        while (query.MoveNext(out var uid, out var eye))
        {
            if (!ReferenceEquals(eye.Eye, fallbackEye))
                continue;

            var candidate = eye.Target ?? uid;
            if (TryResolveZLevelViewer(candidate, out viewEntity, out viewer, out xform))
                return true;

            if (candidate != uid &&
                TryResolveZLevelViewer(uid, out viewEntity, out viewer, out xform))
            {
                return true;
            }

            return false;
        }

        return false;
    }

    private bool TryResolveZLevelViewer(
        EntityUid candidate,
        out EntityUid viewEntity,
        out CMUZLevelViewerComponent viewer,
        out TransformComponent xform)
    {
        viewEntity = default;
        viewer = default!;
        xform = default!;

        var current = candidate;
        for (var i = 0; i < 8; i++)
        {
            if (_entityManager.TryGetComponent<CMUZLevelViewerComponent>(current, out var currentViewer) &&
                _xformQuery is not null &&
                _xformQuery.Value.TryComp(current, out var currentXform) &&
                currentXform.MapUid is not null)
            {
                viewEntity = current;
                viewer = currentViewer;
                xform = currentXform;
                return true;
            }

            if (_containers is null ||
                !_containers.TryGetContainingContainer((current, null, null), out var container))
            {
                break;
            }

            current = container.Owner;
        }

        return false;
    }

    private MapId GetWeatherSourceMapId(EntityUid baseMap, MapId fallback)
    {
        if (_zLevels is null ||
            !_zLevels.TryGetZNetwork(baseMap, out var network) ||
            !_zLevels.TryGetMapAtDepth(network.Value, 0, out _, out var groundMapComp))
        {
            return fallback;
        }

        return groundMapComp.MapId;
    }

    private void RenderStairPreviewComposite(IClydeViewport sourceViewport, ZEye sourceEye)
    {
        EnsureStairPreviewViewport(sourceViewport);
        if (_stairPreviewViewport is null)
            return;

        CopyZEye(_stairPreviewEye, sourceEye);
        _stairPreviewEye.DrawFov = false;
        _stairPreviewEye.ConfigureVisibleEntityIndicators(false, _zOpeningBounds);

        _stairPreviewViewport.Eye = _stairPreviewEye;
        _stairPreviewViewport.ClearColor = Color.Transparent;
        _stairPreviewViewport.Render();
        _drawStairPreviewComposite = true;
    }

    private void EnsureStairPreviewViewport(IClydeViewport sourceViewport)
    {
        if (_stairPreviewViewport != null &&
            _stairPreviewViewport.Size == sourceViewport.Size &&
            _stairPreviewViewport.RenderScale.Equals(sourceViewport.RenderScale))
        {
            return;
        }

        _stairPreviewViewport?.Dispose();
        _stairPreviewViewport = _clyde.CreateViewport(
            sourceViewport.Size,
            new TextureSampleParameters
            {
                Filter = StretchMode == ScalingViewportStretchMode.Bilinear,
            },
            "cmu-z-stair-preview");
        _stairPreviewViewport.RenderScale = sourceViewport.RenderScale;
    }

    private static void CopyZEye(ZEye target, ZEye source)
    {
        target.LowestDepth = source.LowestDepth;
        target.Depth = source.Depth;
        target.HighestDepth = source.HighestDepth;
        target.BaseMapId = source.BaseMapId;
        target.WeatherSourceMapId = source.WeatherSourceMapId;
        target.Position = source.Position;
        target.DrawFov = source.DrawFov;
        target.DrawLight = source.DrawLight;
        target.Offset = source.Offset;
        target.Rotation = source.Rotation;
        target.Scale = source.Scale;
    }

    private void DrawZLevelComposites(IRenderHandle handle, UIBox2i drawBox)
    {
        if (_drawStairPreviewComposite)
            DrawStairPreviewComposite(handle.DrawingHandleScreen, drawBox);
    }

    private void DrawStairPreviewComposite(DrawingHandleScreen screen, UIBox2 drawBox)
    {
        if (_stairPreviewViewport is null ||
            _stairPreviewViewport.Eye is null ||
            _stairPreviewEye.Position.MapId == MapId.Nullspace)
        {
            return;
        }

        screen.UseShader(_proto.Index(StencilClearShader).Instance());
        screen.DrawRect(drawBox, Color.White);

        screen.UseShader(_proto.Index(StencilMaskShader).Instance());
        DrawStairPreviewFovMask(screen, drawBox);

        screen.UseShader(_proto.Index(StencilEqualDrawShader).Instance());
        screen.DrawTextureRect(_stairPreviewViewport.RenderTarget.Texture, drawBox);

        screen.UseShader(_proto.Index(StencilClearShader).Instance());
        screen.DrawRect(drawBox, Color.White);
        screen.UseShader(null);
    }

    private void DrawStairPreviewFovMask(DrawingHandleScreen screen, UIBox2 drawBox)
    {
        if (_stairPreviewViewport is null ||
            _mapSystem is null ||
            _transform is null ||
            _lookup is null ||
            _examine is null ||
            !TryGetViewportWorldAabb(_stairPreviewViewport, out var worldAabb))
        {
            return;
        }

        var mapId = _stairPreviewEye.Position.MapId;
        if (_stairPreviewOrigins.Count == 0)
            return;

        _stairPreviewGrids.Clear();
        _mapManager.FindGridsIntersecting(mapId, worldAabb, ref _stairPreviewGrids, approx: true, includeMap: true);

        foreach (var grid in _stairPreviewGrids)
        {
            var gridMatrix = _transform.GetWorldMatrix(grid.Owner);
            foreach (var tile in _mapSystem.GetTilesIntersecting(grid.Owner, grid.Comp, worldAabb, ignoreEmpty: true))
            {
                var localBounds = _lookup.GetLocalBounds(tile, grid.Comp.TileSize).Enlarged(0.01f);
                var targetPosition = Vector2.Transform(localBounds.Center, gridMatrix);
                var target = new MapCoordinates(targetPosition, mapId);

                if (!CanAnyStairPreviewOriginSee(target, mapId))
                    continue;

                screen.DrawRect(GetCompositeScreenBox(localBounds, gridMatrix, drawBox), Color.White);
            }
        }

        _stairPreviewGrids.Clear();
    }

    private void SetStairPreviewOrigins(CMUZLevelViewerComponent viewer)
    {
        _stairPreviewOrigins.Clear();

        var count = Math.Clamp(
            viewer.StairPreviewPositionCount,
            0,
            CMUZLevelViewerComponent.MaxStairPreviewPositions);

        for (var i = 0; i < count; i++)
        {
            var position = i switch
            {
                0 => viewer.StairPreviewPosition,
                1 => viewer.StairPreviewPosition2,
                2 => viewer.StairPreviewPosition3,
                3 => viewer.StairPreviewPosition4,
                _ => default,
            };

            if (position == default)
                continue;

            _stairPreviewOrigins.Add(position);
        }
    }

    private bool CanAnyStairPreviewOriginSee(MapCoordinates target, MapId mapId)
    {
        if (_examine is null)
            return false;

        foreach (var originPosition in _stairPreviewOrigins)
        {
            var origin = new MapCoordinates(originPosition, mapId);
            if (_examine.InRangeUnOccluded(origin, target, 0f, null))
                return true;
        }

        return false;
    }

    private bool TryGetViewportWorldAabb(IClydeViewport viewport, out Box2 worldAabb)
    {
        worldAabb = default;

        if (viewport.Eye is null)
            return false;

        var c0 = viewport.LocalToWorld(Vector2.Zero).Position;
        var c1 = viewport.LocalToWorld(new Vector2(viewport.Size.X, 0)).Position;
        var c2 = viewport.LocalToWorld(new Vector2(0, viewport.Size.Y)).Position;
        var c3 = viewport.LocalToWorld(viewport.Size).Position;

        var minX = MathF.Min(MathF.Min(c0.X, c1.X), MathF.Min(c2.X, c3.X));
        var minY = MathF.Min(MathF.Min(c0.Y, c1.Y), MathF.Min(c2.Y, c3.Y));
        var maxX = MathF.Max(MathF.Max(c0.X, c1.X), MathF.Max(c2.X, c3.X));
        var maxY = MathF.Max(MathF.Max(c0.Y, c1.Y), MathF.Max(c2.Y, c3.Y));

        worldAabb = new Box2(minX, minY, maxX, maxY);
        return true;
    }

    private UIBox2 GetCompositeScreenBox(Box2 localBounds, Matrix3x2 gridMatrix, UIBox2 drawBox)
    {
        var c0 = CompositeWorldToScreen(Vector2.Transform(localBounds.BottomLeft, gridMatrix), drawBox);
        var c1 = CompositeWorldToScreen(Vector2.Transform(localBounds.TopLeft, gridMatrix), drawBox);
        var c2 = CompositeWorldToScreen(Vector2.Transform(localBounds.TopRight, gridMatrix), drawBox);
        var c3 = CompositeWorldToScreen(Vector2.Transform(localBounds.BottomRight, gridMatrix), drawBox);

        var minX = MathF.Min(MathF.Min(c0.X, c1.X), MathF.Min(c2.X, c3.X));
        var minY = MathF.Min(MathF.Min(c0.Y, c1.Y), MathF.Min(c2.Y, c3.Y));
        var maxX = MathF.Max(MathF.Max(c0.X, c1.X), MathF.Max(c2.X, c3.X));
        var maxY = MathF.Max(MathF.Max(c0.Y, c1.Y), MathF.Max(c2.Y, c3.Y));

        return new UIBox2(minX, minY, maxX, maxY);
    }

    private Vector2 CompositeWorldToScreen(Vector2 worldPosition, UIBox2 drawBox)
    {
        if (_stairPreviewViewport is null)
            return drawBox.TopLeft;

        var viewportPosition = _stairPreviewViewport.WorldToLocal(worldPosition);
        return drawBox.TopLeft + viewportPosition * (drawBox.Size / (Vector2) _stairPreviewViewport.Size);
    }

    private void ClearZLevelCompositeState()
    {
        _drawStairPreviewComposite = false;
    }

    private void DisposeZLevelViewports()
    {
        _stairPreviewViewport?.Dispose();
        _stairPreviewViewport = null;
        ClearZLevelCompositeState();
    }

    public sealed class ZEye : Robust.Shared.Graphics.Eye
    {
        private readonly List<Box2> _visibleEntityIndicatorBounds = new();

        public int LowestDepth;
        public int Depth;
        public int HighestDepth;
        public MapId BaseMapId;
        public MapId WeatherSourceMapId;

        public IReadOnlyList<Box2> VisibleEntityIndicatorBounds => _visibleEntityIndicatorBounds;
        public bool DrawVisibleEntityIndicators { get; private set; }

        public void ConfigureVisibleEntityIndicators(bool enabled, List<Box2> visibilityBounds)
        {
            _visibleEntityIndicatorBounds.Clear();

            if (!enabled || visibilityBounds.Count == 0)
            {
                DrawVisibleEntityIndicators = false;
                return;
            }

            _visibleEntityIndicatorBounds.AddRange(visibilityBounds);
            DrawVisibleEntityIndicators = true;
        }
    }

}
