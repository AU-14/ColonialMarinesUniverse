using System.Numerics;
using Content.Client.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels;
using Content.Shared.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
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

namespace Content.Client.CMU14.ZLevels.Lighting;

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
    private const float ViewBoundsLightPadding = 2f;

    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private LightTreeSystem _lightTree = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private ITileDefinitionManager _tile = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private CMUClientZLevelsSystem _zLevels = default!;

    internal static ProjectedLightingDebugStats LastProjectedLightingDebugStats { get; } = new();

    /// <summary>
    /// Cache of source light and stable grid/tile aperture identity to a reusable projected entity.
    /// </summary>
    private readonly Dictionary<ProjectedLightKey, EntityUid> _projectedLights = new();

    private readonly HashSet<EntityUid> _activeThisFrame = new();
    private readonly List<ProjectedLightCandidate> _candidates = new();
    private readonly List<ProjectedLightCandidate> _sourceCandidates = new();
    private readonly List<ProjectedLightCandidate> _componentCandidates = new();
    private readonly List<int> _candidateStack = new();
    private readonly List<bool> _visitedSourceCandidates = new();
    private List<Entity<MapGridComponent>> _openingGrids = new();
    private readonly List<ProjectedLightKey> _toRemove = new();
    private readonly List<CMUZOpeningPortal> _tempOpenings = new();
    private readonly List<Box2> _currentViewOpeningBounds = new();
    private readonly HashSet<Entity<SharedPointLightComponent, TransformComponent>> _lightTreeResults = new();
    private readonly HashSet<EntityUid> _sourceLightSeen = new();
    private readonly List<Box2> _portalLightQueryBounds = new();
    private readonly List<Box2> _portalOpeningCandidateBounds = new();
    private readonly List<Box2> _cachedCurrentViewOpeningBounds = new();
    private readonly HashSet<MapId> _queriedSourceLightMaps = new();
    private readonly Dictionary<MapId, List<SourceLight>> _sourceLightBuckets = new();
    private readonly List<MapId> _unusedSourceMaps = new();
    private readonly Dictionary<OpeningCandidateBucketKey, List<int>> _openingCandidateBuckets = new();
    private readonly List<List<int>> _openingCandidateBucketPool = new();
    private readonly ProjectedLightAlongAxisComparer _alongAxisComparer = new();
    private Box2 _combinedCurrentViewOpeningBounds;
    private Box2 _cachedCombinedCurrentViewOpeningBounds;
    private TimeSpan _currentViewOpeningGraceUntil = TimeSpan.Zero;
    private MapId _currentViewOpeningGraceMapId = MapId.Nullspace;
    private bool _currentViewOpeningBoundsComplete;
    private bool _cachedCurrentViewOpeningBoundsComplete;
    private bool _currentViewOpeningConservativeFallback;
    private bool _portalLightQueryBoundsReady;
    private bool _diagnosticsEnabled;
    private int _sourceCandidateStart;

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
        Subs.CVar(_config, CMUZLevelsCVars.ClientDiagnosticsEnabled, OnDiagnosticsChanged, true);
    }

    private void OnDiagnosticsChanged(bool enabled)
    {
        _diagnosticsEnabled = enabled;
        if (_diagnosticsEnabled)
            LastProjectedLightingDebugStats.Reset();
    }

    /// <inheritdoc />
    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var stats = LastProjectedLightingDebugStats;
        if (_diagnosticsEnabled)
            stats.Reset();
        var totalStart = _diagnosticsEnabled ? SysStopwatch.GetTimestamp() : 0;

        if (!_config.GetCVar(CMUZLevelsCVars.Enabled) ||
            !_config.GetCVar(CMUZLevelsCVars.RenderEnabled) ||
            !_config.GetCVar(CMUZLevelsCVars.ProjectedLightingEnabled))
        {
            if (_diagnosticsEnabled)
                stats.SkipReason = "projected lighting disabled";
            var removed = CleanupAllProjectedLights();
            if (_diagnosticsEnabled)
            {
                stats.CleanupCount += removed;
                stats.ActiveProjectedLights = GetActiveProjectedLightCount();
                stats.TotalMs = GetElapsedMilliseconds(totalStart);
            }
            return;
        }

        if (_player.LocalEntity is not { } playerUid ||
            !TryComp<CMUZLevelViewerComponent>(playerUid, out var viewer) ||
            !_xformQuery.TryComp(playerUid, out var playerXform) ||
            playerXform.MapUid is not { } playerMapUid ||
            !_mapQuery.TryComp(playerMapUid, out var playerMapComp) ||
            !_zMapQuery.TryComp(playerMapUid, out var playerZMap))
        {
            if (_diagnosticsEnabled)
                stats.SkipReason = "no local Z-level viewer";
            var removed = CleanupAllProjectedLights();
            if (_diagnosticsEnabled)
            {
                stats.CleanupCount += removed;
                stats.ActiveProjectedLights = GetActiveProjectedLightCount();
                stats.TotalMs = GetElapsedMilliseconds(totalStart);
            }
            return;
        }

        var maxPerLevel = Math.Max(0, _config.GetCVar(CMUZLevelsCVars.MaxProjectedLightsPerLevel));
        var maxGlobal = Math.Max(0, _config.GetCVar(CMUZLevelsCVars.MaxProjectedLightsGlobal));
        if (maxPerLevel == 0 || maxGlobal == 0)
        {
            if (_diagnosticsEnabled)
                stats.SkipReason = "max projected lights is zero";
            var removed = CleanupAllProjectedLights();
            if (_diagnosticsEnabled)
            {
                stats.CleanupCount += removed;
                stats.ActiveProjectedLights = GetActiveProjectedLightCount();
                stats.TotalMs = GetElapsedMilliseconds(totalStart);
            }
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
        if (_diagnosticsEnabled)
            stats.VisibilityGraceSeconds = visibilityGraceSeconds;
        _activeThisFrame.Clear();
        _candidates.Clear();

        var viewBounds = _eyeManager.GetWorldViewbounds();
        var viewAabb = viewBounds.CalcBoundingBox();
        var playerWorldPosition = _eyeManager.CurrentEye.Position.Position;
        // Lighting must not depend on whichever viewport last wrote diagnostic statistics.
        const bool useRenderVisibilityGate = false;
        var maxOpeningRects = Math.Max(0, _config.GetCVar(CMUZLevelsCVars.MaxOpeningRectsPerPass));
        var openingStart = _diagnosticsEnabled ? SysStopwatch.GetTimestamp() : 0;
        var hasCurrentViewOpening = TryUpdateCurrentViewOpenings(
            playerMapComp.MapId,
            viewAabb,
            playerWorldPosition,
            maxOpeningRects,
            visibilityGraceSeconds);
        var hasUpperSourceOpening = HasUpperSourceOpening(playerMapUid, viewAabb);
        if (_diagnosticsEnabled)
        {
            stats.CurrentOpeningMs = GetElapsedMilliseconds(openingStart);
            stats.VisibleCurrentOpenings = hasCurrentViewOpening;
            stats.UpperSourceOpenings = hasUpperSourceOpening;
            stats.CurrentOpeningBounds = _currentViewOpeningBounds.Count;
            stats.CurrentOpeningBoundsComplete = _currentViewOpeningBoundsComplete;
        }

        if (!viewer.LookUp &&
            !viewer.StairPreviewUp &&
            !hasCurrentViewOpening &&
            !hasUpperSourceOpening)
        {
            if (_diagnosticsEnabled)
                stats.SkipReason = "no visible current openings";
            _sourceLightBuckets.Clear();
            ReconcileProjectedLights(_candidates, maxPerLevel, maxGlobal, currentFrame, visibilityGraceSeconds);
            if (_diagnosticsEnabled)
            {
                stats.ActiveProjectedLights = GetActiveProjectedLightCount();
                stats.TotalMs = GetElapsedMilliseconds(totalStart);
            }
            return;
        }

        if (_diagnosticsEnabled)
        {
            stats.Ran = true;
            stats.SkipReason = "processed";
        }
        Entity<CMUZLevelMapComponent?> playerZLevelMap = (playerMapUid, playerZMap);
        var sourceStart = _diagnosticsEnabled ? SysStopwatch.GetTimestamp() : 0;
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
            Array.Empty<int>());
        if (_diagnosticsEnabled)
            stats.SourceQueryMs = GetElapsedMilliseconds(sourceStart);

        var candidateStart = _diagnosticsEnabled ? SysStopwatch.GetTimestamp() : 0;
        for (var depthOffset = -maxDepth; depthOffset <= 1; depthOffset++)
        {
            if (depthOffset == 0)
                continue;

            if (depthOffset < 0 && !projectLowerSources)
                continue;

            if (!ShouldProcessLowerProjectionDepth(depthOffset, useRenderVisibilityGate, Array.Empty<int>()))
            {
                if (_diagnosticsEnabled)
                    stats.LowerSourcePassesSkippedByRenderVisibility++;
                continue;
            }

            if (!_zLevels.TryMapOffset(playerMapUid, depthOffset, out var adjacentMap, out var adjacentMapComp) ||
                adjacentMapComp.MapId == MapId.Nullspace)
            {
                continue;
            }

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


        }

        if (projectLowerReceivers)
        {
            for (var receivingDepth = -1; receivingDepth >= -maxDepth; receivingDepth--)
            {
                if (!ShouldProcessLowerProjectionDepth(receivingDepth, useRenderVisibilityGate, Array.Empty<int>()))
                {
                    if (_diagnosticsEnabled)
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
                if (!ShouldProcessLowerProjectionDepth(sourceDepth, useRenderVisibilityGate, Array.Empty<int>()))
                {
                    if (_diagnosticsEnabled)
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


            }
        }

        ReconcileProjectedLights(_candidates, maxPerLevel, maxGlobal, currentFrame, visibilityGraceSeconds);
        if (_diagnosticsEnabled)
        {
            stats.CandidateMs = GetElapsedMilliseconds(candidateStart);
            stats.ActiveProjectedLights = GetActiveProjectedLightCount();
            stats.TotalMs = GetElapsedMilliseconds(totalStart);
        }
    }

    internal bool TryUpdateCurrentViewOpenings(
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
        _currentViewOpeningConservativeFallback = false;

        var openingLimit = maxOpeningRects is 0 or int.MaxValue
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
            _map,
            _transform,
            _tile);

        if (_diagnosticsEnabled)
            stats.CurrentOpeningQueryFoundOpening = found;

        if (_openingGrids.Count == 0)
        {
            _currentViewOpeningConservativeFallback = true;
            if (_diagnosticsEnabled)
            {
                stats.CurrentOpeningLosConservativeFallback = true;
                stats.CurrentOpeningLosMode = "no grids";
            }
            return true;
        }

        if (!found)
            return TryUseCurrentViewOpeningGrace(mapId, visibilityGraceSeconds);

        if (_currentViewOpeningBounds.Count == 0)
        {
            _currentViewOpeningConservativeFallback = true;
            if (_diagnosticsEnabled)
            {
                stats.CurrentOpeningLosConservativeFallback = true;
                stats.CurrentOpeningLosMode = "no bounds";
            }
            return true;
        }

        if (maxOpeningRects > 0 && _currentViewOpeningBounds.Count > maxOpeningRects)
        {
            _currentViewOpeningConservativeFallback = true;
            if (_diagnosticsEnabled)
            {
                stats.CurrentOpeningBoundsTruncated = true;
                stats.CurrentOpeningLosConservativeFallback = true;
                stats.CurrentOpeningLosMode = "truncated";
            }
            return true;
        }

        var visible = FilterVisibleCurrentViewOpenings(mapId, viewerPosition);
        _currentViewOpeningBoundsComplete = true;

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
        // Point samples cannot prove an aperture hidden, even when every sampled ray is blocked.
        // Keep complete real geometry for the source broad phase; receiving shadows provide occlusion.
        if (_diagnosticsEnabled)
            LastProjectedLightingDebugStats.CurrentOpeningLosMode = "conservative geometry";
        return _currentViewOpeningBounds.Count > 0;
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
        if (_diagnosticsEnabled)
        {
            stats.CurrentOpeningBoundsFromGrace = true;
            stats.CurrentOpeningLosMode = "visibility grace";
            stats.CurrentOpeningGraceRemainingMs = Math.Max(
                0d,
                (_currentViewOpeningGraceUntil - _timing.CurTime).TotalMilliseconds);
        }
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
            _map,
            _transform,
            _tile);
    }

    internal void BuildSourceLightBuckets(
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
        BuildPortalOpeningCandidateBounds();
        // Source maps share this update's aperture geometry. Rebuild lazily on the first query.
        _portalLightQueryBoundsReady = false;

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
            if (_diagnosticsEnabled)
                LastProjectedLightingDebugStats.SourceMapsSkippedByRenderVisibility++;
        }

        for (var depthOffset = -maxDepth; depthOffset <= 1; depthOffset++)
        {
            if (depthOffset == 0)
                continue;

            // Lower receivers need their immediately higher source even when upward projection is disabled.
            if (depthOffset < 0 && !includeLowerSources && !(includePlayerMap && depthOffset > -maxDepth))
                continue;

            if (!ShouldProcessLowerProjectionDepth(depthOffset, useRenderVisibilityGate, renderedLowerDepths))
            {
                if (_diagnosticsEnabled)
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
        _unusedSourceMaps.Clear();
        foreach (var mapId in _sourceLightBuckets.Keys)
        {
            if (!_queriedSourceLightMaps.Contains(mapId))
                _unusedSourceMaps.Add(mapId);
        }

        foreach (var mapId in _unusedSourceMaps)
            _sourceLightBuckets.Remove(mapId);
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
        if (_diagnosticsEnabled)
        {
            stats.SourceMapsChecked++;
            stats.SourceQueries++;
        }
        _lightTreeResults.Clear();
        _lightTree.QueryAabb(_lightTreeResults, mapId, viewBounds);
        if (_diagnosticsEnabled)
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
        if (_diagnosticsEnabled)
            stats.SourceMapsChecked++;
        if (!_portalLightQueryBoundsReady)
        {
            BuildPortalLightQueryBounds();
            _portalLightQueryBoundsReady = true;
        }
        if (_portalLightQueryBounds.Count == 0)
            return;

        _sourceLightSeen.Clear();
        foreach (var bounds in _portalLightQueryBounds)
        {
            if (_diagnosticsEnabled)
            {
                stats.SourceQueries++;
                stats.PortalLightQueries++;
            }
            _lightTreeResults.Clear();
            _lightTree.QueryAabb(_lightTreeResults, mapId, bounds);
            if (_diagnosticsEnabled)
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
                if (_diagnosticsEnabled)
                    stats.PortalLightsAccepted++;
            }
        }
    }

    internal bool TryBuildSourceLight(
        Entity<SharedPointLightComponent, TransformComponent> lightEnt,
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
            light.ContainerOccluded ||
            light.Radius <= 0f ||
            light.Energy <= 0f ||
            light.Energy < minEnergy)
        {
            return false;
        }

        sourceLight = new SourceLight(
            lightUid,
            _transform.GetWorldPosition(lightXform) + _transform.GetWorldRotation(lightXform).RotateVec(light.Offset),
            light.Radius,
            light.Energy,
            light.Color,
            light.Softness,
            light.Falloff,
            light.CurveFactor);
        return true;
    }

    private void AddSourceLight(SourceLight sourceLight, MapId mapId)
    {
        GetSourceLightBucket(mapId).Add(sourceLight);
        if (_diagnosticsEnabled)
            LastProjectedLightingDebugStats.LightsAccepted++;
    }

    private void BuildPortalLightQueryBounds()
    {
        if (_diagnosticsEnabled)
            LastProjectedLightingDebugStats.PortalLightQueryBuilds++;
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

        if (_diagnosticsEnabled)
            LastProjectedLightingDebugStats.PortalLightQueryBounds += _portalLightQueryBounds.Count;
    }

    private void BuildPortalOpeningCandidateBounds()
    {
        _portalOpeningCandidateBounds.Clear();
        if (!CanUseCurrentViewOpeningBoundsFilter())
        {
            if (_diagnosticsEnabled)
                LastProjectedLightingDebugStats.PortalOpeningCandidateBounds = 0;
            return;
        }

        foreach (var openingBounds in _currentViewOpeningBounds)
        {
            AddMergedPortalLightQueryBounds(_portalOpeningCandidateBounds, openingBounds);
        }

        if (_diagnosticsEnabled)
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
            if (_diagnosticsEnabled)
                LastProjectedLightingDebugStats.LightsRejectedBySourceCap += rejected;
        }
    }

    private static int CompareSourceLightEnergyDescending(SourceLight left, SourceLight right)
    {
        var energy = right.Energy.CompareTo(left.Energy);
        return energy != 0 ? energy : left.Entity.CompareTo(right.Entity);
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

    internal void CollectCandidates(
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
        foreach (var sourceLight in sourceLights)
        {
            if (openingMapIsCurrentView &&
                !SourceLightCanReachCurrentViewOpening(sourceLight))
            {
                if (_diagnosticsEnabled)
                    LastProjectedLightingDebugStats.LightsRejectedByOpeningBounds++;
                continue;
            }

            _tempOpenings.Clear();
            // Bounds (including merged broad-phase bounds) are never transmission geometry.
            if (_diagnosticsEnabled)
                LastProjectedLightingDebugStats.OpeningSearches++;
            FindOpeningsNearPosition(
                openingMapComp.MapId,
                sourceLight.WorldPosition,
                sourceLight.Radius,
                _tempOpenings);
            if (_diagnosticsEnabled)
                LastProjectedLightingDebugStats.OpeningsFound += _tempOpenings.Count;

            if (openingMapIsCurrentView)
            {
                var beforeFilter = _tempOpenings.Count;
                FilterTempOpeningsToCurrentView();
                if (_diagnosticsEnabled)
                    LastProjectedLightingDebugStats.OpeningsRejectedByCurrentView += beforeFilter - _tempOpenings.Count;
            }

            CapTempOpeningsPerSource(maxOpeningsPerSource);

            if (_tempOpenings.Count == 0)
                continue;

            _sourceCandidates.Clear();
            foreach (var portal in _tempOpenings)
            {
                var openingCenter = portal.Center;
                var sourceToOpeningDistance = portal.Distance;

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

                // Reject contributions with no usable energy or radius before querying world geometry.
                if (!CanTransmitThroughMaps(playerMapUid, adjacentMap.Owner, depthOffset, openingCenter))
                    continue;

                if (IsSourceRayBlocked(sourceLight, adjacentMapId, openingCenter))
                    continue;

                var candidate = new ProjectedLightCandidate(
                    sourceLight.Entity,
                    adjacentMapId,
                    playerMapId,
                    depthOffset,
                    portal.Grid,
                    portal.Tile,
                    openingCenter,
                    openingCenter,
                    projectedRadius,
                    projectedEnergy,
                    sourceLight.Color,
                    sourceLight.Softness,
                    sourceLight.Falloff,
                    sourceLight.CurveFactor);

                _sourceCandidates.Add(candidate);
                if (_diagnosticsEnabled)
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

    private bool IsSourceRayBlocked(SourceLight source, MapId mapId, Vector2 openingCenter)
    {
        var direction = openingCenter - source.WorldPosition;
        var length = direction.Length();
        if (length <= 0.01f)
            return false;

        if (_diagnosticsEnabled)
            LastProjectedLightingDebugStats.Raycasts++;
        var ray = new CollisionRay(source.WorldPosition, direction / length, (int) CollisionGroup.Opaque);
        foreach (var _ in _physics.IntersectRay(mapId, ray, length, ignoredEnt: source.Entity, returnOnFirstHit: true))
            return true;

        return false;
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
            if (_openingCandidateBucketPool.Count < 64 && bucket.Capacity <= 256)
                _openingCandidateBucketPool.Add(bucket);
        }

        _openingCandidateBuckets.Clear();
    }

    private void AddSourceCandidates()
    {
        _sourceCandidateStart = _candidates.Count;
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
            (int)MathF.Floor(openingCenter.X / OpeningConnectionDistance),
            (int)MathF.Floor(openingCenter.Y / OpeningConnectionDistance));
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
            (int)MathF.Ceiling(length / StripSampleSpacing) + 1,
            2,
            Math.Min(component.Count, MaxStripSamples));
        var energyScale = 1f / MathF.Sqrt(sampleCount);

        for (var i = 0; i < sampleCount; i++)
        {
            var index = sampleCount == 1
                ? 0
                : (int)MathF.Round(i * (component.Count - 1) / (sampleCount - 1f));
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
        for (var i = _sourceCandidateStart; i < _candidates.Count; i++)
        {
            var accepted = _candidates[i];
            if (accepted.SourceLight != candidate.SourceLight ||
                accepted.ReceivingMapId != candidate.ReceivingMapId ||
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
            return _currentViewOpeningConservativeFallback;

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
               _currentViewOpeningBoundsComplete;
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
        if (_diagnosticsEnabled)
            LastProjectedLightingDebugStats.OpeningsRejectedBySourceCap += rejected;
    }

    private static int CompareOpeningDistance(
        CMUZOpeningPortal left,
        CMUZOpeningPortal right)
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
        List<CMUZOpeningPortal> openings)
    {
        _zLevels.OpeningCache.FindOpeningPortalsNear(
            openingMapId,
            sourcePosition,
            searchRadius,
            openings,
            _openingGrids,
            _map,
            _transform,
            _tile,
            edgeOnly: false);
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

    private readonly record struct OpeningCandidateBucketKey(
        int X,
        int Y);

    internal readonly record struct SourceLight(
        EntityUid Entity,
        Vector2 WorldPosition,
        float Radius,
        float Energy,
        Color Color,
        float Softness,
        float Falloff,
        float CurveFactor);

    internal readonly record struct ProjectedLightCandidate(
        EntityUid SourceLight,
        MapId SourceMapId,
        MapId ReceivingMapId,
        int DepthOffset,
        EntityUid PortalGrid,
        Vector2i PortalTile,
        Vector2 OpeningCenter,
        Vector2 ProjectedCenter,
        float ProjectedRadius,
        float ProjectedEnergy,
        Color Color,
        float Softness,
        float Falloff,
        float CurveFactor);

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
        public int PortalLightQueryBuilds;
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
        public int TransmissionChecks;
        public int Candidates;
        public int LowerSourcePassesSkippedByRenderVisibility;
        public int LowerReceiverPassesSkippedByRenderVisibility;
        public int ProjectedLightsApplied;
        public int ProjectedLightsCreated;
        public int ProjectedLightsReused;
        public int ProjectedLightsReassigned;
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
            PortalLightQueryBuilds = 0;
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
            TransmissionChecks = 0;
            Candidates = 0;
            LowerSourcePassesSkippedByRenderVisibility = 0;
            LowerReceiverPassesSkippedByRenderVisibility = 0;
            ProjectedLightsApplied = 0;
            ProjectedLightsCreated = 0;
            ProjectedLightsReused = 0;
            ProjectedLightsReassigned = 0;
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
