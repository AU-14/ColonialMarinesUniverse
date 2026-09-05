using System.Numerics;
using System.Collections.Generic;
using System.Diagnostics;
using Content.Client.Examine;
using Content.Shared.CMU14.ZLevels;
using Content.Client.CMU14.ZLevels.Core;
using Content.Client.CMU14.ZLevels.Culling;
using Content.Shared.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Maps;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Containers;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Profiling;
using Robust.Shared.Prototypes;

namespace Content.Client.Viewport;

public sealed partial class ScalingViewport
{
    [Dependency] private ITileDefinitionManager _tile = default!;
    [Dependency] private ProfManager _prof = default!;
    [Dependency] private Robust.Shared.Timing.IGameTiming _timing = default!;

    private static readonly ProtoId<ShaderPrototype> StencilClearShader = "StencilClear";
    private static readonly ProtoId<ShaderPrototype> StencilMaskShader = "StencilMask";
    private static readonly ProtoId<ShaderPrototype> StencilEqualDrawShader = "StencilEqualDraw";
    private static readonly Color StairPreviewTint = new(0.05f, 0.05f, 0.05f, 0.48f);

    private CMUClientZLevelsSystem? _zLevels;
    private SharedMapSystem? _mapSystem;
    private SharedTransformSystem? _transform;
    private EntityLookupSystem? _lookup;
    private ExamineSystem? _examine;
    private SharedContainerSystem? _containers;
    private CMUZLevelSpriteCullingSystem? _spriteCulling;
    private ShaderInstance? _stencilClearShaderInstance;
    private ShaderInstance? _stencilMaskShaderInstance;
    private ShaderInstance? _stencilEqualDrawShaderInstance;

    private EntityQuery<TransformComponent>? _xformQuery;

    private List<Entity<MapGridComponent>> _zLevelGrids = new();
    private List<Entity<MapGridComponent>> _stairPreviewGrids = new();
    private readonly List<StairPreviewOrigin> _stairPreviewOrigins = new(CMUZLevelViewerComponent.MaxStairPreviewPositions);
    private readonly CMUZViewportRenderPlan _zRenderPlan = new();
    private readonly List<Box2> _stairPreviewTileBounds = new();
    private readonly Vector2[] _stairPreviewStencilCorners = new Vector2[4];
    private readonly List<Box2> _zOpeningBounds = new();
    private readonly List<Box2> _zLowerOpeningBounds = new();
    private readonly ZEye _zEye = new();
    private readonly ZEye _stairPreviewEye = new();
    private IClydeViewport? _stairPreviewViewport;
    private bool _drawStairPreviewComposite;
    // AU14 (building overhaul): "faint upper" rooftop-awareness pass. Reuses the stair-preview offscreen
    // viewport (the two are mutually exclusive: the faint pass only runs when neither LookUp nor
    // StairPreviewUp is active, which is exactly when the stair-preview composite is idle).
    private bool _drawFaintUpperComposite;
    private float _faintUpperAlpha;
    private EntityUid? _lastZLevelEyeEntity;
    private EntityUid? _lastZLevelViewEntity;
    private TimeSpan _zLowerRenderGraceUntil = TimeSpan.Zero;
    private int _zLowerRenderGraceLowestDepth;
    private IEye? _zLowerRenderGraceEye;
    private EntityUid? _zLowerRenderGraceViewer;
    private EntityUid? _zLowerRenderGraceMap;
    private bool _zRenderDiagnostics;

    internal static ZLevelRenderDebugStats LastZRenderDebugStats { get; } = new();

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

        if (!TryGetViewportWorldAabb(viewport, out var viewportWorldAabb))
            return true;

        var worldAabb = viewportWorldAabb.Translated(viewportToMapOffset);

        return TryFindEmptyTilesInAabb(
            mapUid,
            worldAabb,
            openingBounds,
            out combinedOpeningBounds,
            maxOpeningBounds,
            exactOpeningBounds);
    }

    private bool TryFindEmptyTilesInAabb(
        EntityUid mapUid,
        Box2 worldAabb,
        List<Box2>? openingBounds,
        out Box2 combinedOpeningBounds,
        int maxOpeningBounds = int.MaxValue,
        bool exactOpeningBounds = false)
    {
        combinedOpeningBounds = default;

        if (_xformQuery is null || !_xformQuery.Value.TryComp(mapUid, out var xform))
            return true;

        var mapId = xform.MapID;

        if (_mapSystem is null || _transform is null)
            return true;

        _zLevels ??= _entityManager.System<CMUClientZLevelsSystem>();
        var openingCache = _zLevels.OpeningCache;

        var foundOpening = openingCache.TryFindOpeningBounds(
            mapId,
            worldAabb,
            openingBounds,
            out combinedOpeningBounds,
            maxOpeningBounds,
            exactOpeningBounds,
            _zLevelGrids,
            _mapSystem,
            _transform,
            _tile);

        return _zLevelGrids.Count == 0 || foundOpening;
    }

    internal void RenderZLevelPasses(IClydeViewport viewport)
    {
        using var renderState = new CMUZViewportRenderState(viewport);
        viewport.ClearColor = Color.Black;
        ClearZLevelCompositeState();
        _zRenderPlan.Reset();
        _zRenderDiagnostics = _cfg.GetCVar(CMUZLevelsCVars.ClientDiagnosticsEnabled);
        if (_zRenderDiagnostics)
            LastZRenderDebugStats.Reset();
        var totalStart = _zRenderDiagnostics ? Stopwatch.GetTimestamp() : 0;

        var zLevelsEnabled = _cfg.GetCVar(CMUZLevelsCVars.Enabled);
        var renderEnabled = _cfg.GetCVar(CMUZLevelsCVars.RenderEnabled);

        if (_eye is null ||
            !ShouldUseZLevelRenderPasses(
                zLevelsEnabled,
                renderEnabled))
        {
            ResetLowerRenderGrace();
            if (_zRenderDiagnostics)
                LastZRenderDebugStats.SkipReason = _eye is null
                    ? "no viewport eye"
                    : !zLevelsEnabled
                        ? "cmu.zlevels.enabled=false"
                        : !renderEnabled
                            ? "cmu.zlevels.render_enabled=false"
                            : "z render disabled";
            var renderStart = _zRenderDiagnostics ? Stopwatch.GetTimestamp() : 0;
            RenderZSpritePass(viewport);
            if (_zRenderDiagnostics)
            {
                LastZRenderDebugStats.BasePassRendered = true;
                LastZRenderDebugStats.BaseRenderMs = GetElapsedMilliseconds(renderStart);
                LastZRenderDebugStats.TotalRenderMs = GetElapsedMilliseconds(totalStart);
            }
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

        if (!TryGetZLevelViewEntity(fallbackEye, out var viewEntity, out var zLevelViewer, out var viewXform) ||
            viewXform.MapUid is null)
        {
            ResetLowerRenderGrace();
            if (_zRenderDiagnostics)
                LastZRenderDebugStats.SkipReason = "no Z-level viewer for current eye";
            var renderStart = _zRenderDiagnostics ? Stopwatch.GetTimestamp() : 0;
            RenderZSpritePass(viewport);
            if (_zRenderDiagnostics)
            {
                LastZRenderDebugStats.BasePassRendered = true;
                LastZRenderDebugStats.BaseRenderMs = GetElapsedMilliseconds(renderStart);
                LastZRenderDebugStats.TotalRenderMs = GetElapsedMilliseconds(totalStart);
            }
            return;
        }

        if (!ReferenceEquals(_zLowerRenderGraceEye, fallbackEye) ||
            _zLowerRenderGraceViewer != viewEntity ||
            _zLowerRenderGraceMap != viewXform.MapUid)
        {
            ResetLowerRenderGrace();
            _zLowerRenderGraceEye = fallbackEye;
            _zLowerRenderGraceViewer = viewEntity;
            _zLowerRenderGraceMap = viewXform.MapUid;
        }

        var lookUp = zLevelViewer.LookUp || zLevelViewer.StairPreviewUp ? 1 : 0;
        var maxDepth = Math.Clamp(
            _cfg.GetCVar(CMUZLevelsCVars.MaxRenderDepth),
            0,
            CMUSharedZLevelsSystem.MaxZLevelsBelowRendering);
        var maxOpeningRects = Math.Max(0, _cfg.GetCVar(CMUZLevelsCVars.MaxOpeningRectsPerPass));
        var lowestDepth = 0;
        var weatherSourceMapId = GetWeatherSourceMapId(viewXform.MapUid.Value, viewXform.MapID);
        if (!TryGetViewportWorldAabb(viewport, out var viewportWorldAabb))
        {
            if (_zRenderDiagnostics)
                LastZRenderDebugStats.SkipReason = "no viewport world bounds";
            var renderStart = _zRenderDiagnostics ? Stopwatch.GetTimestamp() : 0;
            RenderZSpritePass(viewport);
            if (_zRenderDiagnostics)
            {
                LastZRenderDebugStats.BasePassRendered = true;
                LastZRenderDebugStats.BaseRenderMs = GetElapsedMilliseconds(renderStart);
                LastZRenderDebugStats.TotalRenderMs = GetElapsedMilliseconds(totalStart);
            }
            return;
        }

        if (_zRenderDiagnostics)
        {
            LastZRenderDebugStats.UsedZRender = true;
            LastZRenderDebugStats.SkipReason = "rendered";
            LastZRenderDebugStats.BaseMapId = viewXform.MapID;
            LastZRenderDebugStats.MaxDepth = maxDepth;
            LastZRenderDebugStats.LookUpDepth = lookUp;
            LastZRenderDebugStats.ViewerLookUp = zLevelViewer.LookUp;
            LastZRenderDebugStats.StairPreviewUp = zLevelViewer.StairPreviewUp;
            LastZRenderDebugStats.BaseMapUid = viewXform.MapUid;
            LastZRenderDebugStats.ViewportWorldAabb = viewportWorldAabb;
            LastZRenderDebugStats.ViewportWorldArea = GetArea(viewportWorldAabb);
        }
        var zRenderRotation = -fallbackEye.Rotation;
        var zRenderOffsetPerDepth = zRenderRotation.ToWorldVec() * CMUClientZLevelsSystem.ZLevelOffset;
        if (_zRenderDiagnostics)
            LastZRenderDebugStats.ZRenderOffsetPerDepth = zRenderOffsetPerDepth;

        _zOpeningBounds.Clear();
        using (var openingProfile = _prof.Group("CMU Z Opening Query"))
        {
            var openingStart = _zRenderDiagnostics ? Stopwatch.GetTimestamp() : 0;
            var currentOpeningStart = _zRenderDiagnostics ? Stopwatch.GetTimestamp() : 0;
            var hasOpenings = TryFindEmptyTilesInAabb(
                viewXform.MapUid.Value,
                viewportWorldAabb,
                _zOpeningBounds,
                out _,
                maxOpeningRects == 0 || maxOpeningRects == int.MaxValue ? int.MaxValue : maxOpeningRects + 1,
                true);
            if (_zRenderDiagnostics)
                LastZRenderDebugStats.CurrentOpeningQueryMs = GetElapsedMilliseconds(currentOpeningStart);

            if (_zRenderDiagnostics)
            {
                LastZRenderDebugStats.OpeningQueryRan = true;
                LastZRenderDebugStats.OpeningQueryFoundOpening = hasOpenings;
                LastZRenderDebugStats.OpeningsBeforeLos = _zOpeningBounds.Count;
                LastZRenderDebugStats.OpeningBoundsTruncated = maxOpeningRects > 0 && _zOpeningBounds.Count > maxOpeningRects;
                LastZRenderDebugStats.OpeningQueryConservativeNoBounds = hasOpenings && _zOpeningBounds.Count == 0;
                LastZRenderDebugStats.OpeningAreaBeforeLos = GetAreaSum(_zOpeningBounds);
            }

            var completeOpenings = (maxOpeningRects == 0 || _zOpeningBounds.Count <= maxOpeningRects) &&
                !(hasOpenings && _zOpeningBounds.Count == 0);
            _zRenderPlan.BaseOpenings.SetOpenings(
                viewXform.MapID, viewportWorldAabb, _zOpeningBounds, completeOpenings);
            hasOpenings = _zRenderPlan.BaseOpenings.Visibility != CMUZVisibility.Hidden;

            // A finite set of failed point rays cannot prove an entire aperture hidden. Geometry
            // admission therefore needs no LOS sampling; the current map's FOV still masks the result.
            if (_zRenderDiagnostics)
            {
                LastZRenderDebugStats.OpeningsAfterLos = _zOpeningBounds.Count;
                LastZRenderDebugStats.OpeningLosConservativeFallback = hasOpenings;
                LastZRenderDebugStats.OpeningLosMode = hasOpenings ? "geometry; LOS unknown" : "closed geometry";
                LastZRenderDebugStats.OpeningAreaAfterLos = GetAreaSum(_zOpeningBounds);
            }

            if (_zRenderDiagnostics)
                LastZRenderDebugStats.VisibleCurrentOpenings = hasOpenings;
            var hasLowerMap = _zLevels.TryMapOffset(viewXform.MapUid.Value, -1, out _);
            if (_zRenderDiagnostics)
                LastZRenderDebugStats.HasLowerMap = hasLowerMap;

            var lowerDiscoveryStart = _zRenderDiagnostics ? Stopwatch.GetTimestamp() : 0;
            if (hasOpenings &&
                maxDepth > 0 &&
                hasLowerMap)
            {
                _zRenderPlan.LowerChain.SetProjected(_zRenderPlan.BaseOpenings, viewXform.MapID, Vector2.Zero);

                var chainDepth = 0;
                var filterMargin = GetLowerFilterMargin(viewport);
                for (var i = -1; i >= -maxDepth; i--)
                {
                    if (_zRenderDiagnostics)
                        LastZRenderDebugStats.LowerDepthsChecked++;
                    if (!_zLevels.TryMapOffset(viewXform.MapUid.Value, i, out var mapUidBelow, out var lowerMapComp))
                        continue;

                    var pass = _zRenderPlan.LowerPass(i);
                    pass.SetProjected(_zRenderPlan.LowerChain, lowerMapComp.MapId,
                        zRenderOffsetPerDepth * (i - chainDepth),
                        filterMargin * (chainDepth == 0 && zLevelViewer.LookUp ? 2f : 1f));
                    chainDepth = i;
                    if (pass.Visibility == CMUZVisibility.Hidden)
                    {
                        if (_zRenderDiagnostics)
                            LastZRenderDebugStats.LowerDepthBreakDepth = i;
                        break;
                    }

                    lowestDepth = i;
                    if (_zRenderDiagnostics)
                        LastZRenderDebugStats.LowerDepthsWithMaps++;

                    // The last rendered floor does not need an aperture query for a nonexistent pass.
                    if (i == -maxDepth)
                        break;

                    var lowerOpeningStart = _zRenderDiagnostics ? Stopwatch.GetTimestamp() : 0;
                    var hasDeeperOpening = FindLowerChainOpenings(mapUidBelow.Value, pass, maxOpeningRects);
                    if (_zRenderDiagnostics)
                        LastZRenderDebugStats.LowerDepthOpeningQueryMs += GetElapsedMilliseconds(lowerOpeningStart);

                    if (!hasDeeperOpening)
                    {
                        if (_zRenderDiagnostics)
                            LastZRenderDebugStats.LowerDepthBreakDepth = i;
                        break;
                    }
                }
            }
            if (_zRenderDiagnostics)
                LastZRenderDebugStats.LowerDepthDiscoveryMs = GetElapsedMilliseconds(lowerDiscoveryStart);

            ApplyLowerRenderGrace(maxDepth, hasLowerMap, ref lowestDepth);

            if (_zRenderDiagnostics)
            {
                LastZRenderDebugStats.LowerSuppressedByOpeningGate = maxDepth > 0 &&
                    hasLowerMap &&
                    lowestDepth == 0 &&
                    !hasOpenings;
                LastZRenderDebugStats.OpeningQueryTotalMs = GetElapsedMilliseconds(openingStart);
            }
        }

        if (_zRenderDiagnostics)
            LastZRenderDebugStats.LowestDepth = lowestDepth;

        //From the lowest depth to the highest, render each level
        using (var passProfile = _prof.Group("CMU Z Render Passes"))
        {
            for (var depth = lowestDepth; depth <= lookUp; depth++)
            {
                if (depth == 0)
                {
                    if (zLevelViewer.LookUp)
                    {
                        _zEye.LowestDepth = lowestDepth;
                        _zEye.Depth = 0;
                        _zEye.HighestDepth = lookUp;
                        _zEye.BaseMapId = viewXform.MapID;
                        _zEye.WeatherSourceMapId = viewXform.MapID;
                        _zEye.Position = fallbackEye.Position;
                        _zEye.DrawFov = fallbackEye.DrawFov;
                        _zEye.DrawLight = fallbackEye.DrawLight;
                        _zEye.Offset = fallbackEye.Offset;
                        _zEye.Rotation = fallbackEye.Rotation;
                        _zEye.Scale = fallbackEye.Scale;
                        _zEye.VisualZOffset = Vector2.Zero;
                        _zEye.BlurCurrentLevel = true;
                        _zEye.ConfigureVisibleEntityIndicators(false, _zOpeningBounds);

                        viewport.Eye = _zEye;
                    }
                    else
                    {
                        viewport.Eye = fallbackEye;
                    }
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
                        SetStairPreviewOrigins(zLevelViewer, _transform.GetWorldPosition(viewXform));
                        if (_stairPreviewOrigins.Count == 0)
                            continue;

                        fovPosition = _stairPreviewOrigins[0].Position;
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
                    _zEye.VisualZOffset = offset;
                    _zEye.BlurCurrentLevel = false;
                    _zEye.ConfigureVisibleEntityIndicators(
                        _cfg.GetCVar(CMUZLevelsCVars.VisibleEntityIndicators) && depth == 1 && !separateStairPreview,
                        _zOpeningBounds);

                    if (separateStairPreview)
                    {
                        var stairPreviewStart = _zRenderDiagnostics ? Stopwatch.GetTimestamp() : 0;
                        if (RenderStairPreviewComposite(viewport, _zEye) && _zRenderDiagnostics)
                            LastZRenderDebugStats.StairPreviewCompositesRendered++;
                        if (_zRenderDiagnostics)
                            LastZRenderDebugStats.StairPreviewRenderMs += GetElapsedMilliseconds(stairPreviewStart);
                        continue;
                    }

                    viewport.Eye = _zEye;
                }

                viewport.ClearColor = depth == lowestDepth ? Color.Black : null;
                var renderStart = _zRenderDiagnostics ? Stopwatch.GetTimestamp() : 0;
                RenderZSpritePass(viewport, depth < 0
                    ? _zRenderPlan.FindLowerPass(depth, viewport.Eye!.Position.MapId)
                    : null);
                if (_zRenderDiagnostics)
                {
                    var renderMs = GetElapsedMilliseconds(renderStart);
                    if (depth < 0)
                    {
                        LastZRenderDebugStats.LowerPassesRendered++;
                        LastZRenderDebugStats.LowerRenderMs += renderMs;
                        LastZRenderDebugStats.LowerRenderedDepths.Add(depth);
                    }
                    else if (depth > 0)
                    {
                        LastZRenderDebugStats.UpperPassesRendered++;
                        LastZRenderDebugStats.UpperRenderMs += renderMs;
                    }
                    else
                    {
                        LastZRenderDebugStats.BasePassRendered = true;
                        LastZRenderDebugStats.BaseRenderMs += renderMs;
                    }
                }
            }
        }

        // AU14 (building overhaul): stage 1 of the look-up cycle. When the viewer toggled faint mode, is not
        // already looking up and is not under a ceiling, ghost the level directly above at low alpha.
        if (lookUp == 0 && zLevelViewer.FaintUp)
            RenderFaintUpperComposite(viewport, fallbackEye, viewXform, lowestDepth, weatherSourceMapId);

        if (_zRenderDiagnostics)
            LastZRenderDebugStats.TotalRenderMs = GetElapsedMilliseconds(totalStart);
    }

    private void RenderZSpritePass(IClydeViewport viewport, CMUZVisibilityMask? mask = null)
    {
        _zLevels ??= _entityManager.System<CMUClientZLevelsSystem>();
        _zLevels.RenderViewport(viewport, mask);
        if (mask is null || !_zRenderDiagnostics)
            return;

        _spriteCulling ??= _entityManager.System<CMUZLevelSpriteCullingSystem>();
        LastZRenderDebugStats.SpriteCullCandidates += _spriteCulling.LastCandidates;
        LastZRenderDebugStats.SpritesCulled += _spriteCulling.LastHidden;
    }

    private void ResetLowerRenderGrace()
    {
        _zLowerRenderGraceUntil = TimeSpan.Zero;
        _zLowerRenderGraceLowestDepth = 0;
        _zLowerRenderGraceEye = null;
        _zLowerRenderGraceViewer = null;
        _zLowerRenderGraceMap = null;
    }

    internal static bool ShouldUseZLevelRenderPasses(bool zLevelsEnabled, bool renderEnabled)
    {
        return zLevelsEnabled &&
               renderEnabled;
    }

    private void ApplyLowerRenderGrace(int maxDepth, bool hasLowerMap, ref int lowestDepth)
    {
        var graceSeconds = Math.Max(0f, _cfg.GetCVar(CMUZLevelsCVars.LowerRenderVisibilityGrace));
        if (_zRenderDiagnostics)
            LastZRenderDebugStats.LowerRenderGraceSeconds = graceSeconds;

        if (lowestDepth < 0)
        {
            _zLowerRenderGraceLowestDepth = lowestDepth;
            _zLowerRenderGraceUntil = graceSeconds > 0f
                ? _timing.CurTime + TimeSpan.FromSeconds(graceSeconds)
                : TimeSpan.Zero;

            if (_zRenderDiagnostics)
            {
                LastZRenderDebugStats.LowerRenderGraceLowestDepth = _zLowerRenderGraceLowestDepth;
                LastZRenderDebugStats.LowerRenderGraceRemainingMs = graceSeconds * 1000d;
            }
            return;
        }

        if (graceSeconds > 0f &&
            maxDepth > 0 &&
            hasLowerMap &&
            _zLowerRenderGraceLowestDepth < 0 &&
            _timing.CurTime <= _zLowerRenderGraceUntil)
        {
            lowestDepth = Math.Clamp(_zLowerRenderGraceLowestDepth, -maxDepth, -1);
            if (_zRenderDiagnostics)
            {
                LastZRenderDebugStats.LowerRenderGraceActive = true;
                LastZRenderDebugStats.LowerRenderGraceLowestDepth = lowestDepth;
                LastZRenderDebugStats.LowerRenderGraceRemainingMs = Math.Max(
                    0d,
                    (_zLowerRenderGraceUntil - _timing.CurTime).TotalMilliseconds);
            }
            return;
        }

        _zLowerRenderGraceLowestDepth = 0;
        _zLowerRenderGraceUntil = TimeSpan.Zero;
    }

    private bool FindLowerChainOpenings(EntityUid mapUid, CMUZVisibilityMask pass, int maxOpeningRects)
    {
        _zRenderPlan.LowerChain.SetProjected(pass, pass.MapId, Vector2.Zero);
        _zLowerOpeningBounds.Clear();

        // Query once for the floor. The AABB is broad phase only; intersections below retain each
        // aperture separately, so an L-shaped shaft never becomes its filled rectangular union.
        var searchBounds = pass.Bounds[0];
        for (var i = 1; i < pass.Bounds.Count; i++)
            searchBounds = searchBounds.Union(pass.Bounds[i]);

        var found = TryFindEmptyTilesInAabb(
            mapUid,
            searchBounds,
            _zLowerOpeningBounds,
            out _,
            maxOpeningRects == 0 || maxOpeningRects == int.MaxValue ? int.MaxValue : maxOpeningRects + 1,
            true);
        var complete = !(found && _zLowerOpeningBounds.Count == 0) &&
            (maxOpeningRects == 0 || _zLowerOpeningBounds.Count <= maxOpeningRects);
        _zRenderPlan.LowerChain.IntersectOpenings(_zLowerOpeningBounds, complete, maxOpeningRects);
        return _zRenderPlan.LowerChain.Visibility != CMUZVisibility.Hidden;
    }

    private float GetLowerFilterMargin(IClydeViewport viewport)
    {
        // zblur samples at most two screen pixels away. Keep their source pixels around every
        // aperture, plus one for texture filtering; repeated lower passes accumulate this support.
        var pixels = 1f;
        if (_cfg.GetCVar(CMUZLevelsCVars.BlurEnabled))
            pixels += Math.Clamp(_cfg.GetCVar(CMUZLevelsCVars.BlurStrength), 0f, 2f);

        var origin = viewport.LocalToWorld(Vector2.Zero).Position;
        var xStep = viewport.LocalToWorld(Vector2.UnitX).Position - origin;
        var yStep = viewport.LocalToWorld(Vector2.UnitY).Position - origin;
        return (xStep.Length() + yStep.Length()) * pixels;
    }

    private static double GetElapsedMilliseconds(long start)
    {
        return (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
    }

    private static float GetArea(Box2 bounds)
    {
        return Math.Max(0f, bounds.Width) * Math.Max(0f, bounds.Height);
    }

    private static float GetAreaSum(List<Box2> bounds)
    {
        var area = 0f;
        foreach (var bound in bounds)
        {
            area += GetArea(bound);
        }

        return area;
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

        if (TryGetCachedZLevelViewEntity(fallbackEye, out viewEntity, out viewer, out xform))
            return true;

        var query = _entityManager.EntityQueryEnumerator<EyeComponent>();
        while (query.MoveNext(out var uid, out var eye))
        {
            if (!ReferenceEquals(eye.Eye, fallbackEye))
                continue;

            var candidate = eye.Target ?? uid;
            if (TryResolveZLevelViewer(candidate, out viewEntity, out viewer, out xform))
            {
                CacheZLevelViewEntity(uid, viewEntity);
                return true;
            }

            if (candidate != uid &&
                TryResolveZLevelViewer(uid, out viewEntity, out viewer, out xform))
            {
                CacheZLevelViewEntity(uid, viewEntity);
                return true;
            }

            ClearZLevelViewEntityCache();
            return false;
        }

        ClearZLevelViewEntityCache();
        return false;
    }

    private bool TryGetCachedZLevelViewEntity(
        IEye fallbackEye,
        out EntityUid viewEntity,
        out CMUZLevelViewerComponent viewer,
        out TransformComponent xform)
    {
        viewEntity = default;
        viewer = default!;
        xform = default!;

        if (_lastZLevelEyeEntity is not { } eyeEntity ||
            _lastZLevelViewEntity is null ||
            !_entityManager.TryGetComponent<EyeComponent>(eyeEntity, out var eye) ||
            !ReferenceEquals(eye.Eye, fallbackEye))
        {
            return false;
        }

        var candidate = eye.Target ?? eyeEntity;
        if (TryResolveZLevelViewer(candidate, out viewEntity, out viewer, out xform))
        {
            CacheZLevelViewEntity(eyeEntity, viewEntity);
            return true;
        }

        if (candidate != eyeEntity &&
            TryResolveZLevelViewer(eyeEntity, out viewEntity, out viewer, out xform))
        {
            CacheZLevelViewEntity(eyeEntity, viewEntity);
            return true;
        }

        ClearZLevelViewEntityCache();
        return false;
    }

    private void CacheZLevelViewEntity(EntityUid eyeEntity, EntityUid viewEntity)
    {
        _lastZLevelEyeEntity = eyeEntity;
        _lastZLevelViewEntity = viewEntity;
    }

    private void ClearZLevelViewEntityCache()
    {
        _lastZLevelEyeEntity = null;
        _lastZLevelViewEntity = null;
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

    private bool RenderStairPreviewComposite(IClydeViewport sourceViewport, ZEye sourceEye)
    {
        EnsureStairPreviewViewport(sourceViewport);
        if (_stairPreviewViewport is null)
            return false;

        CopyZEye(_stairPreviewEye, sourceEye);
        _stairPreviewEye.DrawFov = false;
        _stairPreviewEye.ConfigureVisibleEntityIndicators(false, _zOpeningBounds);

        _stairPreviewViewport.Eye = _stairPreviewEye;
        _stairPreviewViewport.ClearColor = Color.Transparent;
        if (!BuildStairPreviewMask())
            return false;

        RenderZSpritePass(_stairPreviewViewport, _zRenderPlan.StairPreview);

        _drawStairPreviewComposite = true;
        return true;
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
        target.VisualZOffset = source.VisualZOffset;
        target.BlurCurrentLevel = source.BlurCurrentLevel;
    }

    private void DrawZLevelComposites(IRenderHandle handle, UIBox2i drawBox)
    {
        if (_drawStairPreviewComposite)
            DrawStairPreviewComposite(handle.DrawingHandleScreen, drawBox);

        // AU14 (building overhaul): the faint upper-level ghost is just the offscreen upper pass drawn at
        // low alpha over the frame - no stencil/LOS masking, that's the point (see everything above you).
        if (_drawFaintUpperComposite && _stairPreviewViewport is not null)
        {
            handle.DrawingHandleScreen.DrawTextureRect(
                _stairPreviewViewport.RenderTarget.Texture,
                drawBox,
                Color.White.WithAlpha(_faintUpperAlpha));
        }
    }

    // AU14 (building overhaul): render the level directly above into the offscreen viewport so it can be
    // composited faintly. Skipped when the viewer is under a ceiling (a non-empty tile straight above them):
    // there the roof legitimately hides the upper level, and ghosting it through a roof would be wall-hacks.
    private void RenderFaintUpperComposite(
        IClydeViewport viewport,
        IEye fallbackEye,
        TransformComponent viewXform,
        int lowestDepth,
        MapId weatherSourceMapId)
    {
        if (!_cfg.GetCVar(CMUZLevelsCVars.FaintUpperEnabled))
            return;

        if (_zLevels is null || _transform is null || _mapSystem is null || viewXform.MapUid is not { } mapUid)
            return;

        if (!_zLevels.TryMapOffset(mapUid, 1, out _, out var upperMapComp))
            return;

        // Ceiling check: any non-empty tile on the upper map straight above the viewer means we're indoors.
        var viewerPos = _transform.GetWorldPosition(viewXform);
        var aboveCoords = new MapCoordinates(viewerPos, upperMapComp.MapId);
        if (_mapSystem.TryFindGridAt(aboveCoords, out var upperGridUid, out var upperGridComp))
        {
            var tileRef = _mapSystem.GetTileRef(upperGridUid, upperGridComp, aboveCoords);
            if (!tileRef.Tile.IsEmpty)
                return;
        }

        EnsureStairPreviewViewport(viewport);
        if (_stairPreviewViewport is null)
            return;

        Angle rotation = fallbackEye.Rotation * -1;
        var offset = rotation.ToWorldVec() * CMUClientZLevelsSystem.ZLevelOffset;

        _zEye.LowestDepth = lowestDepth;
        _zEye.Depth = 1;
        _zEye.HighestDepth = 1;
        _zEye.BaseMapId = viewXform.MapID;
        _zEye.WeatherSourceMapId = weatherSourceMapId;
        _zEye.Position = new MapCoordinates(fallbackEye.Position.Position, upperMapComp.MapId);
        // Keep FOV on (matches the real look-up upper pass): without it the ghost leaked through walls
        // and the viewer's own field-of-view cone, which is a wall-hack.
        _zEye.DrawFov = fallbackEye.DrawFov;
        _zEye.DrawLight = fallbackEye.DrawLight;
        _zEye.Offset = fallbackEye.Offset + offset;
        _zEye.Rotation = fallbackEye.Rotation;
        _zEye.Scale = fallbackEye.Scale;
        _zEye.VisualZOffset = offset;
        _zEye.BlurCurrentLevel = false;
        _zEye.ConfigureVisibleEntityIndicators(false, _zOpeningBounds);

        _stairPreviewViewport.Eye = _zEye;
        _stairPreviewViewport.ClearColor = Color.Transparent;
        RenderZSpritePass(_stairPreviewViewport);

        _faintUpperAlpha = Math.Clamp(_cfg.GetCVar(CMUZLevelsCVars.FaintUpperAlpha), 0.05f, 0.80f);
        _drawFaintUpperComposite = true;
    }

    private void DrawStairPreviewComposite(DrawingHandleScreen screen, UIBox2 drawBox)
    {
        if (_stairPreviewViewport is null ||
            _stairPreviewViewport.Eye is null ||
            _stairPreviewEye.Position.MapId == MapId.Nullspace)
        {
            return;
        }

        screen.UseShader(GetStencilClearShader());
        screen.DrawRect(drawBox, Color.White);

        screen.UseShader(GetStencilMaskShader());
        DrawStairPreviewFovMask(screen, drawBox);

        screen.UseShader(GetStencilEqualDrawShader());
        screen.DrawTextureRect(_stairPreviewViewport.RenderTarget.Texture, drawBox);
        screen.DrawRect(drawBox, StairPreviewTint);

        screen.UseShader(GetStencilClearShader());
        screen.DrawRect(drawBox, Color.White);
        screen.UseShader(null);
    }

    private ShaderInstance GetStencilClearShader()
    {
        return _stencilClearShaderInstance ??= _prototypeManager.Index(StencilClearShader).Instance();
    }

    private ShaderInstance GetStencilMaskShader()
    {
        return _stencilMaskShaderInstance ??= _prototypeManager.Index(StencilMaskShader).Instance();
    }

    private ShaderInstance GetStencilEqualDrawShader()
    {
        return _stencilEqualDrawShaderInstance ??= _prototypeManager.Index(StencilEqualDrawShader).Instance();
    }

    private bool BuildStairPreviewMask()
    {
        _zRenderPlan.StairTiles.Clear();
        _stairPreviewTileBounds.Clear();
        if (_stairPreviewViewport is null ||
            _mapSystem is null ||
            _transform is null ||
            _lookup is null ||
            _examine is null ||
            _stairPreviewOrigins.Count == 0 ||
            !TryGetViewportWorldAabb(_stairPreviewViewport, out var worldAabb))
        {
            return false;
        }

        var mapId = _stairPreviewEye.Position.MapId;
        _stairPreviewGrids.Clear();
        _mapSystem.FindGridsIntersecting(mapId, worldAabb, ref _stairPreviewGrids, approx: true, includeMap: true);
        foreach (var grid in _stairPreviewGrids)
        {
            var gridMatrix = _transform.GetWorldMatrix(grid.Owner);
            foreach (var tile in _mapSystem.GetTilesIntersecting(grid.Owner, grid.Comp, worldAabb, ignoreEmpty: true))
            {
                if (_zRenderDiagnostics)
                    LastZRenderDebugStats.StairPreviewTilesExamined++;
                var localBounds = _lookup.GetLocalBounds(tile, grid.Comp.TileSize).Enlarged(0.01f);
                var visibleTile = new CMUZViewportRenderPlan.StairTile(
                    Vector2.Transform(localBounds.BottomLeft, gridMatrix),
                    Vector2.Transform(localBounds.TopLeft, gridMatrix),
                    Vector2.Transform(localBounds.TopRight, gridMatrix),
                    Vector2.Transform(localBounds.BottomRight, gridMatrix));
                var bounds = visibleTile.Bounds;
                if (!CanAnyStairPreviewOriginSeeTile(visibleTile, mapId, _stairPreviewEye.VisualZOffset))
                    continue;

                _zRenderPlan.StairTiles.Add(visibleTile);
                _stairPreviewTileBounds.Add(bounds);
            }
        }

        _stairPreviewGrids.Clear();
        _zRenderPlan.StairPreview.SetOpenings(mapId, worldAabb, _stairPreviewTileBounds, complete: true, dynamicOnly: false);
        _zRenderPlan.StairPreview.ConfirmVisible();
        if (_zRenderDiagnostics)
            LastZRenderDebugStats.StairPreviewTilesVisible = _zRenderPlan.StairTiles.Count;
        return _zRenderPlan.StairPreview.Visibility != CMUZVisibility.Hidden;
    }

    private void DrawStairPreviewFovMask(DrawingHandleScreen screen, UIBox2 drawBox)
    {
        var corners = _stairPreviewStencilCorners;
        foreach (var tile in _zRenderPlan.StairTiles)
        {
            corners[0] = CompositeWorldToScreen(tile.BottomLeft, drawBox);
            corners[1] = CompositeWorldToScreen(tile.TopLeft, drawBox);
            corners[2] = CompositeWorldToScreen(tile.TopRight, drawBox);
            corners[3] = CompositeWorldToScreen(tile.BottomRight, drawBox);
            screen.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, corners, Color.White);
        }
    }

    private void SetStairPreviewOrigins(CMUZLevelViewerComponent viewer, Vector2 viewerPosition)
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

            _stairPreviewOrigins.Add(new StairPreviewOrigin(position, viewerPosition));
        }
    }

    private bool CanAnyStairPreviewOriginSeeTile(
        CMUZViewportRenderPlan.StairTile tile,
        MapId mapId,
        Vector2 renderOffset)
    {
        if (_examine is null)
            return false;

        var target = new MapCoordinates(tile.Bounds.Center, mapId);
        foreach (var origin in _stairPreviewOrigins)
        {
            if (!CMUZLevelStairPreviewVisibility.IsInFrontOfStair(
                    origin.ViewerPosition,
                    origin.Position,
                    target.Position - renderOffset))
            {
                continue;
            }

            if (!CMUZLevelStairPreviewVisibility.ProjectedCornersStayInFrontOfStair(
                    origin.ViewerPosition,
                    origin.Position,
                    tile.BottomLeft,
                    tile.TopLeft,
                    tile.TopRight,
                    tile.BottomRight,
                    renderOffset))
            {
                continue;
            }

            var originCoordinates = new MapCoordinates(origin.Position, mapId);
            if (_zRenderDiagnostics)
                LastZRenderDebugStats.StairPreviewLosChecks++;
            if (_examine.InRangeUnOccluded(originCoordinates, target, 0f, null))
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
        _drawFaintUpperComposite = false; // AU14: faint upper ghost is re-decided every frame
    }

    internal void NoteZRenderBypassed(string reason)
    {
        _zRenderDiagnostics = _cfg.GetCVar(CMUZLevelsCVars.ClientDiagnosticsEnabled);
        if (_zRenderDiagnostics)
        {
            LastZRenderDebugStats.Reset();
            LastZRenderDebugStats.SkipReason = reason;
            LastZRenderDebugStats.BasePassRendered = true;
        }
    }

    private void DisposeZLevelViewports()
    {
        _stairPreviewViewport?.Dispose();
        _stairPreviewViewport = null;
        _zRenderPlan.Reset();
        _stairPreviewTileBounds.Clear();
        ClearZLevelCompositeState();
    }

    private readonly record struct StairPreviewOrigin(Vector2 Position, Vector2 ViewerPosition);

    internal sealed class ZLevelRenderDebugStats
    {
        public int Sequence;
        public bool UsedZRender;
        public bool BasePassRendered;
        public string SkipReason = "not rendered yet";
        public MapId BaseMapId = MapId.Nullspace;
        public EntityUid? BaseMapUid;
        public Box2 ViewportWorldAabb;
        public float ViewportWorldArea;
        public Vector2 ZRenderOffsetPerDepth;
        public int MaxDepth;
        public int LookUpDepth;
        public int LowestDepth;
        public bool ViewerLookUp;
        public bool StairPreviewUp;
        public bool OpeningQueryRan;
        public bool OpeningQueryFoundOpening;
        public bool OpeningQueryConservativeNoBounds;
        public bool OpeningBoundsTruncated;
        public int OpeningsBeforeLos;
        public int OpeningLosChecks;
        public int OpeningsAfterLos;
        public int OpeningsRemovedByLos;
        public bool OpeningLosConservativeFallback;
        public string OpeningLosMode = "none";
        public float OpeningAreaBeforeLos;
        public float OpeningAreaAfterLos;
        public bool VisibleCurrentOpenings;
        public bool HasLowerMap;
        public bool LowerSuppressedByOpeningGate;
        public bool LowerRenderGraceActive;
        public int LowerRenderGraceLowestDepth;
        public float LowerRenderGraceSeconds;
        public double LowerRenderGraceRemainingMs;
        public int LowerDepthsChecked;
        public int LowerDepthsWithMaps;
        public int LowerDepthBreakDepth;
        public int LowerPassesRendered;
        public int UpperPassesRendered;
        public int StairPreviewCompositesRendered;
        public int StairPreviewTilesExamined;
        public int StairPreviewTilesVisible;
        public int StairPreviewLosChecks;
        public int SpriteCullCandidates;
        public int SpritesCulled;
        public double TotalRenderMs;
        public double OpeningQueryTotalMs;
        public double CurrentOpeningQueryMs;
        public double OpeningLosMs;
        public double LowerDepthDiscoveryMs;
        public double LowerDepthOpeningQueryMs;
        public int LowerDepthOpeningLosChecks;
        public double LowerRenderMs;
        public double BaseRenderMs;
        public double UpperRenderMs;
        public double StairPreviewRenderMs;
        public readonly List<int> LowerRenderedDepths = new();

        public void Reset()
        {
            Sequence++;
            UsedZRender = false;
            BasePassRendered = false;
            SkipReason = "not rendered yet";
            BaseMapId = MapId.Nullspace;
            BaseMapUid = null;
            ViewportWorldAabb = default;
            ViewportWorldArea = 0f;
            ZRenderOffsetPerDepth = Vector2.Zero;
            MaxDepth = 0;
            LookUpDepth = 0;
            LowestDepth = 0;
            ViewerLookUp = false;
            StairPreviewUp = false;
            OpeningQueryRan = false;
            OpeningQueryFoundOpening = false;
            OpeningQueryConservativeNoBounds = false;
            OpeningBoundsTruncated = false;
            OpeningsBeforeLos = 0;
            OpeningLosChecks = 0;
            OpeningsAfterLos = 0;
            OpeningsRemovedByLos = 0;
            OpeningLosConservativeFallback = false;
            OpeningLosMode = "none";
            OpeningAreaBeforeLos = 0f;
            OpeningAreaAfterLos = 0f;
            VisibleCurrentOpenings = false;
            HasLowerMap = false;
            LowerSuppressedByOpeningGate = false;
            LowerRenderGraceActive = false;
            LowerRenderGraceLowestDepth = 0;
            LowerRenderGraceSeconds = 0f;
            LowerRenderGraceRemainingMs = 0d;
            LowerDepthsChecked = 0;
            LowerDepthsWithMaps = 0;
            LowerDepthBreakDepth = 0;
            LowerPassesRendered = 0;
            UpperPassesRendered = 0;
            StairPreviewCompositesRendered = 0;
            StairPreviewTilesExamined = 0;
            StairPreviewTilesVisible = 0;
            StairPreviewLosChecks = 0;
            SpriteCullCandidates = 0;
            SpritesCulled = 0;
            TotalRenderMs = 0d;
            OpeningQueryTotalMs = 0d;
            CurrentOpeningQueryMs = 0d;
            OpeningLosMs = 0d;
            LowerDepthDiscoveryMs = 0d;
            LowerDepthOpeningQueryMs = 0d;
            LowerDepthOpeningLosChecks = 0;
            LowerRenderMs = 0d;
            BaseRenderMs = 0d;
            UpperRenderMs = 0d;
            StairPreviewRenderMs = 0d;
            LowerRenderedDepths.Clear();
        }
    }

    public sealed class ZEye : Robust.Shared.Graphics.Eye
    {
        private readonly List<Box2> _visibleEntityIndicatorBounds = new();

        public int LowestDepth;
        public int Depth;
        public int HighestDepth;
        public MapId BaseMapId;
        public MapId WeatherSourceMapId;
        public Vector2 VisualZOffset;
        public bool BlurCurrentLevel;

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
