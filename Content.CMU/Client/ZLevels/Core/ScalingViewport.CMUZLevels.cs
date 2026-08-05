using System.Numerics;
using System.Collections.Generic;
using System.Diagnostics;
using Content.Client.Examine;
using Content.Shared._CMU14.ZLevels;
using Content.Client._CMU14.ZLevels.Core;
using Content.Shared._CMU14.ZLevels.Core;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
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
    // Entries exist only during synchronous viewport rendering and are removed in a finally block.
    private static readonly HashSet<long> ActiveZLevelViewportIds = new();

    [Dependency] private ITileDefinitionManager _tile = default!;
    [Dependency] private ProfManager _prof = default!;
    [Dependency] private Robust.Shared.Timing.IGameTiming _timing = default!;

    private static readonly ProtoId<ShaderPrototype> StencilClearShader = "StencilClear";
    private static readonly ProtoId<ShaderPrototype> StencilMaskShader = "StencilMask";
    private static readonly ProtoId<ShaderPrototype> StencilEqualDrawShader = "StencilEqualDraw";
    private static readonly Color StairPreviewTint = new(0.05f, 0.05f, 0.05f, 0.48f);
    private const int MaxOpeningLosChecks = 32;

    private CMUClientZLevelsSystem? _zLevels;
    private SharedMapSystem? _mapSystem;
    private SharedTransformSystem? _transform;
    private EntityLookupSystem? _lookup;
    private ExamineSystem? _examine;
    private OccluderSystem? _occluder;
    private SharedContainerSystem? _containers;
    private SpriteSystem? _sprite;
    private ShaderInstance? _stencilClearShaderInstance;
    private ShaderInstance? _stencilMaskShaderInstance;
    private ShaderInstance? _stencilEqualDrawShaderInstance;

    private EntityQuery<TransformComponent>? _xformQuery;

    private List<Entity<MapGridComponent>> _zLevelGrids = new();
    private List<Entity<MapGridComponent>> _stairPreviewGrids = new();
    private readonly List<StairPreviewOrigin> _stairPreviewOrigins = new(CMUZLevelViewerComponent.MaxStairPreviewPositions);
    private readonly HashSet<Entity<SpriteComponent>> _stairPreviewSpriteCandidates = new();
    private readonly Dictionary<EntityUid, bool> _stairPreviewHiddenSpriteVisibility = new();
    private readonly List<Box2> _zOpeningBounds = new();
    private readonly List<Box2> _zLowerChainBounds = new();
    private readonly List<Box2> _zLowerOpeningBounds = new();
    private readonly List<Box2> _zLowerSearchBounds = new();
    private readonly List<int> _checkedOpeningIndices = new(MaxOpeningLosChecks);
    private readonly List<int> _zRenderedLowerDepths = new();
    private readonly ZLevelRenderVisibilityState _zLevelRenderVisibility = new();
    private readonly ZEye _zEye = new();
    private readonly ZEye _stairPreviewEye = new();
    private readonly LowerRenderGraceState _zLowerRenderGrace = new();
    private IClydeViewport? _stairPreviewViewport;
    private bool _drawStairPreviewComposite;
    private bool _drawFaintUpperComposite;
    private float _faintUpperAlpha;
    private EntityUid? _lastZLevelEyeEntity;
    private EntityUid? _lastZLevelViewEntity;

    internal static ZLevelRenderDebugStats LastZRenderDebugStats { get; } = new();

    private SpriteSystem Sprite => _sprite ??= _entityManager.System<SpriteSystem>();

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

    private void RenderZLevelPasses(IClydeViewport viewport)
    {
        var controlEye = _eye;
        var viewportEye = viewport.Eye;
        var clearColor = viewport.ClearColor;
        var clearWhenMissingEye = viewport.ClearWhenMissingEye;
        SetZLevelRenderingActive(viewport, false);
        InvalidateZRenderVisibility();

        try
        {
            RenderZLevelPassesCore(viewport);
        }
        finally
        {
            Eye = controlEye;
            viewport.Eye = viewportEye;
            viewport.ClearColor = clearColor;
            viewport.ClearWhenMissingEye = clearWhenMissingEye;
            SetZLevelRenderingActive(viewport, false);
        }
    }

    private void RenderZLevelPassesCore(IClydeViewport viewport)
    {
        ClearZLevelCompositeState();
        LastZRenderDebugStats.Reset();
        var totalStart = Stopwatch.GetTimestamp();

        var zLevelsEnabled = _cfg.GetCVar(CMUZLevelsCVars.Enabled);
        var renderEnabled = _cfg.GetCVar(CMUZLevelsCVars.RenderEnabled);

        if (_eye is null ||
            !ShouldUseZLevelRenderPasses(
                zLevelsEnabled,
                renderEnabled))
        {
            LastZRenderDebugStats.SkipReason = _eye is null
                ? "no viewport eye"
                : !zLevelsEnabled
                    ? "cmu.zlevels.enabled=false"
                    : !renderEnabled
                        ? "cmu.zlevels.render_enabled=false"
                        : "z render disabled";
            _zLowerRenderGrace.Reset();
            var renderStart = Stopwatch.GetTimestamp();
            RenderFreshBasePass(viewport);
            LastZRenderDebugStats.BasePassRendered = true;
            LastZRenderDebugStats.BaseRenderMs = GetElapsedMilliseconds(renderStart);
            LastZRenderDebugStats.TotalRenderMs = GetElapsedMilliseconds(totalStart);
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
        _occluder ??= _entityManager.System<OccluderSystem>();
        _containers ??= _entityManager.System<SharedContainerSystem>();

        if (!TryGetZLevelViewEntity(fallbackEye, out var viewEntity, out var zLevelViewer, out var viewXform) ||
            viewXform.MapUid is null)
        {
            LastZRenderDebugStats.SkipReason = "no Z-level viewer for current eye";
            _zLowerRenderGrace.Reset();
            var renderStart = Stopwatch.GetTimestamp();
            RenderFreshBasePass(viewport);
            LastZRenderDebugStats.BasePassRendered = true;
            LastZRenderDebugStats.BaseRenderMs = GetElapsedMilliseconds(renderStart);
            LastZRenderDebugStats.TotalRenderMs = GetElapsedMilliseconds(totalStart);
            return;
        }

        var lookUp = zLevelViewer.LookUp || zLevelViewer.StairPreviewUp ? 1 : 0;
        var maxDepth = Math.Clamp(
            _cfg.GetCVar(CMUZLevelsCVars.MaxRenderDepth),
            0,
            CMUSharedZLevelsSystem.MaxZLevelTraversalDepth);
        var maxOpeningRects = Math.Max(0, _cfg.GetCVar(CMUZLevelsCVars.MaxOpeningRectsPerPass));
        var lowestDepth = 0;
        var weatherSourceMapId = GetWeatherSourceMapId(viewXform.MapUid.Value, viewXform.MapID);
        if (!TryGetViewportWorldAabb(viewport, out var viewportWorldAabb))
        {
            LastZRenderDebugStats.SkipReason = "no viewport world bounds";
            _zLowerRenderGrace.Reset();
            var renderStart = Stopwatch.GetTimestamp();
            RenderFreshBasePass(viewport);
            LastZRenderDebugStats.BasePassRendered = true;
            LastZRenderDebugStats.BaseRenderMs = GetElapsedMilliseconds(renderStart);
            LastZRenderDebugStats.TotalRenderMs = GetElapsedMilliseconds(totalStart);
            return;
        }

        SetZLevelRenderingActive(viewport, true);
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
        var zRenderRotation = -fallbackEye.Rotation;
        LastZRenderDebugStats.ZRenderOffsetPerDepth =
            zRenderRotation.ToWorldVec() * CMUClientZLevelsSystem.ZLevelOffset;
        var renderIdentity = new ZLevelViewIdentity(
            fallbackEye,
            viewEntity,
            viewXform.MapUid.Value,
            _entityManager.TryGetComponent<CMUZLevelMapComponent>(viewXform.MapUid.Value, out var zMap)
                ? zMap.NetworkUid
                : EntityUid.Invalid);

        _zOpeningBounds.Clear();
        using (var openingProfile = _prof.Group("CMU Z Opening Query"))
        {
            var openingStart = Stopwatch.GetTimestamp();
            var currentOpeningStart = Stopwatch.GetTimestamp();
            var hasOpenings = TryFindEmptyTilesInAabb(
                viewXform.MapUid.Value,
                viewportWorldAabb,
                _zOpeningBounds,
                out _,
                maxOpeningRects == 0 ? int.MaxValue : maxOpeningRects + 1,
                true);
            LastZRenderDebugStats.CurrentOpeningQueryMs = GetElapsedMilliseconds(currentOpeningStart);

            LastZRenderDebugStats.OpeningQueryRan = true;
            LastZRenderDebugStats.OpeningQueryFoundOpening = hasOpenings;
            LastZRenderDebugStats.OpeningsBeforeLos = _zOpeningBounds.Count;
            LastZRenderDebugStats.OpeningBoundsTruncated = maxOpeningRects > 0 && _zOpeningBounds.Count > maxOpeningRects;
            LastZRenderDebugStats.OpeningQueryConservativeNoBounds = hasOpenings && _zOpeningBounds.Count == 0;
            LastZRenderDebugStats.OpeningAreaBeforeLos = GetAreaSum(_zOpeningBounds);

            if (hasOpenings)
            {
                var beforeLos = _zOpeningBounds.Count;
                var losStart = Stopwatch.GetTimestamp();
                hasOpenings = FilterVisibleOpeningBounds(
                    viewXform.MapID,
                    _transform.GetWorldPosition(viewXform),
                    _zOpeningBounds,
                    maxOpeningRects,
                    out var losChecks,
                    out var conservativeLos,
                    out var losMode);

                LastZRenderDebugStats.OpeningLosChecks = losChecks;
                LastZRenderDebugStats.OpeningLosMs = GetElapsedMilliseconds(losStart);
                LastZRenderDebugStats.OpeningsAfterLos = _zOpeningBounds.Count;
                LastZRenderDebugStats.OpeningsRemovedByLos = Math.Max(0, beforeLos - _zOpeningBounds.Count);
                LastZRenderDebugStats.OpeningLosConservativeFallback = conservativeLos;
                LastZRenderDebugStats.OpeningLosMode = losMode;
                LastZRenderDebugStats.OpeningAreaAfterLos = GetAreaSum(_zOpeningBounds);
            }

            LastZRenderDebugStats.VisibleCurrentOpenings = hasOpenings;
            var hasLowerMap = _zLevels.TryMapOffset(viewXform.MapUid.Value, -1, out _);
            LastZRenderDebugStats.HasLowerMap = hasLowerMap;

            var lowerDiscoveryStart = Stopwatch.GetTimestamp();
            if (hasOpenings &&
                maxDepth > 0 &&
                hasLowerMap)
            {
                _zLowerChainBounds.Clear();
                _zLowerChainBounds.AddRange(_zOpeningBounds);

                var lowerStepOffset = -LastZRenderDebugStats.ZRenderOffsetPerDepth;
                for (var i = -1; i >= -maxDepth; i--)
                {
                    LastZRenderDebugStats.LowerDepthsChecked++;
                    if (!_zLevels.TryMapOffset(viewXform.MapUid.Value, i, out var mapUidBelow, out var lowerMapComp))
                        continue;

                    lowestDepth = i;
                    LastZRenderDebugStats.LowerDepthsWithMaps++;

                    var lowerOpeningStart = Stopwatch.GetTimestamp();
                    var hasDeeperOpening = TryFindChainedLowerOpeningBounds(
                        mapUidBelow.Value,
                        lowerMapComp.MapId,
                        viewportWorldAabb,
                        lowerStepOffset,
                        maxOpeningRects,
                        out var losChecks);
                    LastZRenderDebugStats.LowerDepthOpeningQueryMs += GetElapsedMilliseconds(lowerOpeningStart);
                    LastZRenderDebugStats.LowerDepthOpeningLosChecks += losChecks;

                    if (!hasDeeperOpening)
                    {
                        LastZRenderDebugStats.LowerDepthBreakDepth = i;
                        break;
                    }
                }
            }
            LastZRenderDebugStats.LowerDepthDiscoveryMs = GetElapsedMilliseconds(lowerDiscoveryStart);

            ApplyLowerRenderGrace(renderIdentity, maxDepth, hasLowerMap, ref lowestDepth);

            LastZRenderDebugStats.LowerSuppressedByOpeningGate = maxDepth > 0 &&
                hasLowerMap &&
                lowestDepth == 0 &&
                !hasOpenings;
            LastZRenderDebugStats.OpeningQueryTotalMs = GetElapsedMilliseconds(openingStart);
        }

        LastZRenderDebugStats.LowestDepth = lowestDepth;
        _zRenderedLowerDepths.Clear();

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
                        var stairPreviewStart = Stopwatch.GetTimestamp();
                        RenderStairPreviewComposite(viewport, _zEye);
                        LastZRenderDebugStats.StairPreviewCompositesRendered++;
                        LastZRenderDebugStats.StairPreviewRenderMs += GetElapsedMilliseconds(stairPreviewStart);
                        continue;
                    }

                    viewport.Eye = _zEye;
                }

                viewport.ClearColor = depth == lowestDepth ? Color.Black : null;
                var renderStart = Stopwatch.GetTimestamp();
                viewport.Render();
                var renderMs = GetElapsedMilliseconds(renderStart);

                if (depth < 0)
                {
                    LastZRenderDebugStats.LowerPassesRendered++;
                    LastZRenderDebugStats.LowerRenderMs += renderMs;
                    LastZRenderDebugStats.LowerRenderedDepths.Add(depth);
                    _zRenderedLowerDepths.Add(depth);
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

        PublishZRenderVisibility(
            renderIdentity,
            viewXform.MapID,
            _zRenderedLowerDepths);

        // The first stage of CMU's look-up cycle ghosts the unobstructed level
        // above the viewer before switching to the full look-up view.
        if (lookUp == 0 && zLevelViewer.FaintUp)
            RenderFaintUpperComposite(viewport, fallbackEye, viewXform, lowestDepth, weatherSourceMapId);

        LastZRenderDebugStats.TotalRenderMs = GetElapsedMilliseconds(totalStart);
    }

    private void RenderWithoutZLevels(IClydeViewport viewport, string reason)
    {
        SetZLevelRenderingActive(viewport, false);
        InvalidateZRenderVisibility();
        _zLowerRenderGrace.Reset();
        NoteZRenderBypassed(reason);
        ClearZLevelCompositeState();
        viewport.Render();
    }

    private void RenderFreshBasePass(IClydeViewport viewport)
    {
        viewport.Eye = _eye ?? viewport.Eye;
        viewport.ClearColor = Color.Black;
        viewport.ClearWhenMissingEye = true;
        viewport.Render();
    }

    internal static bool ShouldUseZLevelRenderPasses(bool zLevelsEnabled, bool renderEnabled)
    {
        return zLevelsEnabled &&
               renderEnabled;
    }

    internal static bool IsZLevelCompositionActive(IClydeViewport viewport)
    {
        return ActiveZLevelViewportIds.Contains(viewport.Id);
    }

    private void SetZLevelRenderingActive(IClydeViewport viewport, bool active)
    {
        if (active)
            ActiveZLevelViewportIds.Add(viewport.Id);
        else
            ActiveZLevelViewportIds.Remove(viewport.Id);
    }

    private void InvalidateZRenderVisibility()
    {
        _zLevelRenderVisibility.Invalidate();
    }

    private void PublishZRenderVisibility(
        ZLevelViewIdentity identity,
        MapId baseMapId,
        IReadOnlyList<int> renderedLowerDepths)
    {
        _zLevelRenderVisibility.Publish(identity, baseMapId, renderedLowerDepths);
    }

    internal bool TryCopyRenderedLowerDepths(
        IEye eye,
        EntityUid viewEntity,
        EntityUid baseMapUid,
        MapId baseMapId,
        EntityUid networkUid,
        List<int> destination)
    {
        destination.Clear();

        return _viewport is not null &&
               _zLevelRenderVisibility.TryCopyRenderedLowerDepths(
                   new ZLevelViewIdentity(eye, viewEntity, baseMapUid, networkUid),
                   baseMapId,
                   destination);
    }

    private void ApplyLowerRenderGrace(
        ZLevelViewIdentity identity,
        int maxDepth,
        bool hasLowerMap,
        ref int lowestDepth)
    {
        var graceSeconds = Math.Max(0f, _cfg.GetCVar(CMUZLevelsCVars.LowerRenderVisibilityGrace));
        LastZRenderDebugStats.LowerRenderGraceSeconds = graceSeconds;
        var discoveredLowestDepth = lowestDepth;
        lowestDepth = _zLowerRenderGrace.Resolve(
            identity,
            _timing.CurTime,
            TimeSpan.FromSeconds(graceSeconds),
            maxDepth,
            hasLowerMap,
            discoveredLowestDepth,
            out var active);

        if (discoveredLowestDepth < 0)
        {
            LastZRenderDebugStats.LowerRenderGraceLowestDepth = lowestDepth;
            LastZRenderDebugStats.LowerRenderGraceRemainingMs = graceSeconds * 1000d;
            return;
        }

        if (active)
        {
            LastZRenderDebugStats.LowerRenderGraceActive = true;
            LastZRenderDebugStats.LowerRenderGraceLowestDepth = lowestDepth;
            LastZRenderDebugStats.LowerRenderGraceRemainingMs = Math.Max(
                0d,
                (_zLowerRenderGrace.Until - _timing.CurTime).TotalMilliseconds);
        }
    }

    private bool TryFindChainedLowerOpeningBounds(
        EntityUid mapUid,
        MapId mapId,
        Box2 viewportWorldAabb,
        Vector2 lowerStepOffset,
        int maxOpeningRects,
        out int losChecks)
    {
        losChecks = 0;
        _zLowerSearchBounds.Clear();

        if (_zLowerChainBounds.Count == 0)
        {
            _zLowerSearchBounds.Add(viewportWorldAabb.Translated(lowerStepOffset));
        }
        else
        {
            foreach (var openingBound in _zLowerChainBounds)
            {
                AddMergedOpeningSearchBounds(
                    _zLowerSearchBounds,
                    openingBound.Translated(lowerStepOffset));
            }
        }

        _zLowerOpeningBounds.Clear();
        var foundOpening = false;
        var openingLimit = maxOpeningRects == 0
            ? int.MaxValue
            : maxOpeningRects + 1;

        foreach (var searchBounds in _zLowerSearchBounds)
        {
            var beforeCount = _zLowerOpeningBounds.Count;
            var foundInBounds = TryFindEmptyTilesInAabb(
                mapUid,
                searchBounds,
                _zLowerOpeningBounds,
                out _,
                openingLimit,
                true);

            if (!foundInBounds)
                continue;

            foundOpening = true;

            if (_zLevelGrids.Count == 0 && _zLowerOpeningBounds.Count == beforeCount)
                _zLowerOpeningBounds.Add(searchBounds);

            if (_zLowerOpeningBounds.Count >= openingLimit)
                break;
        }

        if (!foundOpening)
            return false;

        var hasVisibleOpening = FilterVisibleChainedLowerOpeningBounds(
            mapId,
            maxOpeningRects,
            out losChecks);

        _zLowerChainBounds.Clear();
        _zLowerChainBounds.AddRange(_zLowerOpeningBounds);

        return hasVisibleOpening;
    }

    private bool FilterVisibleChainedLowerOpeningBounds(
        MapId mapId,
        int maxOpeningRects,
        out int losChecks)
    {
        losChecks = 0;

        if (_examine is null)
            return true;

        if (_zLowerOpeningBounds.Count == 0)
            return true;

        if (maxOpeningRects > 0 && _zLowerOpeningBounds.Count > maxOpeningRects)
            return true;

        if (_zLowerOpeningBounds.Count > MaxOpeningLosChecks)
        {
            if (HasSampledVisibleChainedLowerOpeningBounds(mapId, ref losChecks))
                return true;

            _zLowerOpeningBounds.Clear();
            return false;
        }

        for (var i = _zLowerOpeningBounds.Count - 1; i >= 0; i--)
        {
            if (CanAnyLowerSearchBoundSeeOpening(mapId, _zLowerOpeningBounds[i], ref losChecks))
                continue;

            _zLowerOpeningBounds.RemoveAt(i);
        }

        return _zLowerOpeningBounds.Count > 0;
    }

    private bool HasSampledVisibleChainedLowerOpeningBounds(MapId mapId, ref int losChecks)
    {
        _checkedOpeningIndices.Clear();

        var checks = Math.Min(MaxOpeningLosChecks, _zLowerOpeningBounds.Count);
        for (var i = 0; i < checks; i++)
        {
            var targetIndex = checks == 1
                ? _zLowerOpeningBounds.Count / 2
                : (int) MathF.Round(i * (_zLowerOpeningBounds.Count - 1) / (float) (checks - 1));
            var index = FindUncheckedOpeningAround(targetIndex, _zLowerOpeningBounds.Count, _checkedOpeningIndices);
            if (index < 0)
                break;

            _checkedOpeningIndices.Add(index);
            if (CanAnyLowerSearchBoundSeeOpening(mapId, _zLowerOpeningBounds[index], ref losChecks))
                return true;
        }

        return false;
    }

    private bool CanAnyLowerSearchBoundSeeOpening(
        MapId mapId,
        Box2 openingBounds,
        ref int losChecks)
    {
        foreach (var searchBounds in _zLowerSearchBounds)
        {
            if (!BoundsOverlapOrTouch(searchBounds, openingBounds))
                continue;

            if (CanSeeOpeningBounds(new MapCoordinates(searchBounds.Center, mapId), mapId, openingBounds, ref losChecks))
                return true;
        }

        return false;
    }

    private static void AddMergedOpeningSearchBounds(List<Box2> searchBounds, Box2 bounds)
    {
        for (var i = 0; i < searchBounds.Count; i++)
        {
            if (!BoundsOverlapOrTouch(searchBounds[i], bounds))
                continue;

            searchBounds[i] = searchBounds[i].Union(bounds);
            MergeOpeningSearchBounds(searchBounds, i);
            return;
        }

        searchBounds.Add(bounds);
    }

    private static void MergeOpeningSearchBounds(List<Box2> searchBounds, int index)
    {
        for (var i = searchBounds.Count - 1; i >= 0; i--)
        {
            if (i == index ||
                !BoundsOverlapOrTouch(searchBounds[index], searchBounds[i]))
            {
                continue;
            }

            searchBounds[index] = searchBounds[index].Union(searchBounds[i]);
            searchBounds.RemoveAt(i);
            if (i < index)
                index--;
        }
    }

    private static bool BoundsOverlapOrTouch(Box2 a, Box2 b)
    {
        return a.BottomLeft.X <= b.TopRight.X &&
               a.TopRight.X >= b.BottomLeft.X &&
               a.BottomLeft.Y <= b.TopRight.Y &&
               a.TopRight.Y >= b.BottomLeft.Y;
    }

    private bool FilterVisibleOpeningBounds(
        MapId mapId,
        Vector2 viewerPosition,
        List<Box2> openingBounds,
        int maxOpeningRects,
        out int losChecks,
        out bool conservativeFallback,
        out string losMode)
    {
        losChecks = 0;
        conservativeFallback = false;
        losMode = "exhaustive";

        if (_examine is null)
        {
            conservativeFallback = true;
            losMode = "no examine system";
            return true;
        }

        // If there were no grids in the queried area, the opening cache conservatively reports open space without
        // concrete bounds. Keep the old behavior for that case.
        if (openingBounds.Count == 0)
        {
            conservativeFallback = true;
            losMode = "no bounds";
            return true;
        }

        // A truncated opening list is incomplete, so keep the old conservative behavior rather than hiding a valid
        // lower view. Large complete lists are handled with bounded sampling below.
        if (maxOpeningRects > 0 && openingBounds.Count > maxOpeningRects)
        {
            conservativeFallback = true;
            losMode = "truncated";
            return true;
        }

        if (openingBounds.Count > MaxOpeningLosChecks)
        {
            losMode = "sampled";
            if (HasSampledVisibleOpeningBounds(mapId, viewerPosition, openingBounds, ref losChecks))
                return true;

            openingBounds.Clear();
            return false;
        }

        var origin = new MapCoordinates(viewerPosition, mapId);
        for (var i = openingBounds.Count - 1; i >= 0; i--)
        {
            if (CanSeeOpeningBounds(origin, mapId, openingBounds[i], ref losChecks))
                continue;

            openingBounds.RemoveAt(i);
        }

        return openingBounds.Count > 0;
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

    private bool HasSampledVisibleOpeningBounds(
        MapId mapId,
        Vector2 viewerPosition,
        List<Box2> openingBounds,
        ref int losChecks)
    {
        var origin = new MapCoordinates(viewerPosition, mapId);
        _checkedOpeningIndices.Clear();

        var nearestChecks = Math.Min(MaxOpeningLosChecks / 2, openingBounds.Count);
        for (var i = 0; i < nearestChecks; i++)
        {
            var index = FindNearestUncheckedOpening(openingBounds, viewerPosition, _checkedOpeningIndices);
            if (index < 0)
                break;

            _checkedOpeningIndices.Add(index);
            if (CanSeeOpeningBounds(origin, mapId, openingBounds[index], ref losChecks))
                return true;
        }

        var spreadChecks = Math.Min(
            MaxOpeningLosChecks - _checkedOpeningIndices.Count,
            openingBounds.Count - _checkedOpeningIndices.Count);
        for (var i = 0; i < spreadChecks && _checkedOpeningIndices.Count < MaxOpeningLosChecks; i++)
        {
            var targetIndex = spreadChecks == 1
                ? openingBounds.Count / 2
                : (int) MathF.Round(i * (openingBounds.Count - 1) / (float) (spreadChecks - 1));
            var index = FindUncheckedOpeningAround(targetIndex, openingBounds.Count, _checkedOpeningIndices);
            if (index < 0)
                break;

            _checkedOpeningIndices.Add(index);
            if (CanSeeOpeningBounds(origin, mapId, openingBounds[index], ref losChecks))
                return true;
        }

        return false;
    }

    private bool CanSeeOpeningBounds(
        MapCoordinates origin,
        MapId mapId,
        Box2 openingBounds,
        ref int losChecks)
    {
        var center = openingBounds.Center;
        if (CanSeeOpeningPoint(origin, mapId, center, ref losChecks))
            return true;

        var closest = openingBounds.ClosestPoint(origin.Position);
        if (CanSeeOpeningPoint(origin, mapId, InsetOpeningPoint(closest, center), ref losChecks))
            return true;

        return CanSeeOpeningPoint(origin, mapId, InsetOpeningPoint(openingBounds.BottomLeft, center), ref losChecks) ||
               CanSeeOpeningPoint(origin, mapId, InsetOpeningPoint(openingBounds.TopLeft, center), ref losChecks) ||
               CanSeeOpeningPoint(origin, mapId, InsetOpeningPoint(openingBounds.TopRight, center), ref losChecks) ||
               CanSeeOpeningPoint(origin, mapId, InsetOpeningPoint(openingBounds.BottomRight, center), ref losChecks);
    }

    private bool CanSeeOpeningPoint(
        MapCoordinates origin,
        MapId mapId,
        Vector2 targetPosition,
        ref int losChecks)
    {
        losChecks++;
        return _examine!.InRangeUnOccluded(origin, new MapCoordinates(targetPosition, mapId), 0f, null);
    }

    private static Vector2 InsetOpeningPoint(Vector2 point, Vector2 center)
    {
        return point + (center - point) * 0.15f;
    }

    private static int FindNearestUncheckedOpening(
        List<Box2> openingBounds,
        Vector2 viewerPosition,
        List<int> checkedIndices)
    {
        var bestIndex = -1;
        var bestDistance = float.PositiveInfinity;
        for (var i = 0; i < openingBounds.Count; i++)
        {
            if (HasCheckedOpeningIndex(checkedIndices, i))
                continue;

            var distance = Vector2.DistanceSquared(viewerPosition, openingBounds[i].Center);
            if (distance >= bestDistance)
                continue;

            bestIndex = i;
            bestDistance = distance;
        }

        return bestIndex;
    }

    private static int FindUncheckedOpeningAround(
        int targetIndex,
        int openingCount,
        List<int> checkedIndices)
    {
        if (!HasCheckedOpeningIndex(checkedIndices, targetIndex))
            return targetIndex;

        for (var offset = 1; offset < openingCount; offset++)
        {
            var lower = targetIndex - offset;
            if (lower >= 0 && !HasCheckedOpeningIndex(checkedIndices, lower))
                return lower;

            var upper = targetIndex + offset;
            if (upper < openingCount && !HasCheckedOpeningIndex(checkedIndices, upper))
                return upper;
        }

        return -1;
    }

    private static bool HasCheckedOpeningIndex(
        List<int> checkedIndices,
        int index)
    {
        for (var i = 0; i < checkedIndices.Count; i++)
        {
            if (checkedIndices[i] == index)
                return true;
        }

        return false;
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

    private void RenderStairPreviewComposite(IClydeViewport sourceViewport, ZEye sourceEye)
    {
        EnsureStairPreviewViewport(sourceViewport);
        if (_stairPreviewViewport is null)
            return;

        CopyZEye(_stairPreviewEye, sourceEye);
        _stairPreviewEye.DrawFov = false;
        _stairPreviewEye.ConfigureVisibleEntityIndicators(false, _zOpeningBounds);

        var viewportEye = _stairPreviewViewport.Eye;
        var clearColor = _stairPreviewViewport.ClearColor;
        try
        {
            _stairPreviewViewport.Eye = _stairPreviewEye;
            _stairPreviewViewport.ClearColor = Color.Transparent;
            var cullStart = Stopwatch.GetTimestamp();
            CullStairPreviewSprites(_stairPreviewEye.Position.MapId);
            LastZRenderDebugStats.StairPreviewCullMs += GetElapsedMilliseconds(cullStart);
            _stairPreviewViewport.Render();
        }
        finally
        {
            RestoreStairPreviewSprites();
            _stairPreviewViewport.Eye = viewportEye;
            _stairPreviewViewport.ClearColor = clearColor;
        }

        _drawStairPreviewComposite = true;
    }

    private void CullStairPreviewSprites(MapId mapId)
    {
        if (_stairPreviewViewport is null ||
            _lookup is null ||
            _transform is null ||
            _xformQuery is not { } xformQuery ||
            !TryGetViewportWorldAabb(_stairPreviewViewport, out var worldAabb))
        {
            return;
        }

        _stairPreviewSpriteCandidates.Clear();
        _lookup.GetEntitiesIntersecting(mapId, worldAabb, _stairPreviewSpriteCandidates, LookupFlags.All);
        LastZRenderDebugStats.StairPreviewSpriteCandidates += _stairPreviewSpriteCandidates.Count;

        foreach (var candidate in _stairPreviewSpriteCandidates)
        {
            var uid = candidate.Owner;
            var sprite = candidate.Comp;
            if (!sprite.Visible ||
                !xformQuery.TryComp(uid, out var xform) ||
                xform.MapID != mapId)
            {
                continue;
            }

            LastZRenderDebugStats.StairPreviewSpriteVisibilityChecks++;
            var worldBounds = GetStairPreviewSpriteBounds(uid, sprite, xform, xformQuery);
            var target = new MapCoordinates(worldBounds.Center, mapId);
            if (CanAnyStairPreviewOriginSeeBounds(target, worldBounds, mapId, _stairPreviewEye.VisualZOffset))
            {
                continue;
            }

            if (!_stairPreviewHiddenSpriteVisibility.TryAdd(uid, sprite.Visible))
                continue;

            Sprite.SetVisible((uid, sprite), false);
        }
    }

    private void RestoreStairPreviewSprites()
    {
        foreach (var (uid, wasVisible) in _stairPreviewHiddenSpriteVisibility)
        {
            if (!wasVisible ||
                !_entityManager.TryGetComponent<SpriteComponent>(uid, out var sprite) ||
                sprite.Visible)
            {
                continue;
            }

            Sprite.SetVisible((uid, sprite), true);
        }

        _stairPreviewHiddenSpriteVisibility.Clear();
        _stairPreviewSpriteCandidates.Clear();
    }

    private Box2 GetStairPreviewSpriteBounds(
        EntityUid uid,
        SpriteComponent sprite,
        TransformComponent xform,
        EntityQuery<TransformComponent> xformQuery)
    {
        var worldPos = _transform!.GetWorldPosition(xform, xformQuery);
        return Sprite.GetLocalBounds((uid, sprite)).Translated(worldPos);
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

        if (_drawFaintUpperComposite && _stairPreviewViewport is not null)
        {
            handle.DrawingHandleScreen.DrawTextureRect(
                _stairPreviewViewport.RenderTarget.Texture,
                drawBox,
                Color.White.WithAlpha(_faintUpperAlpha));
        }
    }

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

        // Do not reveal the upper map through a real ceiling.
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

        var rotation = -fallbackEye.Rotation;
        var offset = rotation.ToWorldVec() * CMUClientZLevelsSystem.ZLevelOffset;

        _zEye.LowestDepth = lowestDepth;
        _zEye.Depth = 1;
        _zEye.HighestDepth = 1;
        _zEye.BaseMapId = viewXform.MapID;
        _zEye.WeatherSourceMapId = weatherSourceMapId;
        _zEye.Position = new MapCoordinates(fallbackEye.Position.Position, upperMapComp.MapId);
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
        _stairPreviewViewport.Render();

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

    private void DrawStairPreviewFovMask(DrawingHandleScreen screen, UIBox2 drawBox)
    {
        var maskStart = Stopwatch.GetTimestamp();
        if (_stairPreviewViewport is null ||
            _mapSystem is null ||
            _transform is null ||
            _lookup is null ||
            _occluder is null ||
            !TryGetViewportWorldAabb(_stairPreviewViewport, out var worldAabb))
        {
            return;
        }

        var mapId = _stairPreviewEye.Position.MapId;
        if (_stairPreviewOrigins.Count == 0)
            return;

        _stairPreviewGrids.Clear();
        _mapSystem.FindGridsIntersecting(mapId, worldAabb, ref _stairPreviewGrids, approx: true, includeMap: true);

        foreach (var grid in _stairPreviewGrids)
        {
            var gridMatrix = _transform.GetWorldMatrix(grid.Owner);
            foreach (var tile in _mapSystem.GetTilesIntersecting(grid.Owner, grid.Comp, worldAabb, ignoreEmpty: true))
            {
                LastZRenderDebugStats.StairPreviewTilesScanned++;
                var localBounds = _lookup.GetLocalBounds(tile, grid.Comp.TileSize).Enlarged(0.01f);
                var worldBounds = gridMatrix.TransformBox(localBounds);
                var target = new MapCoordinates(worldBounds.Center, mapId);

                if (!CanAnyStairPreviewOriginSeeBounds(target, worldBounds, mapId, _stairPreviewEye.VisualZOffset))
                    continue;

                LastZRenderDebugStats.StairPreviewTilesDrawn++;
                screen.DrawRect(GetCompositeScreenBox(localBounds, gridMatrix, drawBox), Color.White);
            }
        }

        _stairPreviewGrids.Clear();
        LastZRenderDebugStats.StairPreviewFovMaskMs += GetElapsedMilliseconds(maskStart);
    }

    private void SetStairPreviewOrigins(CMUZLevelViewerComponent viewer, Vector2 viewerPosition)
    {
        _stairPreviewOrigins.Clear();

        for (var i = 0; i < CMUZLevelViewerComponent.MaxStairPreviewPositions; i++)
        {
            if (!TryGetStairPreviewPosition(viewer, i, out var position))
                break;

            _stairPreviewOrigins.Add(new StairPreviewOrigin(position, viewerPosition));
        }
    }

    internal static bool TryGetStairPreviewPosition(
        CMUZLevelViewerComponent viewer,
        int index,
        out Vector2 position)
    {
        var count = Math.Clamp(
            viewer.StairPreviewPositionCount,
            0,
            CMUZLevelViewerComponent.MaxStairPreviewPositions);

        if (index < 0 || index >= count)
        {
            position = default;
            return false;
        }

        position = index switch
        {
            0 => viewer.StairPreviewPosition,
            1 => viewer.StairPreviewPosition2,
            2 => viewer.StairPreviewPosition3,
            3 => viewer.StairPreviewPosition4,
            _ => default,
        };
        return true;
    }

    private bool CanAnyStairPreviewOriginSeeBounds(
        MapCoordinates target,
        Box2 bounds,
        MapId mapId,
        Vector2 renderOffset)
    {
        if (_occluder is null)
            return false;

        foreach (var origin in _stairPreviewOrigins)
        {
            if (!CMUZLevelStairPreviewVisibility.IsInFrontOfStair(
                    origin.ViewerPosition,
                    origin.Position,
                    target.Position - renderOffset))
            {
                continue;
            }

            if (!CMUZLevelStairPreviewVisibility.ProjectedBoundsStayInFrontOfStair(
                    origin.ViewerPosition,
                    origin.Position,
                    bounds,
                    renderOffset))
            {
                continue;
            }

            var originCoordinates = new MapCoordinates(origin.Position, mapId);
            LastZRenderDebugStats.StairPreviewLosChecks++;
            if (_occluder.InRangeUnoccluded(originCoordinates, target, 0f, ignoreTouching: true))
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
        _drawFaintUpperComposite = false;
    }

    internal static void NoteZRenderBypassed(string reason)
    {
        LastZRenderDebugStats.Reset();
        LastZRenderDebugStats.SkipReason = reason;
        LastZRenderDebugStats.BasePassRendered = true;
    }

    private void DisposeZLevelViewports()
    {
        _stairPreviewViewport?.Dispose();
        _stairPreviewViewport = null;
        _zLowerRenderGrace.Reset();
        RestoreStairPreviewSprites();
        ClearZLevelCompositeState();
    }

    private readonly record struct StairPreviewOrigin(Vector2 Position, Vector2 ViewerPosition);

    internal readonly struct ZLevelViewIdentity
    {
        public readonly IEye Eye;
        public readonly EntityUid ViewEntity;
        public readonly EntityUid BaseMapUid;
        public readonly EntityUid NetworkUid;

        public ZLevelViewIdentity(
            IEye eye,
            EntityUid viewEntity,
            EntityUid baseMapUid,
            EntityUid networkUid)
        {
            Eye = eye;
            ViewEntity = viewEntity;
            BaseMapUid = baseMapUid;
            NetworkUid = networkUid;
        }

        public bool Matches(ZLevelViewIdentity other)
        {
            return ReferenceEquals(Eye, other.Eye) &&
                   ViewEntity == other.ViewEntity &&
                   BaseMapUid == other.BaseMapUid &&
                   NetworkUid == other.NetworkUid;
        }
    }

    internal sealed class LowerRenderGraceState
    {
        private ZLevelViewIdentity _identity;
        private bool _hasIdentity;

        public int LowestDepth { get; private set; }
        public TimeSpan Until { get; private set; }

        public int Resolve(
            ZLevelViewIdentity identity,
            TimeSpan now,
            TimeSpan graceDuration,
            int maxDepth,
            bool hasLowerMap,
            int discoveredLowestDepth,
            out bool active)
        {
            if (!_hasIdentity || !_identity.Matches(identity))
            {
                Reset();
                _identity = identity;
                _hasIdentity = true;
            }

            active = false;
            if (discoveredLowestDepth < 0)
            {
                LowestDepth = discoveredLowestDepth;
                Until = graceDuration > TimeSpan.Zero
                    ? now + graceDuration
                    : TimeSpan.Zero;
                return discoveredLowestDepth;
            }

            if (graceDuration > TimeSpan.Zero &&
                maxDepth > 0 &&
                hasLowerMap &&
                LowestDepth < 0 &&
                now <= Until)
            {
                active = true;
                return Math.Clamp(LowestDepth, -maxDepth, -1);
            }

            LowestDepth = 0;
            Until = TimeSpan.Zero;
            return discoveredLowestDepth;
        }

        public void Reset()
        {
            _identity = default;
            _hasIdentity = false;
            LowestDepth = 0;
            Until = TimeSpan.Zero;
        }
    }

    internal sealed class ZLevelRenderVisibilityState
    {
        private readonly List<int> _renderedLowerDepths = new();
        private ZLevelViewIdentity _identity;
        private MapId _baseMapId = MapId.Nullspace;
        private bool _valid;

        public void Publish(
            ZLevelViewIdentity identity,
            MapId baseMapId,
            IReadOnlyList<int> renderedLowerDepths)
        {
            _identity = identity;
            _baseMapId = baseMapId;
            _renderedLowerDepths.Clear();

            for (var i = 0; i < renderedLowerDepths.Count; i++)
            {
                _renderedLowerDepths.Add(renderedLowerDepths[i]);
            }

            _valid = true;
        }

        public bool TryCopyRenderedLowerDepths(
            ZLevelViewIdentity identity,
            MapId baseMapId,
            List<int> destination)
        {
            destination.Clear();
            if (!_valid ||
                _baseMapId != baseMapId ||
                !_identity.Matches(identity))
            {
                return false;
            }

            destination.AddRange(_renderedLowerDepths);
            return true;
        }

        public void Invalidate()
        {
            _valid = false;
            _identity = default;
            _baseMapId = MapId.Nullspace;
            _renderedLowerDepths.Clear();
        }
    }

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
        public double StairPreviewCullMs;
        public double StairPreviewFovMaskMs;
        public int StairPreviewSpriteCandidates;
        public int StairPreviewSpriteVisibilityChecks;
        public int StairPreviewTilesScanned;
        public int StairPreviewTilesDrawn;
        public int StairPreviewLosChecks;
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
            StairPreviewCullMs = 0d;
            StairPreviewFovMaskMs = 0d;
            StairPreviewSpriteCandidates = 0;
            StairPreviewSpriteVisibilityChecks = 0;
            StairPreviewTilesScanned = 0;
            StairPreviewTilesDrawn = 0;
            StairPreviewLosChecks = 0;
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
