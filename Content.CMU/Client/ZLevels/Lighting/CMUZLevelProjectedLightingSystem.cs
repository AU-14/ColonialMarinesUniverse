using System.Numerics;
using Content.Client.Examine;
using Content.Client._CMU14.ZLevels.Core;
using Content.Client.Viewport;
using Content.Shared._CMU14.ZLevels;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using SysStopwatch = System.Diagnostics.Stopwatch;

namespace Content.Client._CMU14.ZLevels.Lighting;

/// <summary>
/// Projects client-only point lights from adjacent Z-level maps onto the local receiving map.
/// </summary>
public sealed partial class CMUZLevelProjectedLightingSystem : EntitySystem
{
    private const float OpeningConnectionDistance = 1.5f;
    private const int MinStripCandidateCount = 4;
    private const float MinStripLength = 3f;
    private const float StripLinearityRatio = 2.5f;
    private const float StripSampleSpacing = 1.5f;
    private const int MaxStripSamples = 8;
    private const float MaxProjectedCenterOffset = 0.5f;
    private const int PartialSelectionSortMultiplier = 4;
    private const float ViewBoundsLightPadding = 2f;
    private const int MaxOpeningLosChecks = 32;

    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private ITileDefinitionManager _tile = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private LightTreeSystem _lightTree = default!;
    [Dependency] private ExamineSystem _examine = default!;

    private CMUClientZLevelsSystem _zLevels = default!;

    internal static ProjectedLightingDebugStats LastProjectedLightingDebugStats { get; } = new();

    /// <summary>
    /// Cache of source light entity and opening center to projected client-only light entity.
    /// </summary>
    private readonly Dictionary<ProjectedLightKey, EntityUid> _projectedLights = new();
    private readonly Dictionary<MergedProjectedLightKey, EntityUid> _mergedProjectedLights = new();

    private readonly HashSet<EntityUid> _activeThisFrame = new();
    private readonly List<ProjectedLightCandidate> _candidates = new();
    private readonly List<ProjectedLightCandidate> _sourceCandidates = new();
    private readonly List<ProjectedLightCandidate> _componentCandidates = new();
    private readonly List<int> _candidateStack = new();
    private readonly List<bool> _visitedSourceCandidates = new();
    private List<Entity<MapGridComponent>> _openingGrids = new();
    private readonly List<ProjectedLightKey> _toRemove = new();
    private readonly List<MergedProjectedLightKey> _mergedToRemove = new();
    private readonly List<(Vector2 Center, float Distance)> _tempOpenings = new();
    private readonly List<Box2> _currentViewOpeningBounds = new();
    private readonly List<int> _checkedOpeningIndices = new(MaxOpeningLosChecks);
    private readonly List<Entity<PointLightComponent, TransformComponent>> _lightTreeResults = new();
    private readonly HashSet<EntityUid> _sourceLightSeen = new();
    private readonly List<Box2> _portalLightQueryBounds = new();
    private readonly List<Box2> _portalOpeningCandidateBounds = new();
    private readonly List<Box2> _cachedCurrentViewOpeningBounds = new();
    private readonly HashSet<MapId> _queriedSourceLightMaps = new();
    private readonly Dictionary<MapId, List<SourceLight>> _sourceLightBuckets = new();
    private readonly Dictionary<OpeningCandidateBucketKey, List<int>> _openingCandidateBuckets = new();
    private readonly List<List<int>> _openingCandidateBucketPool = new();
    private readonly ProjectedLightAlongAxisComparer _alongAxisComparer = new();
    private Box2 _combinedCurrentViewOpeningBounds;
    private Box2 _cachedCombinedCurrentViewOpeningBounds;
    private TimeSpan _currentViewOpeningGraceUntil = TimeSpan.Zero;
    private MapId _currentViewOpeningGraceMapId = MapId.Nullspace;
    private bool _currentViewOpeningBoundsComplete;
    private bool _cachedCurrentViewOpeningBoundsComplete;

    private EntityQuery<CMUProjectedLightComponent> _projectedQuery;
    private EntityQuery<PointLightComponent> _pointLightQuery;
    private EntityQuery<MapComponent> _mapQuery;
    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<CMUZLevelMapComponent> _zMapQuery;

    /// <inheritdoc />
    public override void Initialize()
    {
        base.Initialize();

        _zLevels = EntityManager.System<CMUClientZLevelsSystem>();
        _projectedQuery = GetEntityQuery<CMUProjectedLightComponent>();
        _pointLightQuery = GetEntityQuery<PointLightComponent>();
        _mapQuery = GetEntityQuery<MapComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
        _zMapQuery = GetEntityQuery<CMUZLevelMapComponent>();
    }

    /// <inheritdoc />
    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var stats = LastProjectedLightingDebugStats;
        stats.Reset();
        var totalStart = SysStopwatch.GetTimestamp();

        if (!_config.GetCVar(CMUZLevelsCVars.Enabled) ||
            !_config.GetCVar(CMUZLevelsCVars.RenderEnabled) ||
            !_config.GetCVar(CMUZLevelsCVars.ProjectedLightingEnabled))
        {
            stats.SkipReason = "projected lighting disabled";
            stats.CleanupCount += CleanupAllProjectedLights();
            stats.ActiveProjectedLights = GetActiveProjectedLightCount();
            stats.TotalMs = GetElapsedMilliseconds(totalStart);
            return;
        }

        if (_player.LocalEntity is not { } playerUid ||
            !TryComp<CMUZLevelViewerComponent>(playerUid, out var viewer) ||
            !_xformQuery.TryComp(playerUid, out var playerXform) ||
            playerXform.MapUid is not { } playerMapUid ||
            !_mapQuery.TryComp(playerMapUid, out var playerMapComp) ||
            !_zMapQuery.TryComp(playerMapUid, out var playerZMap))
        {
            stats.SkipReason = "no local Z-level viewer";
            stats.CleanupCount += CleanupAllProjectedLights();
            stats.ActiveProjectedLights = GetActiveProjectedLightCount();
            stats.TotalMs = GetElapsedMilliseconds(totalStart);
            return;
        }

        var maxPerLevel = Math.Max(0, _config.GetCVar(CMUZLevelsCVars.MaxProjectedLightsPerLevel));
        if (maxPerLevel == 0)
        {
            stats.SkipReason = "max projected lights is zero";
            stats.CleanupCount += CleanupAllProjectedLights();
            stats.ActiveProjectedLights = GetActiveProjectedLightCount();
            stats.TotalMs = GetElapsedMilliseconds(totalStart);
            return;
        }

        var projectLowerReceivers = _config.GetCVar(CMUZLevelsCVars.ProjectedLightingLowerReceivers);
        var projectLowerSources = _config.GetCVar(CMUZLevelsCVars.ProjectedLightingLowerSources);
        var visibilityGraceSeconds = Math.Max(0f, _config.GetCVar(CMUZLevelsCVars.ProjectedLightingVisibilityGrace));
        var maxSourceLightsPerMap = Math.Max(0, _config.GetCVar(CMUZLevelsCVars.ProjectedLightingMaxSourceLightsPerMap));
        var maxOpeningsPerSource = Math.Max(0, _config.GetCVar(CMUZLevelsCVars.ProjectedLightingMaxOpeningsPerSource));
        var attenuationPerDepth = Math.Max(0f, _config.GetCVar(CMUZLevelsCVars.ProjectedLightAttenuationPerDepth));
        var attenuationPerTile = Math.Max(0f, _config.GetCVar(CMUZLevelsCVars.ProjectedLightAttenuationPerTile));
        var maxRadius = Math.Max(0f, _config.GetCVar(CMUZLevelsCVars.ProjectedLightMaxRadius));
        var radiusScale = Math.Max(0f, _config.GetCVar(CMUZLevelsCVars.ProjectedLightRadiusScale));
        var minEnergy = Math.Max(0f, _config.GetCVar(CMUZLevelsCVars.ProjectedLightMinEnergy));
        var maxDepth = Math.Clamp(
            _config.GetCVar(CMUZLevelsCVars.MaxRenderDepth),
            0,
            CMUSharedZLevelsSystem.MaxZLevelsBelowRendering);

        var currentFrame = _timing.CurFrame;
        stats.VisibilityGraceSeconds = visibilityGraceSeconds;
        _activeThisFrame.Clear();

        var viewBounds = _eyeManager.GetWorldViewbounds();
        var viewAabb = viewBounds.CalcBoundingBox();
        var playerWorldPosition = _transform.GetWorldPosition(playerXform);
        var useRenderVisibilityGate = TryUpdateRenderedLowerDepths(playerMapUid, playerMapComp.MapId);
        var maxOpeningRects = Math.Max(0, _config.GetCVar(CMUZLevelsCVars.MaxOpeningRectsPerPass));
        var openingStart = SysStopwatch.GetTimestamp();
        var hasCurrentViewOpening = TryUpdateCurrentViewOpenings(
            playerMapComp.MapId,
            viewAabb,
            playerWorldPosition,
            maxOpeningRects,
            visibilityGraceSeconds);
        var hasUpperSourceOpening = HasUpperSourceOpening(playerMapUid, viewAabb);
        stats.CurrentOpeningMs = GetElapsedMilliseconds(openingStart);
        stats.VisibleCurrentOpenings = hasCurrentViewOpening;
        stats.UpperSourceOpenings = hasUpperSourceOpening;
        stats.CurrentOpeningBounds = _currentViewOpeningBounds.Count;
        stats.CurrentOpeningBoundsComplete = _currentViewOpeningBoundsComplete;
        BuildPortalOpeningCandidateBounds();

        if (!viewer.LookUp &&
            !viewer.StairPreviewUp &&
            !hasCurrentViewOpening &&
            !hasUpperSourceOpening)
        {
            stats.SkipReason = "no visible current openings";
            stats.CleanupCount += CleanupStaleProjectedLights(visibilityGraceSeconds);
            stats.ActiveProjectedLights = GetActiveProjectedLightCount();
            stats.TotalMs = GetElapsedMilliseconds(totalStart);
            return;
        }

        stats.Ran = true;
        stats.SkipReason = "processed";
        Entity<CMUZLevelMapComponent?> playerZLevelMap = (playerMapUid, playerZMap);
        var sourceStart = SysStopwatch.GetTimestamp();
        BuildSourceLightBuckets(
            viewBounds,
            minEnergy,
            playerZLevelMap,
            playerMapComp,
            maxDepth,
            maxSourceLightsPerMap,
            projectLowerReceivers,
            projectLowerSources,
            useRenderVisibilityGate,
            stats.RenderedLowerDepths);
        stats.SourceQueryMs = GetElapsedMilliseconds(sourceStart);

        var candidateStart = SysStopwatch.GetTimestamp();
        for (var depthOffset = -maxDepth; depthOffset <= 1; depthOffset++)
        {
            if (depthOffset == 0)
                continue;

            if (depthOffset < 0 && !projectLowerSources)
                continue;

            if (!ShouldProcessLowerProjectionDepth(depthOffset, useRenderVisibilityGate, stats.RenderedLowerDepths))
            {
                stats.LowerSourcePassesSkippedByRenderVisibility++;
                continue;
            }

            if (!_zLevels.TryMapOffset(playerMapUid, depthOffset, out var adjacentMap, out var adjacentMapComp) ||
                adjacentMapComp.MapId == MapId.Nullspace)
            {
                continue;
            }

            _candidates.Clear();
            if (!_sourceLightBuckets.TryGetValue(adjacentMapComp.MapId, out var sourceLights) ||
                sourceLights.Count == 0)
            {
                continue;
            }

            CollectCandidates(
                sourceLights,
                adjacentMap.Value,
                adjacentMapComp.MapId,
                playerMapUid,
                playerMapComp.MapId,
                playerMapUid,
                playerMapComp.MapId,
                depthOffset,
                attenuationPerDepth,
                attenuationPerTile,
                radiusScale,
                maxRadius,
                minEnergy,
                maxOpeningsPerSource);

            ApplyLevelCap(maxPerLevel, currentFrame);
        }

        if (projectLowerReceivers)
        {
            for (var receivingDepth = -1; receivingDepth >= -maxDepth; receivingDepth--)
            {
                if (!ShouldProcessLowerProjectionDepth(receivingDepth, useRenderVisibilityGate, stats.RenderedLowerDepths))
                {
                    stats.LowerReceiverPassesSkippedByRenderVisibility++;
                    continue;
                }

                if (!_zLevels.TryMapOffset(playerZLevelMap, receivingDepth, out var receivingMap, out var receivingMapComp))
                    break;

                if (receivingMap is not { } receiving ||
                    receivingMapComp.MapId == MapId.Nullspace)
                {
                    continue;
                }

                var sourceDepth = receivingDepth + 1;
                if (!ShouldProcessLowerProjectionDepth(sourceDepth, useRenderVisibilityGate, stats.RenderedLowerDepths))
                {
                    stats.LowerReceiverPassesSkippedByRenderVisibility++;
                    continue;
                }

                Entity<CMUZLevelMapComponent> sourceMap;
                MapComponent sourceMapComp;
                if (sourceDepth == 0)
                {
                    sourceMap = (playerMapUid, playerZMap);
                    sourceMapComp = playerMapComp;
                }
                else if (!_zLevels.TryMapOffset(playerZLevelMap, sourceDepth, out var offsetSourceMap, out var offsetSourceMapComp))
                {
                    continue;
                }
                else
                {
                    sourceMap = offsetSourceMap.Value;
                    sourceMapComp = offsetSourceMapComp;
                }

                if (sourceMapComp.MapId == MapId.Nullspace)
                {
                    continue;
                }

                _candidates.Clear();
                if (!_sourceLightBuckets.TryGetValue(sourceMapComp.MapId, out var sourceLights) ||
                    sourceLights.Count == 0)
                {
                    continue;
                }

                CollectCandidates(
                    sourceLights,
                    sourceMap,
                    sourceMapComp.MapId,
                    receiving.Owner,
                    receivingMapComp.MapId,
                    playerMapUid,
                    playerMapComp.MapId,
                    1,
                    attenuationPerDepth,
                    attenuationPerTile,
                    radiusScale,
                    maxRadius,
                    minEnergy,
                    maxOpeningsPerSource);

                ApplyLevelCap(maxPerLevel, currentFrame);
            }
        }

        stats.CandidateMs = GetElapsedMilliseconds(candidateStart);
        stats.CleanupCount += CleanupStaleProjectedLights(visibilityGraceSeconds);
        stats.ActiveProjectedLights = GetActiveProjectedLightCount();
        stats.TotalMs = GetElapsedMilliseconds(totalStart);
    }

    private bool TryUpdateCurrentViewOpenings(
        MapId mapId,
        Box2 worldAabb,
        Vector2 viewerPosition,
        int maxOpeningRects,
        float visibilityGraceSeconds)
    {
        var stats = LastProjectedLightingDebugStats;
        _currentViewOpeningBounds.Clear();
        _portalOpeningCandidateBounds.Clear();
        _portalLightQueryBounds.Clear();
        _combinedCurrentViewOpeningBounds = default;
        _currentViewOpeningBoundsComplete = false;

        var openingLimit = maxOpeningRects == 0
            ? int.MaxValue
            : maxOpeningRects + 1;

        var found = _zLevels.OpeningCache.TryFindOpeningBounds(
            mapId,
            worldAabb,
            _currentViewOpeningBounds,
            out _combinedCurrentViewOpeningBounds,
            openingLimit,
            true,
            _openingGrids,
            _mapManager,
            _map,
            _transform,
            _tile);

        stats.CurrentOpeningQueryFoundOpening = found;

        if (_openingGrids.Count == 0)
        {
            stats.CurrentOpeningLosConservativeFallback = true;
            stats.CurrentOpeningLosMode = "no grids";
            return true;
        }

        if (!found)
            return TryUseCurrentViewOpeningGrace(mapId, visibilityGraceSeconds);

        if (_currentViewOpeningBounds.Count == 0)
        {
            stats.CurrentOpeningLosConservativeFallback = true;
            stats.CurrentOpeningLosMode = "no bounds";
            return true;
        }

        if (maxOpeningRects > 0 && _currentViewOpeningBounds.Count > maxOpeningRects)
        {
            stats.CurrentOpeningBoundsTruncated = true;
            stats.CurrentOpeningLosConservativeFallback = true;
            stats.CurrentOpeningLosMode = "truncated";
            return true;
        }

        var visible = FilterVisibleCurrentViewOpenings(mapId, viewerPosition);
        _currentViewOpeningBoundsComplete = visible &&
            stats.CurrentOpeningLosMode == "exhaustive";

        if (visible)
        {
            RememberCurrentViewOpeningBounds(mapId, visibilityGraceSeconds);
            return true;
        }

        if (TryUseCurrentViewOpeningGrace(mapId, visibilityGraceSeconds))
            return true;

        return visible;
    }

    private bool FilterVisibleCurrentViewOpenings(MapId mapId, Vector2 viewerPosition)
    {
        var stats = LastProjectedLightingDebugStats;
        stats.CurrentOpeningLosMode = "exhaustive";

        if (_currentViewOpeningBounds.Count > MaxOpeningLosChecks)
        {
            stats.CurrentOpeningLosMode = "sampled";
            if (HasSampledVisibleCurrentViewOpening(mapId, viewerPosition))
                return true;

            _currentViewOpeningBounds.Clear();
            _combinedCurrentViewOpeningBounds = default;
            return false;
        }

        var origin = new MapCoordinates(viewerPosition, mapId);
        for (var i = _currentViewOpeningBounds.Count - 1; i >= 0; i--)
        {
            if (CanSeeCurrentViewOpening(origin, mapId, _currentViewOpeningBounds[i]))
                continue;

            _currentViewOpeningBounds.RemoveAt(i);
        }

        RecalculateCurrentViewOpeningBounds();
        return _currentViewOpeningBounds.Count > 0;
    }

    private bool HasSampledVisibleCurrentViewOpening(MapId mapId, Vector2 viewerPosition)
    {
        var origin = new MapCoordinates(viewerPosition, mapId);
        _checkedOpeningIndices.Clear();

        var nearestChecks = Math.Min(MaxOpeningLosChecks / 2, _currentViewOpeningBounds.Count);
        for (var i = 0; i < nearestChecks; i++)
        {
            var index = FindNearestUncheckedOpening(_currentViewOpeningBounds, viewerPosition, _checkedOpeningIndices);
            if (index < 0)
                break;

            _checkedOpeningIndices.Add(index);
            if (CanSeeCurrentViewOpening(origin, mapId, _currentViewOpeningBounds[index]))
                return true;
        }

        var spreadChecks = Math.Min(
            MaxOpeningLosChecks - _checkedOpeningIndices.Count,
            _currentViewOpeningBounds.Count - _checkedOpeningIndices.Count);
        for (var i = 0; i < spreadChecks && _checkedOpeningIndices.Count < MaxOpeningLosChecks; i++)
        {
            var targetIndex = spreadChecks == 1
                ? _currentViewOpeningBounds.Count / 2
                : (int) MathF.Round(i * (_currentViewOpeningBounds.Count - 1) / (float) (spreadChecks - 1));
            var index = FindUncheckedOpeningAround(targetIndex, _currentViewOpeningBounds.Count, _checkedOpeningIndices);
            if (index < 0)
                break;

            _checkedOpeningIndices.Add(index);
            if (CanSeeCurrentViewOpening(origin, mapId, _currentViewOpeningBounds[index]))
                return true;
        }

        return false;
    }

    private bool CanSeeCurrentViewOpening(MapCoordinates origin, MapId mapId, Box2 openingBounds)
    {
        var center = openingBounds.Center;
        if (CanSeeCurrentViewOpeningPoint(origin, mapId, center))
            return true;

        var closest = openingBounds.ClosestPoint(origin.Position);
        if (CanSeeCurrentViewOpeningPoint(origin, mapId, InsetOpeningPoint(closest, center)))
            return true;

        return CanSeeCurrentViewOpeningPoint(origin, mapId, InsetOpeningPoint(openingBounds.BottomLeft, center)) ||
               CanSeeCurrentViewOpeningPoint(origin, mapId, InsetOpeningPoint(openingBounds.TopLeft, center)) ||
               CanSeeCurrentViewOpeningPoint(origin, mapId, InsetOpeningPoint(openingBounds.TopRight, center)) ||
               CanSeeCurrentViewOpeningPoint(origin, mapId, InsetOpeningPoint(openingBounds.BottomRight, center));
    }

    private bool CanSeeCurrentViewOpeningPoint(MapCoordinates origin, MapId mapId, Vector2 targetPosition)
    {
        LastProjectedLightingDebugStats.CurrentOpeningLosChecks++;
        return _examine.InRangeUnOccluded(origin, new MapCoordinates(targetPosition, mapId), 0f, null);
    }

    private static Vector2 InsetOpeningPoint(Vector2 point, Vector2 center)
    {
        return point + (center - point) * 0.15f;
    }

    private void RememberCurrentViewOpeningBounds(MapId mapId, float visibilityGraceSeconds)
    {
        _cachedCurrentViewOpeningBounds.Clear();
        _cachedCurrentViewOpeningBounds.AddRange(_currentViewOpeningBounds);
        _cachedCombinedCurrentViewOpeningBounds = _combinedCurrentViewOpeningBounds;
        _cachedCurrentViewOpeningBoundsComplete = _currentViewOpeningBoundsComplete;
        _currentViewOpeningGraceMapId = mapId;
        _currentViewOpeningGraceUntil = visibilityGraceSeconds > 0f
            ? _timing.CurTime + TimeSpan.FromSeconds(visibilityGraceSeconds)
            : TimeSpan.Zero;
    }

    private bool TryUseCurrentViewOpeningGrace(MapId mapId, float visibilityGraceSeconds)
    {
        if (visibilityGraceSeconds <= 0f ||
            _cachedCurrentViewOpeningBounds.Count == 0 ||
            _currentViewOpeningGraceMapId != mapId ||
            _timing.CurTime > _currentViewOpeningGraceUntil)
        {
            return false;
        }

        _currentViewOpeningBounds.Clear();
        _currentViewOpeningBounds.AddRange(_cachedCurrentViewOpeningBounds);
        _combinedCurrentViewOpeningBounds = _cachedCombinedCurrentViewOpeningBounds;
        _currentViewOpeningBoundsComplete = _cachedCurrentViewOpeningBoundsComplete;

        var stats = LastProjectedLightingDebugStats;
        stats.CurrentOpeningBoundsFromGrace = true;
        stats.CurrentOpeningLosMode = "visibility grace";
        stats.CurrentOpeningGraceRemainingMs = Math.Max(
            0d,
            (_currentViewOpeningGraceUntil - _timing.CurTime).TotalMilliseconds);
        return true;
    }

    private bool HasUpperSourceOpening(EntityUid playerMapUid, Box2 worldAabb)
    {
        if (!_zLevels.TryMapOffset(playerMapUid, 1, out _, out var upperMapComp) ||
            upperMapComp.MapId == MapId.Nullspace)
        {
            return false;
        }

        return _zLevels.OpeningCache.TryFindOpeningBounds(
            upperMapComp.MapId,
            worldAabb,
            null,
            out _,
            1,
            false,
            _openingGrids,
            _mapManager,
            _map,
            _transform,
            _tile);
    }

    private void RecalculateCurrentViewOpeningBounds()
    {
        _combinedCurrentViewOpeningBounds = default;
        var hasBounds = false;

        foreach (var bounds in _currentViewOpeningBounds)
        {
            _combinedCurrentViewOpeningBounds = hasBounds
                ? _combinedCurrentViewOpeningBounds.Union(bounds)
                : bounds;
            hasBounds = true;
        }
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

    private static bool HasCheckedOpeningIndex(List<int> checkedIndices, int index)
    {
        for (var i = 0; i < checkedIndices.Count; i++)
        {
            if (checkedIndices[i] == index)
                return true;
        }

        return false;
    }

    private void BuildSourceLightBuckets(
        Box2Rotated viewBounds,
        float minEnergy,
        Entity<CMUZLevelMapComponent?> playerZLevelMap,
        MapComponent playerMapComp,
        int maxDepth,
        int maxSourceLightsPerMap,
        bool includePlayerMap,
        bool includeLowerSources,
        bool useRenderVisibilityGate,
        IReadOnlyList<int> renderedLowerDepths)
    {
        ClearSourceLightBuckets();
        _queriedSourceLightMaps.Clear();

        if (includePlayerMap &&
            ShouldProcessLowerProjectionDepth(-1, useRenderVisibilityGate, renderedLowerDepths))
        {
            if (CanUseCurrentViewOpeningBoundsFilter())
                QuerySourceLightBucketForCurrentViewOpenings(playerMapComp.MapId, minEnergy);
            else
                QuerySourceLightBucket(playerMapComp.MapId, viewBounds, minEnergy);
        }
        else if (includePlayerMap)
        {
            LastProjectedLightingDebugStats.SourceMapsSkippedByRenderVisibility++;
        }

        for (var depthOffset = -maxDepth; depthOffset <= 1; depthOffset++)
        {
            if (depthOffset == 0)
                continue;

            if (depthOffset < 0 && !includeLowerSources)
                continue;

            if (!ShouldProcessLowerProjectionDepth(depthOffset, useRenderVisibilityGate, renderedLowerDepths))
            {
                LastProjectedLightingDebugStats.SourceMapsSkippedByRenderVisibility++;
                continue;
            }

            if (_zLevels.TryMapOffset(playerZLevelMap, depthOffset, out _, out var adjacentMapComp) &&
                adjacentMapComp.MapId != MapId.Nullspace)
            {
                if (depthOffset < 0 && CanUseCurrentViewOpeningBoundsFilter())
                    QuerySourceLightBucketForCurrentViewOpenings(adjacentMapComp.MapId, minEnergy);
                else
                    QuerySourceLightBucket(adjacentMapComp.MapId, viewBounds, minEnergy);
            }
        }

        CapSourceLightBuckets(maxSourceLightsPerMap);
    }

    private static bool ShouldProcessLowerProjectionDepth(
        int depthOffset,
        bool useRenderVisibilityGate,
        IReadOnlyList<int> renderedLowerDepths)
    {
        if (depthOffset >= 0 ||
            !useRenderVisibilityGate)
        {
            return true;
        }

        for (var i = 0; i < renderedLowerDepths.Count; i++)
        {
            if (renderedLowerDepths[i] == depthOffset)
                return true;
        }

        return false;
    }

    private bool TryUpdateRenderedLowerDepths(EntityUid playerMapUid, MapId playerMapId)
    {
        var stats = LastProjectedLightingDebugStats;
        stats.RenderedLowerDepths.Clear();
        stats.RenderVisibilityGateValid = false;

        var zRenderStats = ScalingViewport.LastZRenderDebugStats;
        if (!zRenderStats.UsedZRender ||
            zRenderStats.BaseMapUid is not { } baseMapUid ||
            baseMapUid != playerMapUid ||
            zRenderStats.BaseMapId != playerMapId)
        {
            return false;
        }

        stats.RenderVisibilityGateValid = true;
        stats.RenderedLowerDepths.AddRange(zRenderStats.LowerRenderedDepths);
        return true;
    }

    private void QuerySourceLightBucket(
        MapId mapId,
        Box2Rotated viewBounds,
        float minEnergy)
    {
        if (mapId == MapId.Nullspace ||
            !_queriedSourceLightMaps.Add(mapId))
        {
            return;
        }

        var stats = LastProjectedLightingDebugStats;
        stats.SourceMapsChecked++;
        stats.SourceQueries++;
        _lightTreeResults.Clear();
        _lightTree.QueryAabb(_lightTreeResults, mapId, viewBounds);
        stats.LightsScanned += _lightTreeResults.Count;

        foreach (var lightEnt in _lightTreeResults)
        {
            if (!TryBuildSourceLight(lightEnt, mapId, minEnergy, out var sourceLight))
                continue;

            var expandedBounds = viewBounds.Enlarged(sourceLight.Radius + ViewBoundsLightPadding);
            if (!expandedBounds.Contains(sourceLight.WorldPosition))
                continue;

            AddSourceLight(sourceLight, mapId);
        }
    }

    private void QuerySourceLightBucketForCurrentViewOpenings(
        MapId mapId,
        float minEnergy)
    {
        if (mapId == MapId.Nullspace ||
            !_queriedSourceLightMaps.Add(mapId))
        {
            return;
        }

        var stats = LastProjectedLightingDebugStats;
        stats.SourceMapsChecked++;
        BuildPortalLightQueryBounds();
        if (_portalLightQueryBounds.Count == 0)
            return;

        _sourceLightSeen.Clear();
        foreach (var bounds in _portalLightQueryBounds)
        {
            stats.SourceQueries++;
            stats.PortalLightQueries++;
            _lightTreeResults.Clear();
            _lightTree.QueryAabb(_lightTreeResults, mapId, bounds);
            stats.LightsScanned += _lightTreeResults.Count;

            foreach (var lightEnt in _lightTreeResults)
            {
                if (!_sourceLightSeen.Add(lightEnt.Owner) ||
                    !TryBuildSourceLight(lightEnt, mapId, minEnergy, out var sourceLight) ||
                    !SourceLightCanReachCurrentViewOpening(sourceLight))
                {
                    continue;
                }

                AddSourceLight(sourceLight, mapId);
                stats.PortalLightsAccepted++;
            }
        }
    }

    private bool TryBuildSourceLight(
        Entity<PointLightComponent, TransformComponent> lightEnt,
        MapId mapId,
        float minEnergy,
        out SourceLight sourceLight)
    {
        sourceLight = default;
        var lightUid = lightEnt.Owner;
        var light = lightEnt.Comp1;
        var lightXform = lightEnt.Comp2;

        if (_projectedQuery.HasComp(lightUid) ||
            lightXform.MapID == MapId.Nullspace ||
            lightXform.MapID != mapId ||
            !light.Enabled ||
            light.Radius <= 0f ||
            light.Energy <= 0f ||
            light.Energy < minEnergy)
        {
            return false;
        }

        sourceLight = new SourceLight(
            lightUid,
            _transform.GetWorldPosition(lightXform),
            light.Radius,
            light.Energy,
            light.Color,
            light.Softness);
        return true;
    }

    private void AddSourceLight(SourceLight sourceLight, MapId mapId)
    {
        GetSourceLightBucket(mapId).Add(sourceLight);
        LastProjectedLightingDebugStats.LightsAccepted++;
    }

    private void BuildPortalLightQueryBounds()
    {
        _portalLightQueryBounds.Clear();
        var sourceBounds = _portalOpeningCandidateBounds.Count > 0
            ? _portalOpeningCandidateBounds
            : _currentViewOpeningBounds;
        foreach (var openingBounds in sourceBounds)
        {
            AddMergedPortalLightQueryBounds(
                _portalLightQueryBounds,
                openingBounds.Enlarged(ViewBoundsLightPadding));
        }

        LastProjectedLightingDebugStats.PortalLightQueryBounds += _portalLightQueryBounds.Count;
    }

    private void BuildPortalOpeningCandidateBounds()
    {
        _portalOpeningCandidateBounds.Clear();
        if (!CanUseCurrentViewOpeningBoundsFilter())
        {
            LastProjectedLightingDebugStats.PortalOpeningCandidateBounds = 0;
            return;
        }

        foreach (var openingBounds in _currentViewOpeningBounds)
        {
            AddMergedPortalLightQueryBounds(_portalOpeningCandidateBounds, openingBounds);
        }

        LastProjectedLightingDebugStats.PortalOpeningCandidateBounds = _portalOpeningCandidateBounds.Count;
    }

    private static void AddMergedPortalLightQueryBounds(List<Box2> queryBounds, Box2 bounds)
    {
        for (var i = 0; i < queryBounds.Count; i++)
        {
            if (!BoundsOverlapOrTouch(queryBounds[i], bounds))
                continue;

            queryBounds[i] = queryBounds[i].Union(bounds);
            MergePortalLightQueryBounds(queryBounds, i);
            return;
        }

        queryBounds.Add(bounds);
    }

    private static void MergePortalLightQueryBounds(List<Box2> queryBounds, int index)
    {
        for (var i = queryBounds.Count - 1; i >= 0; i--)
        {
            if (i == index ||
                !BoundsOverlapOrTouch(queryBounds[index], queryBounds[i]))
            {
                continue;
            }

            queryBounds[index] = queryBounds[index].Union(queryBounds[i]);
            queryBounds.RemoveAt(i);
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

    private void CapSourceLightBuckets(int maxSourceLightsPerMap)
    {
        if (maxSourceLightsPerMap <= 0)
            return;

        foreach (var bucket in _sourceLightBuckets.Values)
        {
            if (bucket.Count <= maxSourceLightsPerMap)
                continue;

            bucket.Sort(CompareSourceLightEnergyDescending);
            var rejected = bucket.Count - maxSourceLightsPerMap;
            bucket.RemoveRange(maxSourceLightsPerMap, rejected);
            LastProjectedLightingDebugStats.LightsRejectedBySourceCap += rejected;
        }
    }

    private static int CompareSourceLightEnergyDescending(SourceLight left, SourceLight right)
    {
        return right.Energy.CompareTo(left.Energy);
    }

    private List<SourceLight> GetSourceLightBucket(MapId mapId)
    {
        if (_sourceLightBuckets.TryGetValue(mapId, out var bucket))
            return bucket;

        bucket = new List<SourceLight>();
        _sourceLightBuckets[mapId] = bucket;
        return bucket;
    }

    private void ClearSourceLightBuckets()
    {
        foreach (var bucket in _sourceLightBuckets.Values)
        {
            bucket.Clear();
        }
    }

    private void ApplyLevelCap(int maxPerLevel, uint currentFrame)
    {
        if (_candidates.Count == 0)
            return;

        if (_candidates.Count <= maxPerLevel)
        {
            _candidates.Sort(CompareProjectedEnergyDescending);
            foreach (var candidate in _candidates)
            {
                UpdateProjectedLight(candidate, currentFrame);
            }

            return;
        }

        var directCount = Math.Max(0, maxPerLevel - 1);
        if (directCount > 0 && ShouldPartiallySelectCandidates(_candidates.Count, directCount))
        {
            SelectTopCandidates(directCount);
        }
        else if (directCount > 0)
        {
            // Full sort is cheaper when the direct keep count is close to the
            // total candidate count; partial selection is O(n*k).
            _candidates.Sort(CompareProjectedEnergyDescending);
        }

        for (var i = 0; i < directCount; i++)
        {
            UpdateProjectedLight(_candidates[i], currentFrame);
        }

        UpdateProjectedLight(MergeOverflowCandidates(directCount), currentFrame);
    }

    private static bool ShouldPartiallySelectCandidates(int candidateCount, int directCount)
    {
        return directCount > 0 &&
               candidateCount > directCount * PartialSelectionSortMultiplier;
    }

    private void SelectTopCandidates(int directCount)
    {
        for (var i = 0; i < directCount; i++)
        {
            var bestIndex = i;
            for (var j = i + 1; j < _candidates.Count; j++)
            {
                if (_candidates[j].ProjectedEnergy > _candidates[bestIndex].ProjectedEnergy)
                    bestIndex = j;
            }

            if (bestIndex == i)
                continue;

            (_candidates[i], _candidates[bestIndex]) = (_candidates[bestIndex], _candidates[i]);
        }
    }

    private static int CompareProjectedEnergyDescending(ProjectedLightCandidate left, ProjectedLightCandidate right)
    {
        return right.ProjectedEnergy.CompareTo(left.ProjectedEnergy);
    }

    private ProjectedLightCandidate MergeOverflowCandidates(int startIndex)
    {
        var first = _candidates[startIndex];
        var weightedOpening = Vector2.Zero;
        var weightedProjection = Vector2.Zero;
        var weightedColor = Vector4.Zero;
        var weightedSoftness = 0f;
        var totalWeight = 0f;
        var maxEnergy = 0f;
        var maxRadius = 0f;

        for (var i = startIndex; i < _candidates.Count; i++)
        {
            var candidate = _candidates[i];
            var weight = Math.Max(candidate.ProjectedEnergy, 0.001f);
            weightedOpening += candidate.OpeningCenter * weight;
            weightedProjection += candidate.ProjectedCenter * weight;
            weightedColor += candidate.Color.RGBA * weight;
            weightedSoftness += candidate.Softness * weight;
            totalWeight += weight;
            maxEnergy = Math.Max(maxEnergy, candidate.ProjectedEnergy);
            maxRadius = Math.Max(maxRadius, candidate.ProjectedRadius);
        }

        return new ProjectedLightCandidate(
            EntityUid.Invalid,
            first.SourceMapId,
            first.ReceivingMapId,
            first.DepthOffset,
            weightedOpening / totalWeight,
            weightedProjection / totalWeight,
            maxRadius,
            maxEnergy,
            new Color(weightedColor / totalWeight),
            weightedSoftness / totalWeight,
            true);
    }

    private void CollectCandidates(
        List<SourceLight> sourceLights,
        Entity<CMUZLevelMapComponent> adjacentMap,
        MapId adjacentMapId,
        EntityUid playerMapUid,
        MapId playerMapId,
        EntityUid currentViewOpeningMapUid,
        MapId currentViewOpeningMapId,
        int depthOffset,
        float attenuationPerDepth,
        float attenuationPerTile,
        float radiusScale,
        float maxRadius,
        float minEnergy,
        int maxOpeningsPerSource)
    {
        var openingMap = GetOpeningMapForProjection(adjacentMap, playerMapUid, depthOffset);
        if (!_mapQuery.TryComp(openingMap, out var openingMapComp) ||
            openingMapComp.MapId == MapId.Nullspace)
        {
            return;
        }

        var openingMapIsCurrentView =
            openingMap == currentViewOpeningMapUid &&
            openingMapComp.MapId == currentViewOpeningMapId;
        var useCurrentViewOpenings =
            openingMapIsCurrentView &&
            CanUseCurrentViewOpeningBoundsFilter();

        foreach (var sourceLight in sourceLights)
        {
            if (openingMapIsCurrentView &&
                !SourceLightCanReachCurrentViewOpening(sourceLight))
            {
                LastProjectedLightingDebugStats.LightsRejectedByOpeningBounds++;
                continue;
            }

            _tempOpenings.Clear();
            if (useCurrentViewOpenings)
            {
                LastProjectedLightingDebugStats.OpeningSearchesSkippedByPortal++;
                if (CanUseCurrentViewOpeningBoundsForPortal())
                    AddCurrentViewOpeningsNearSource(sourceLight, _tempOpenings);
                else
                    AddCurrentViewPortalRegionsNearSource(sourceLight, _tempOpenings);

                LastProjectedLightingDebugStats.PortalOpeningCandidates += _tempOpenings.Count;
            }
            else
            {
                LastProjectedLightingDebugStats.OpeningSearches++;
                FindOpeningsNearPosition(
                    openingMapComp.MapId,
                    sourceLight.WorldPosition,
                    sourceLight.Radius,
                    _tempOpenings);
                LastProjectedLightingDebugStats.OpeningsFound += _tempOpenings.Count;

                if (openingMapIsCurrentView)
                {
                    var beforeFilter = _tempOpenings.Count;
                    FilterTempOpeningsToCurrentView();
                    LastProjectedLightingDebugStats.OpeningsRejectedByCurrentView += beforeFilter - _tempOpenings.Count;
                }
            }

            CapTempOpeningsPerSource(maxOpeningsPerSource);

            if (_tempOpenings.Count == 0)
                continue;

            _sourceCandidates.Clear();
            foreach (var (openingCenter, sourceToOpeningDistance) in _tempOpenings)
            {
                // 1. Light Source Occlusion (Top-Down Blockage)
                var rayDirection = openingCenter - sourceLight.WorldPosition;
                var rayLength = rayDirection.Length();
                if (rayLength > 0.01f)
                {
                    LastProjectedLightingDebugStats.Raycasts++;
                    var ray = new CollisionRay(sourceLight.WorldPosition, rayDirection.Normalized(), (int) CollisionGroup.Opaque);
                    var blocked = false;
                    foreach (var _ in _physics.IntersectRay(adjacentMapId, ray, rayLength, ignoredEnt: sourceLight.Entity, returnOnFirstHit: true))
                    {
                        blocked = true;
                        break;
                    }

                    if (blocked)
                    {
                        continue;
                    }
                }

                // Smooth attenuation keeps the projected leak from becoming brighter than the source.
                var depth = Math.Abs(depthOffset);
                var s = Math.Clamp(sourceToOpeningDistance / sourceLight.Radius, 0f, 1f);
                var s2 = s * s;
                var numerator = (1f - s2) * (1f - s2);
                var denominator = 1f + attenuationPerDepth * depth + attenuationPerTile * sourceToOpeningDistance;
                var factor = numerator / denominator;
                var projectedEnergy = sourceLight.Energy * factor;

                if (projectedEnergy < minEnergy)
                    continue;

                var remainingDistance = sourceLight.Radius - sourceToOpeningDistance;
                if (remainingDistance <= 0f)
                    continue;

                // Keep the bright point near the opening, but give it enough radius to carry the
                // remaining source-light edge outward from the opening.
                var projectedRadius = Math.Min(remainingDistance * radiusScale, maxRadius);
                if (projectedRadius <= 0f)
                    continue;

                var projectedCenter = openingCenter;
                if (rayLength > 0.01f)
                    projectedCenter += rayDirection / rayLength * Math.Min(projectedRadius, MaxProjectedCenterOffset);

                var candidate = new ProjectedLightCandidate(
                    sourceLight.Entity,
                    adjacentMapId,
                    playerMapId,
                    depthOffset,
                    openingCenter,
                    projectedCenter,
                    projectedRadius,
                    projectedEnergy,
                    sourceLight.Color,
                    sourceLight.Softness);

                _sourceCandidates.Add(candidate);
                LastProjectedLightingDebugStats.Candidates++;
            }

            if (_sourceCandidates.Count > 0)
                AddSourceCandidates();
        }
    }

    private static EntityUid GetOpeningMapForProjection(
        Entity<CMUZLevelMapComponent> sourceMap,
        EntityUid receivingMap,
        int depthOffset)
    {
        // Holes are floor apertures on the higher level. When the source light is above
        // the receiver, use the source map; when it is below, use the receiver map.
        return depthOffset > 0 ? sourceMap.Owner : receivingMap;
    }

    private void RebuildOpeningCandidateBuckets()
    {
        ClearOpeningCandidateBuckets();

        for (var i = 0; i < _sourceCandidates.Count; i++)
        {
            var bucketKey = GetOpeningCandidateBucketKey(_sourceCandidates[i].OpeningCenter);
            if (!_openingCandidateBuckets.TryGetValue(bucketKey, out var bucket))
            {
                bucket = RentOpeningCandidateBucket();
                _openingCandidateBuckets[bucketKey] = bucket;
            }

            bucket.Add(i);
        }
    }

    private List<int> RentOpeningCandidateBucket()
    {
        if (_openingCandidateBucketPool.Count == 0)
            return new List<int>();

        var bucket = _openingCandidateBucketPool[^1];
        _openingCandidateBucketPool.RemoveAt(_openingCandidateBucketPool.Count - 1);
        return bucket;
    }

    private void ClearOpeningCandidateBuckets()
    {
        foreach (var bucket in _openingCandidateBuckets.Values)
        {
            bucket.Clear();
            _openingCandidateBucketPool.Add(bucket);
        }

        _openingCandidateBuckets.Clear();
    }

    private void AddSourceCandidates()
    {
        RebuildOpeningCandidateBuckets();

        _visitedSourceCandidates.Clear();
        for (var i = 0; i < _sourceCandidates.Count; i++)
        {
            _visitedSourceCandidates.Add(false);
        }

        for (var i = 0; i < _sourceCandidates.Count; i++)
        {
            if (_visitedSourceCandidates[i])
                continue;

            _componentCandidates.Clear();
            _candidateStack.Clear();
            _candidateStack.Add(i);
            _visitedSourceCandidates[i] = true;

            while (_candidateStack.Count > 0)
            {
                var index = _candidateStack[^1];
                _candidateStack.RemoveAt(_candidateStack.Count - 1);

                var candidate = _sourceCandidates[index];
                _componentCandidates.Add(candidate);

                QueueConnectedOpeningCandidates(candidate);
            }

            AddOpeningComponentCandidates(_componentCandidates);
        }

        ClearOpeningCandidateBuckets();
    }

    private void QueueConnectedOpeningCandidates(ProjectedLightCandidate candidate)
    {
        var bucketKey = GetOpeningCandidateBucketKey(candidate.OpeningCenter);
        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                var neighborKey = new OpeningCandidateBucketKey(bucketKey.X + x, bucketKey.Y + y);
                if (!_openingCandidateBuckets.TryGetValue(neighborKey, out var indexes))
                    continue;

                foreach (var index in indexes)
                {
                    if (_visitedSourceCandidates[index] ||
                        !AreConnectedOpenings(candidate, _sourceCandidates[index]))
                    {
                        continue;
                    }

                    _visitedSourceCandidates[index] = true;
                    _candidateStack.Add(index);
                }
            }
        }
    }

    private static OpeningCandidateBucketKey GetOpeningCandidateBucketKey(Vector2 openingCenter)
    {
        return new OpeningCandidateBucketKey(
            (int) MathF.Floor(openingCenter.X / OpeningConnectionDistance),
            (int) MathF.Floor(openingCenter.Y / OpeningConnectionDistance));
    }

    private static bool AreConnectedOpenings(ProjectedLightCandidate left, ProjectedLightCandidate right)
    {
        return Vector2.DistanceSquared(left.OpeningCenter, right.OpeningCenter) <=
               OpeningConnectionDistance * OpeningConnectionDistance;
    }

    private void AddOpeningComponentCandidates(List<ProjectedLightCandidate> component)
    {
        if (component.Count < MinStripCandidateCount ||
            !TryAddStripCandidates(component))
        {
            AddSeparatedCandidates(component, 1f);
        }
    }

    private bool TryAddStripCandidates(List<ProjectedLightCandidate> component)
    {
        if (!TryGetStripAxis(component, out var axis, out var minAlong, out var maxAlong))
            return false;

        _alongAxisComparer.Axis = axis;
        component.Sort(_alongAxisComparer);

        var length = maxAlong - minAlong;
        var sampleCount = Math.Clamp(
            (int) MathF.Ceiling(length / StripSampleSpacing) + 1,
            2,
            Math.Min(component.Count, MaxStripSamples));
        var energyScale = 1f / MathF.Sqrt(sampleCount);

        for (var i = 0; i < sampleCount; i++)
        {
            var index = sampleCount == 1
                ? 0
                : (int) MathF.Round(i * (component.Count - 1) / (sampleCount - 1f));
            var baseCandidate = component[Math.Clamp(index, 0, component.Count - 1)];
            var candidate = baseCandidate with
            {
                ProjectedEnergy = baseCandidate.ProjectedEnergy * energyScale,
            };

            if (OverlapsAcceptedCandidate(candidate))
                continue;

            _candidates.Add(candidate);
        }

        return true;
    }

    private static bool TryGetStripAxis(
        List<ProjectedLightCandidate> component,
        out Vector2 axis,
        out float minAlong,
        out float maxAlong)
    {
        axis = Vector2.UnitX;
        minAlong = 0f;
        maxAlong = 0f;

        var mean = Vector2.Zero;
        foreach (var candidate in component)
        {
            mean += candidate.OpeningCenter;
        }

        mean /= component.Count;

        var xx = 0f;
        var xy = 0f;
        var yy = 0f;
        foreach (var candidate in component)
        {
            var delta = candidate.OpeningCenter - mean;
            xx += delta.X * delta.X;
            xy += delta.X * delta.Y;
            yy += delta.Y * delta.Y;
        }

        var angle = 0.5f * MathF.Atan2(2f * xy, xx - yy);
        axis = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var perpendicular = new Vector2(-axis.Y, axis.X);

        minAlong = float.MaxValue;
        maxAlong = float.MinValue;
        var minAcross = float.MaxValue;
        var maxAcross = float.MinValue;

        foreach (var candidate in component)
        {
            var relative = candidate.OpeningCenter - mean;
            var along = Vector2.Dot(relative, axis);
            var across = Vector2.Dot(relative, perpendicular);
            minAlong = Math.Min(minAlong, along);
            maxAlong = Math.Max(maxAlong, along);
            minAcross = Math.Min(minAcross, across);
            maxAcross = Math.Max(maxAcross, across);
        }

        var length = maxAlong - minAlong;
        var width = Math.Max(maxAcross - minAcross, 0.001f);
        return length >= MinStripLength && length / width >= StripLinearityRatio;
    }

    private void AddSeparatedCandidates(List<ProjectedLightCandidate> candidates, float energyScale)
    {
        candidates.Sort(CompareProjectedEnergyDescending);

        foreach (var candidate in candidates)
        {
            var scaledCandidate = candidate with
            {
                ProjectedEnergy = candidate.ProjectedEnergy * energyScale,
            };

            if (OverlapsAcceptedCandidate(scaledCandidate))
                continue;

            _candidates.Add(scaledCandidate);
        }
    }

    private bool OverlapsAcceptedCandidate(ProjectedLightCandidate candidate)
    {
        foreach (var accepted in _candidates)
        {
            if (accepted.SourceLight != candidate.SourceLight ||
                accepted.DepthOffset != candidate.DepthOffset)
            {
                continue;
            }

            var minSeparation = Math.Max(0.75f, Math.Min(candidate.ProjectedRadius, accepted.ProjectedRadius) * 0.5f);
            if (Vector2.DistanceSquared(candidate.ProjectedCenter, accepted.ProjectedCenter) < minSeparation * minSeparation)
                return true;
        }

        return false;
    }

    private bool SourceLightCanReachCurrentViewOpening(SourceLight sourceLight)
    {
        if (_currentViewOpeningBounds.Count == 0)
            return LastProjectedLightingDebugStats.CurrentOpeningLosConservativeFallback;

        if (!CanUseCurrentViewOpeningBoundsFilter())
            return true;

        var reachPadding = sourceLight.Radius + ViewBoundsLightPadding;
        if (!_combinedCurrentViewOpeningBounds.Enlarged(reachPadding).Contains(sourceLight.WorldPosition))
            return false;

        var bounds = _portalOpeningCandidateBounds.Count > 0
            ? _portalOpeningCandidateBounds
            : _currentViewOpeningBounds;
        foreach (var openingBounds in bounds)
        {
            if (openingBounds.Enlarged(reachPadding).Contains(sourceLight.WorldPosition))
                return true;
        }

        return false;
    }

    private bool CanUseCurrentViewOpeningBoundsFilter()
    {
        return _currentViewOpeningBounds.Count > 0 &&
               !LastProjectedLightingDebugStats.CurrentOpeningBoundsTruncated &&
               !LastProjectedLightingDebugStats.CurrentOpeningLosConservativeFallback;
    }

    private bool CanUseCurrentViewOpeningBoundsForPortal()
    {
        return CanUseCurrentViewOpeningBoundsFilter() &&
               LastProjectedLightingDebugStats.CurrentOpeningBoundsComplete;
    }

    private void AddCurrentViewPortalRegionsNearSource(
        SourceLight sourceLight,
        List<(Vector2 Center, float Distance)> openings)
    {
        var radiusSquared = sourceLight.Radius * sourceLight.Radius;
        foreach (var openingBounds in _portalOpeningCandidateBounds)
        {
            if (!openingBounds.Enlarged(sourceLight.Radius).Contains(sourceLight.WorldPosition))
                continue;

            var openingCenter = openingBounds.ClosestPoint(sourceLight.WorldPosition);
            var distanceSquared = Vector2.DistanceSquared(sourceLight.WorldPosition, openingCenter);
            if (distanceSquared > radiusSquared)
                continue;

            openings.Add((openingCenter, MathF.Sqrt(distanceSquared)));
        }
    }

    private void AddCurrentViewOpeningsNearSource(
        SourceLight sourceLight,
        List<(Vector2 Center, float Distance)> openings)
    {
        var radiusSquared = sourceLight.Radius * sourceLight.Radius;
        foreach (var openingBounds in _currentViewOpeningBounds)
        {
            if (!openingBounds.Enlarged(sourceLight.Radius).Contains(sourceLight.WorldPosition))
                continue;

            var openingCenter = openingBounds.Center;
            var distanceSquared = Vector2.DistanceSquared(sourceLight.WorldPosition, openingCenter);
            if (distanceSquared > radiusSquared)
                continue;

            openings.Add((openingCenter, MathF.Sqrt(distanceSquared)));
        }
    }

    private void FilterTempOpeningsToCurrentView()
    {
        if (!CanUseCurrentViewOpeningBoundsFilter())
        {
            return;
        }

        for (var i = _tempOpenings.Count - 1; i >= 0; i--)
        {
            if (CurrentViewContainsOpening(_tempOpenings[i].Center))
                continue;

            _tempOpenings.RemoveAt(i);
        }
    }

    private void CapTempOpeningsPerSource(int maxOpeningsPerSource)
    {
        if (maxOpeningsPerSource <= 0 ||
            _tempOpenings.Count <= maxOpeningsPerSource)
        {
            return;
        }

        _tempOpenings.Sort(CompareOpeningDistance);
        var rejected = _tempOpenings.Count - maxOpeningsPerSource;
        _tempOpenings.RemoveRange(maxOpeningsPerSource, rejected);
        LastProjectedLightingDebugStats.OpeningsRejectedBySourceCap += rejected;
    }

    private static int CompareOpeningDistance(
        (Vector2 Center, float Distance) left,
        (Vector2 Center, float Distance) right)
    {
        return left.Distance.CompareTo(right.Distance);
    }

    private bool CurrentViewContainsOpening(Vector2 openingCenter)
    {
        if (!_combinedCurrentViewOpeningBounds.Contains(openingCenter))
            return false;

        foreach (var openingBounds in _currentViewOpeningBounds)
        {
            if (openingBounds.Contains(openingCenter))
                return true;
        }

        return false;
    }

    private void FindOpeningsNearPosition(
        MapId openingMapId,
        Vector2 sourcePosition,
        float searchRadius,
        List<(Vector2 Center, float Distance)> openings)
    {
        _zLevels.OpeningCache.FindOpeningCentersNear(
            openingMapId,
            sourcePosition,
            searchRadius,
            openings,
            _openingGrids,
            _mapManager,
            _map,
            _transform,
            _tile);
    }

    private void UpdateProjectedLight(ProjectedLightCandidate candidate, uint currentFrame)
    {
        LastProjectedLightingDebugStats.ProjectedLightsApplied++;
        var projectedUid = GetOrCreateProjectedLight(candidate);

        if (_pointLightQuery.TryComp(projectedUid, out var light))
        {
            _lights.SetRadius(projectedUid, candidate.ProjectedRadius, light);
            _lights.SetEnergy(projectedUid, candidate.ProjectedEnergy, light);
            _lights.SetColor(projectedUid, candidate.Color, light);
            _lights.SetSoftness(projectedUid, candidate.Softness, light);
            _lights.SetCastShadows(projectedUid, false, light);
            _lights.SetEnabled(projectedUid, true, light);
        }
        else
        {
            _lights.SetRadius(projectedUid, candidate.ProjectedRadius);
            _lights.SetEnergy(projectedUid, candidate.ProjectedEnergy);
            _lights.SetColor(projectedUid, candidate.Color);
            _lights.SetSoftness(projectedUid, candidate.Softness);
            _lights.SetCastShadows(projectedUid, false);
            _lights.SetEnabled(projectedUid, true);
        }

        if (_projectedQuery.TryComp(projectedUid, out var projected))
        {
            if (projected.LastAppliedMapId != candidate.ReceivingMapId ||
                projected.LastAppliedCenter != candidate.ProjectedCenter)
            {
                _transform.SetMapCoordinates(projectedUid, new MapCoordinates(candidate.ProjectedCenter, candidate.ReceivingMapId));
                projected.LastAppliedMapId = candidate.ReceivingMapId;
                projected.LastAppliedCenter = candidate.ProjectedCenter;
            }

            projected.OpeningCenter = candidate.OpeningCenter;
            projected.LastActiveFrame = currentFrame;
            projected.LastActiveTime = _timing.CurTime;
            projected.LastProjectedEnergy = candidate.ProjectedEnergy;
            projected.SourceMapId = candidate.SourceMapId;
            projected.DepthOffset = candidate.DepthOffset;
        }
        else
        {
            _transform.SetMapCoordinates(projectedUid, new MapCoordinates(candidate.ProjectedCenter, candidate.ReceivingMapId));
        }

        _activeThisFrame.Add(projectedUid);
    }

    private EntityUid GetOrCreateProjectedLight(ProjectedLightCandidate candidate)
    {
        EntityUid projectedUid;
        var key = new ProjectedLightKey(candidate.SourceLight, candidate.ReceivingMapId, candidate.OpeningCenter);
        var mergedKey = new MergedProjectedLightKey(candidate.ReceivingMapId, candidate.DepthOffset);
        var hasProjectedLight = candidate.IsMerged
            ? _mergedProjectedLights.TryGetValue(mergedKey, out projectedUid)
            : _projectedLights.TryGetValue(key, out projectedUid);

        if (!hasProjectedLight || !Exists(projectedUid))
        {
            projectedUid = Spawn(null, new MapCoordinates(candidate.ProjectedCenter, candidate.ReceivingMapId));
            var projectedComp = AddComp<CMUProjectedLightComponent>(projectedUid);
            projectedComp.SourceLight = candidate.SourceLight;
            projectedComp.SourceMapId = candidate.SourceMapId;
            projectedComp.DepthOffset = candidate.DepthOffset;
            projectedComp.OpeningCenter = candidate.OpeningCenter;
            projectedComp.LastAppliedMapId = candidate.ReceivingMapId;
            projectedComp.LastAppliedCenter = candidate.ProjectedCenter;

            AddComp<PointLightComponent>(projectedUid);

            if (candidate.IsMerged)
                _mergedProjectedLights[mergedKey] = projectedUid;
            else
                _projectedLights[key] = projectedUid;
        }

        return projectedUid;
    }

    private int CleanupStaleProjectedLights(float visibilityGraceSeconds)
    {
        var removed = 0;
        _toRemove.Clear();
        foreach (var (key, projectedUid) in _projectedLights)
        {
            if (_activeThisFrame.Contains(projectedUid))
                continue;

            if (TryKeepStaleProjectedLight(projectedUid, visibilityGraceSeconds))
                continue;

            _toRemove.Add(key);
            if (Exists(projectedUid))
            {
                Del(projectedUid);
                removed++;
            }
        }

        foreach (var key in _toRemove)
        {
            _projectedLights.Remove(key);
        }

        _mergedToRemove.Clear();
        foreach (var (key, projectedUid) in _mergedProjectedLights)
        {
            if (_activeThisFrame.Contains(projectedUid))
                continue;

            if (TryKeepStaleProjectedLight(projectedUid, visibilityGraceSeconds))
                continue;

            _mergedToRemove.Add(key);
            if (Exists(projectedUid))
            {
                Del(projectedUid);
                removed++;
            }
        }

        foreach (var key in _mergedToRemove)
        {
            _mergedProjectedLights.Remove(key);
        }

        return removed;
    }

    private bool TryKeepStaleProjectedLight(EntityUid projectedUid, float visibilityGraceSeconds)
    {
        if (visibilityGraceSeconds <= 0f ||
            !_projectedQuery.TryComp(projectedUid, out var projected))
        {
            return false;
        }

        var elapsedSeconds = Math.Max(0f, (float) (_timing.CurTime - projected.LastActiveTime).TotalSeconds);
        if (elapsedSeconds >= visibilityGraceSeconds)
            return false;

        var fade = 1f - elapsedSeconds / visibilityGraceSeconds;
        var energy = projected.LastProjectedEnergy * fade;
        if (energy <= 0.001f)
            return false;

        if (_pointLightQuery.TryComp(projectedUid, out var light))
        {
            _lights.SetEnergy(projectedUid, energy, light);
            _lights.SetEnabled(projectedUid, true, light);
        }
        else
        {
            _lights.SetEnergy(projectedUid, energy);
            _lights.SetEnabled(projectedUid, true);
        }

        LastProjectedLightingDebugStats.ProjectedLightsHeldByVisibilityGrace++;
        return true;
    }

    private int CleanupAllProjectedLights()
    {
        var removed = 0;
        foreach (var (_, projectedUid) in _projectedLights)
        {
            if (Exists(projectedUid))
            {
                Del(projectedUid);
                removed++;
            }
        }

        foreach (var (_, projectedUid) in _mergedProjectedLights)
        {
            if (Exists(projectedUid))
            {
                Del(projectedUid);
                removed++;
            }
        }

        _projectedLights.Clear();
        _mergedProjectedLights.Clear();
        _activeThisFrame.Clear();
        _currentViewOpeningBounds.Clear();
        _cachedCurrentViewOpeningBounds.Clear();
        _lightTreeResults.Clear();
        _queriedSourceLightMaps.Clear();
        _currentViewOpeningBoundsComplete = false;
        _cachedCurrentViewOpeningBoundsComplete = false;
        _combinedCurrentViewOpeningBounds = default;
        _cachedCombinedCurrentViewOpeningBounds = default;
        _currentViewOpeningGraceMapId = MapId.Nullspace;
        _currentViewOpeningGraceUntil = TimeSpan.Zero;
        ClearSourceLightBuckets();
        ClearOpeningCandidateBuckets();

        return removed;
    }

    private int GetActiveProjectedLightCount()
    {
        return _projectedLights.Count + _mergedProjectedLights.Count;
    }

    private static double GetElapsedMilliseconds(long start)
    {
        return (SysStopwatch.GetTimestamp() - start) * 1000d / SysStopwatch.Frequency;
    }

    /// <inheritdoc />
    public override void Shutdown()
    {
        base.Shutdown();
        CleanupAllProjectedLights();
    }

    private readonly record struct ProjectedLightKey(
        EntityUid SourceLight,
        MapId ReceivingMapId,
        Vector2 OpeningCenter);

    private readonly record struct MergedProjectedLightKey(
        MapId ReceivingMapId,
        int DepthOffset);

    private readonly record struct OpeningCandidateBucketKey(
        int X,
        int Y);

    private readonly record struct SourceLight(
        EntityUid Entity,
        Vector2 WorldPosition,
        float Radius,
        float Energy,
        Color Color,
        float Softness);

    private readonly record struct ProjectedLightCandidate(
        EntityUid SourceLight,
        MapId SourceMapId,
        MapId ReceivingMapId,
        int DepthOffset,
        Vector2 OpeningCenter,
        Vector2 ProjectedCenter,
        float ProjectedRadius,
        float ProjectedEnergy,
        Color Color,
        float Softness,
        bool IsMerged = false);

    private sealed class ProjectedLightAlongAxisComparer : IComparer<ProjectedLightCandidate>
    {
        public Vector2 Axis;

        public int Compare(ProjectedLightCandidate left, ProjectedLightCandidate right)
        {
            return Vector2.Dot(left.OpeningCenter, Axis).CompareTo(Vector2.Dot(right.OpeningCenter, Axis));
        }
    }

    internal sealed class ProjectedLightingDebugStats
    {
        public int Sequence;
        public bool Ran;
        public string SkipReason = "not updated";
        public bool VisibleCurrentOpenings;
        public bool UpperSourceOpenings;
        public bool RenderVisibilityGateValid;
        public readonly List<int> RenderedLowerDepths = new();
        public bool CurrentOpeningQueryFoundOpening;
        public bool CurrentOpeningBoundsComplete;
        public bool CurrentOpeningBoundsTruncated;
        public bool CurrentOpeningBoundsFromGrace;
        public bool CurrentOpeningLosConservativeFallback;
        public string CurrentOpeningLosMode = "none";
        public int CurrentOpeningBounds;
        public int CurrentOpeningLosChecks;
        public double CurrentOpeningGraceRemainingMs;
        public int SourceMapsChecked;
        public int SourceQueries;
        public int SourceMapsSkippedByRenderVisibility;
        public int PortalLightQueryBounds;
        public int PortalLightQueries;
        public int PortalLightsAccepted;
        public int PortalOpeningCandidateBounds;
        public int LightsScanned;
        public int LightsAccepted;
        public int LightsRejectedBySourceCap;
        public int LightsRejectedByOpeningBounds;
        public int OpeningSearches;
        public int OpeningSearchesSkippedByPortal;
        public int OpeningsFound;
        public int PortalOpeningCandidates;
        public int OpeningsRejectedByCurrentView;
        public int OpeningsRejectedBySourceCap;
        public int Raycasts;
        public int Candidates;
        public int LowerSourcePassesSkippedByRenderVisibility;
        public int LowerReceiverPassesSkippedByRenderVisibility;
        public int ProjectedLightsApplied;
        public int ProjectedLightsHeldByVisibilityGrace;
        public int ActiveProjectedLights;
        public int CleanupCount;
        public float VisibilityGraceSeconds;
        public double TotalMs;
        public double CurrentOpeningMs;
        public double SourceQueryMs;
        public double CandidateMs;

        public void Reset()
        {
            Sequence++;
            Ran = false;
            SkipReason = "not updated";
            VisibleCurrentOpenings = false;
            UpperSourceOpenings = false;
            RenderVisibilityGateValid = false;
            RenderedLowerDepths.Clear();
            CurrentOpeningQueryFoundOpening = false;
            CurrentOpeningBoundsComplete = false;
            CurrentOpeningBoundsTruncated = false;
            CurrentOpeningBoundsFromGrace = false;
            CurrentOpeningLosConservativeFallback = false;
            CurrentOpeningLosMode = "none";
            CurrentOpeningBounds = 0;
            CurrentOpeningLosChecks = 0;
            CurrentOpeningGraceRemainingMs = 0d;
            SourceMapsChecked = 0;
            SourceQueries = 0;
            SourceMapsSkippedByRenderVisibility = 0;
            PortalLightQueryBounds = 0;
            PortalLightQueries = 0;
            PortalLightsAccepted = 0;
            PortalOpeningCandidateBounds = 0;
            LightsScanned = 0;
            LightsAccepted = 0;
            LightsRejectedBySourceCap = 0;
            LightsRejectedByOpeningBounds = 0;
            OpeningSearches = 0;
            OpeningSearchesSkippedByPortal = 0;
            OpeningsFound = 0;
            PortalOpeningCandidates = 0;
            OpeningsRejectedByCurrentView = 0;
            OpeningsRejectedBySourceCap = 0;
            Raycasts = 0;
            Candidates = 0;
            LowerSourcePassesSkippedByRenderVisibility = 0;
            LowerReceiverPassesSkippedByRenderVisibility = 0;
            ProjectedLightsApplied = 0;
            ProjectedLightsHeldByVisibilityGrace = 0;
            ActiveProjectedLights = 0;
            CleanupCount = 0;
            VisibilityGraceSeconds = 0f;
            TotalMs = 0d;
            CurrentOpeningMs = 0d;
            SourceQueryMs = 0d;
            CandidateMs = 0d;
        }
    }
}
