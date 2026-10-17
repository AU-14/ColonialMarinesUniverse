using System.Numerics;
using Content.Shared.CMU14.ZLevels.Core;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.Client.CMU14.ZLevels.Lighting;

public sealed partial class CMUZLevelProjectedLightingSystem
{
    private readonly List<ProjectedLightCandidate> _selectedCandidates = new();
    private readonly HashSet<ProjectedLightKey> _selectedKeys = new();
    private readonly Dictionary<MapId, int> _selectedPerMap = new();
    private readonly Dictionary<MapId, int> _livePerMap = new();
    private List<Entity<MapGridComponent>> _transmissionGrids = new();

    internal int TotalCreated { get; private set; }
    internal int TotalDeleted { get; private set; }
    internal int TotalReassigned { get; private set; }
    internal int SourceBucketCount => _sourceLightBuckets.Count;
    internal IReadOnlyList<ProjectedLightCandidate> CollectedCandidates => _candidates;
    internal int GetSourceLightCount(MapId map) => _sourceLightBuckets.TryGetValue(map, out var lights) ? lights.Count : 0;

    /// <summary>
    /// Selects once across every source contribution. A stale entity remains in the same budgets as
    /// an active one, and its slot can be reused immediately when another real aperture wins selection.
    /// </summary>
    internal void ReconcileProjectedLights(
        List<ProjectedLightCandidate> candidates,
        int maxPerMap,
        int maxGlobal,
        uint currentFrame,
        float visibilityGraceSeconds)
    {
        _activeThisFrame.Clear();
        _selectedCandidates.Clear();
        _selectedKeys.Clear();
        _selectedPerMap.Clear();
        _livePerMap.Clear();

        if (maxPerMap <= 0 || maxGlobal <= 0)
        {
            var removed = CleanupAllProjectedLights();
            if (_diagnosticsEnabled)
                LastProjectedLightingDebugStats.CleanupCount += removed;
            return;
        }

        var needsSelection = candidates.Count > maxGlobal;
        foreach (var candidate in candidates)
        {
            var count = _selectedPerMap.GetValueOrDefault(candidate.ReceivingMapId) + 1;
            _selectedPerMap[candidate.ReceivingMapId] = count;
            needsSelection |= count > maxPerMap;
        }

        if (needsSelection)
            candidates.Sort(CompareProjectedEnergyDescending);

        _selectedPerMap.Clear();
        foreach (var candidate in candidates)
        {
            if (_selectedCandidates.Count >= maxGlobal)
                break;

            var count = _selectedPerMap.GetValueOrDefault(candidate.ReceivingMapId);
            if (count >= maxPerMap || !_selectedKeys.Add(KeyFor(candidate)))
                continue;

            _selectedCandidates.Add(candidate);
            _selectedPerMap[candidate.ReceivingMapId] = count + 1;
        }

        // A deleted map/source, removed component or closed aperture never receives visibility grace.
        _toRemove.Clear();
        foreach (var (key, uid) in _projectedLights)
        {
            if (!_projectedQuery.TryComp(uid, out var projected) || !_pointLightQuery.HasComp(uid) ||
                (!_selectedKeys.Contains(key) && !IsProjectedTransmissionValid(projected)))
            {
                _toRemove.Add(key);
                continue;
            }

            _livePerMap[key.ReceivingMapId] = _livePerMap.GetValueOrDefault(key.ReceivingMapId) + 1;
        }

        foreach (var key in _toRemove)
            RemoveProjectedLight(key, counted: false);

        // Also enforce smaller settings immediately, before creating or repositioning any entity.
        _toRemove.Clear();
        foreach (var (key, _) in _projectedLights)
        {
            if (!_selectedKeys.Contains(key) &&
                (_projectedLights.Count - _toRemove.Count > maxGlobal ||
                 _livePerMap[key.ReceivingMapId] > maxPerMap))
            {
                _toRemove.Add(key);
                _livePerMap[key.ReceivingMapId]--;
            }
        }

        foreach (var key in _toRemove)
            RemoveProjectedLight(key, counted: false);

        foreach (var candidate in _selectedCandidates)
        {
            var key = KeyFor(candidate);
            if (!_projectedLights.TryGetValue(key, out var uid))
            {
                var requireSameMap = _livePerMap.GetValueOrDefault(candidate.ReceivingMapId) >= maxPerMap;
                if (TryFindReusableLight(candidate.ReceivingMapId, requireSameMap, out var previous, out uid))
                {
                    _projectedLights.Remove(previous);
                    _livePerMap[previous.ReceivingMapId]--;
                    TotalReassigned++;
                    if (_diagnosticsEnabled)
                        LastProjectedLightingDebugStats.ProjectedLightsReassigned++;
                }
                else
                {
                    // The selected set fits both budgets; this guard also makes unexpected corruption safe.
                    if (requireSameMap || _projectedLights.Count >= maxGlobal)
                        continue;

                    uid = Spawn(null, new MapCoordinates(candidate.ProjectedCenter, candidate.ReceivingMapId));
                    AddComp<CMUProjectedLightComponent>(uid);
                    AddComp<PointLightComponent>(uid);
                    TotalCreated++;
                    if (_diagnosticsEnabled)
                        LastProjectedLightingDebugStats.ProjectedLightsCreated++;
                }

                _projectedLights.Add(key, uid);
                _livePerMap[candidate.ReceivingMapId] = _livePerMap.GetValueOrDefault(candidate.ReceivingMapId) + 1;
            }
            else
            {
                if (_diagnosticsEnabled)
                    LastProjectedLightingDebugStats.ProjectedLightsReused++;
            }

            UpdateProjectedLight(uid, candidate, currentFrame);
        }

        _toRemove.Clear();
        foreach (var (key, uid) in _projectedLights)
        {
            if (_activeThisFrame.Contains(uid))
                continue;

            var projected = Comp<CMUProjectedLightComponent>(uid);
            var elapsed = Math.Max(0f, (float) (_timing.CurTime - projected.LastActiveTime).TotalSeconds);
            if (visibilityGraceSeconds <= 0f || elapsed >= visibilityGraceSeconds)
            {
                _toRemove.Add(key);
                continue;
            }

            var energy = projected.LastProjectedEnergy * (1f - elapsed / visibilityGraceSeconds);
            _lights.SetEnergy(uid, energy);
            if (_diagnosticsEnabled)
                LastProjectedLightingDebugStats.ProjectedLightsHeldByVisibilityGrace++;
        }

        foreach (var key in _toRemove)
            RemoveProjectedLight(key);

        if (_diagnosticsEnabled)
            LastProjectedLightingDebugStats.ActiveProjectedLights = _projectedLights.Count;
    }

    private bool TryFindReusableLight(
        MapId receivingMap,
        bool requireSameMap,
        out ProjectedLightKey key,
        out EntityUid uid)
    {
        key = default;
        uid = default;
        var found = false;
        var lowestEnergy = float.PositiveInfinity;
        foreach (var (existingKey, existingUid) in _projectedLights)
        {
            if (_selectedKeys.Contains(existingKey) ||
                (requireSameMap && existingKey.ReceivingMapId != receivingMap))
            {
                continue;
            }

            var energy = Comp<CMUProjectedLightComponent>(existingUid).LastProjectedEnergy;
            if (found && energy >= lowestEnergy)
                continue;

            key = existingKey;
            uid = existingUid;
            lowestEnergy = energy;
            found = true;
        }

        return found;
    }

    private void UpdateProjectedLight(EntityUid uid, ProjectedLightCandidate candidate, uint currentFrame)
    {
        var projected = Comp<CMUProjectedLightComponent>(uid);
        var light = Comp<PointLightComponent>(uid);
        _lights.SetRadius(uid, candidate.ProjectedRadius, light);
        _lights.SetEnergy(uid, candidate.ProjectedEnergy, light);
        _lights.SetColor(uid, candidate.Color, light);
        _lights.SetSoftness(uid, candidate.Softness, light);
        _lights.SetFalloff(uid, candidate.Falloff, light);
        _lights.SetCurveFactor(uid, candidate.CurveFactor, light);
        _lights.SetCastShadows(uid, true, light);
        _lights.SetEnabled(uid, true, light);

        if (projected.LastAppliedMapId != candidate.ReceivingMapId || projected.LastAppliedCenter != candidate.ProjectedCenter)
            _transform.SetMapCoordinates(uid, new MapCoordinates(candidate.ProjectedCenter, candidate.ReceivingMapId));

        projected.SourceLight = candidate.SourceLight;
        projected.SourceMapId = candidate.SourceMapId;
        projected.DepthOffset = candidate.DepthOffset;
        projected.PortalGrid = candidate.PortalGrid;
        projected.PortalTile = candidate.PortalTile;
        projected.OpeningCenter = candidate.OpeningCenter;
        projected.LastAppliedMapId = candidate.ReceivingMapId;
        projected.LastAppliedCenter = candidate.ProjectedCenter;
        projected.LastActiveFrame = currentFrame;
        projected.LastActiveTime = _timing.CurTime;
        projected.LastProjectedEnergy = candidate.ProjectedEnergy;
        _activeThisFrame.Add(uid);
        if (_diagnosticsEnabled)
            LastProjectedLightingDebugStats.ProjectedLightsApplied++;
    }

    private bool IsProjectedTransmissionValid(CMUProjectedLightComponent projected)
    {
        if (!_map.TryGetMap(projected.LastAppliedMapId, out var receivingMap) ||
            !_map.TryGetMap(projected.SourceMapId, out var sourceMap) ||
            !_xformQuery.TryComp(projected.SourceLight, out var sourceXform) ||
            !_pointLightQuery.TryComp(projected.SourceLight, out var sourceLight) ||
            !TryBuildSourceLight((projected.SourceLight, sourceLight, sourceXform), projected.SourceMapId, 0f, out var source) ||
            !TryComp<MapGridComponent>(projected.PortalGrid, out var grid) ||
            !_xformQuery.TryComp(projected.PortalGrid, out var gridXform))
        {
            return false;
        }

        var openingMap = projected.DepthOffset > 0 ? sourceMap.Value : receivingMap.Value;
        if ((gridXform.MapUid != openingMap && projected.PortalGrid != openingMap) ||
            !CMUZLevelOpeningCache.IsOpeningTile((projected.PortalGrid, grid), projected.PortalTile, _map, _tile))
        {
            return false;
        }

        var center = Vector2.Transform(
            (projected.PortalTile + new Vector2(0.5f)) * grid.TileSize,
            _transform.GetWorldMatrix(projected.PortalGrid));
        return center == projected.OpeningCenter &&
               Vector2.DistanceSquared(source.WorldPosition, center) < source.Radius * source.Radius &&
               CanTransmitThroughMaps(receivingMap.Value, sourceMap.Value, projected.DepthOffset, center) &&
               !IsSourceRayBlocked(source, projected.SourceMapId, center);
    }

    /// <summary>Checks every crossed upper floor at this aperture's actual world point, including child grids.</summary>
    internal bool CanTransmitThroughMaps(EntityUid receivingMap, EntityUid sourceMap, int depthOffset, Vector2 point)
    {
        if (_diagnosticsEnabled)
            LastProjectedLightingDebugStats.TransmissionChecks++;
        if (depthOffset == 0 ||
            !_zLevels.TryMapOffset(receivingMap, depthOffset, out var actualSource, out _) ||
            actualSource?.Owner != sourceMap)
        {
            return false;
        }

        var first = depthOffset > 0 ? 1 : 0;
        var last = depthOffset > 0 ? depthOffset : depthOffset + 1;
        var step = depthOffset > 0 ? 1 : -1;
        for (var offset = first; ; offset += step)
        {
            var floorMap = receivingMap;
            if (offset != 0)
            {
                if (!_zLevels.TryMapOffset(receivingMap, offset, out var floor, out _))
                    return false;

                floorMap = floor.Value.Owner;
            }

            if (!_mapQuery.TryComp(floorMap, out var mapComponent) || !IsOpenOnAllGrids(mapComponent.MapId, point))
                return false;

            if (offset == last)
                return true;
        }
    }

    private bool IsOpenOnAllGrids(MapId mapId, Vector2 point)
    {
        _transmissionGrids.Clear();
        _map.FindGridsIntersecting(mapId, Box2.CenteredAround(point, new Vector2(0.01f)), ref _transmissionGrids, approx: true, includeMap: true);
        foreach (var grid in _transmissionGrids)
        {
            if (!CMUZLevelOpeningCache.IsOpeningTile(grid.Owner, grid.Comp, point, _map, _tile))
                return false;
        }

        return true;
    }

    private void RemoveProjectedLight(ProjectedLightKey key, bool counted = true)
    {
        if (!_projectedLights.Remove(key, out var uid))
            return;

        if (counted)
            _livePerMap[key.ReceivingMapId]--;

        if (Exists(uid))
        {
            Del(uid);
            TotalDeleted++;
            if (_diagnosticsEnabled)
                LastProjectedLightingDebugStats.CleanupCount++;
        }
    }

    internal int CleanupAllProjectedLights()
    {
        var removed = 0;
        foreach (var uid in _projectedLights.Values)
        {
            if (!Exists(uid))
                continue;

            Del(uid);
            removed++;
        }

        TotalDeleted += removed;
        _projectedLights.Clear();
        _activeThisFrame.Clear();
        _selectedKeys.Clear();
        _selectedCandidates.Clear();
        _selectedPerMap.Clear();
        _livePerMap.Clear();
        _candidates.Clear();
        _sourceCandidates.Clear();
        _componentCandidates.Clear();
        _tempOpenings.Clear();
        _currentViewOpeningBounds.Clear();
        _cachedCurrentViewOpeningBounds.Clear();
        _portalOpeningCandidateBounds.Clear();
        _portalLightQueryBounds.Clear();
        _lightTreeResults.Clear();
        _sourceLightSeen.Clear();
        _queriedSourceLightMaps.Clear();
        _openingGrids.Clear();
        _transmissionGrids.Clear();
        _sourceLightBuckets.Clear();
        _unusedSourceMaps.Clear();
        _currentViewOpeningBoundsComplete = false;
        _currentViewOpeningConservativeFallback = false;
        _portalLightQueryBoundsReady = false;
        _cachedCurrentViewOpeningBoundsComplete = false;
        _combinedCurrentViewOpeningBounds = default;
        _cachedCombinedCurrentViewOpeningBounds = default;
        _currentViewOpeningGraceMapId = MapId.Nullspace;
        _currentViewOpeningGraceUntil = TimeSpan.Zero;
        ClearOpeningCandidateBuckets();
        _openingCandidateBucketPool.Clear();
        return removed;
    }

    private int GetActiveProjectedLightCount() => _projectedLights.Count;

    private static ProjectedLightKey KeyFor(ProjectedLightCandidate candidate) =>
        new(candidate.SourceLight, candidate.ReceivingMapId, candidate.PortalGrid, candidate.PortalTile);

    private static int CompareProjectedEnergyDescending(ProjectedLightCandidate left, ProjectedLightCandidate right)
    {
        var energy = right.ProjectedEnergy.CompareTo(left.ProjectedEnergy);
        if (energy != 0)
            return energy;

        var source = left.SourceLight.CompareTo(right.SourceLight);
        if (source != 0)
            return source;

        var receiver = ((int) left.ReceivingMapId).CompareTo((int) right.ReceivingMapId);
        if (receiver != 0)
            return receiver;

        var grid = left.PortalGrid.CompareTo(right.PortalGrid);
        if (grid != 0)
            return grid;

        var x = left.PortalTile.X.CompareTo(right.PortalTile.X);
        return x != 0 ? x : left.PortalTile.Y.CompareTo(right.PortalTile.Y);
    }

    private readonly record struct ProjectedLightKey(EntityUid Source, MapId ReceivingMapId, EntityUid PortalGrid, Vector2i PortalTile);
}
