using System.Linq;
using System.Numerics;
using Content.Client._CMU14.ZLevels.Core;
using Content.Shared._CMU14.ZLevels;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Maps;
using Content.Shared.Physics;
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

    private CMUClientZLevelsSystem _zLevels = default!;

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

    private EntityQuery<CMUProjectedLightComponent> _projectedQuery;
    private EntityQuery<MapComponent> _mapQuery;
    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<CMUZLevelMapComponent> _zMapQuery;

    /// <inheritdoc />
    public override void Initialize()
    {
        base.Initialize();

        _zLevels = EntityManager.System<CMUClientZLevelsSystem>();
        _projectedQuery = GetEntityQuery<CMUProjectedLightComponent>();
        _mapQuery = GetEntityQuery<MapComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
        _zMapQuery = GetEntityQuery<CMUZLevelMapComponent>();
    }

    /// <inheritdoc />
    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!_config.GetCVar(CMUZLevelsCVars.Enabled) ||
            !_config.GetCVar(CMUZLevelsCVars.ProjectedLightingEnabled))
        {
            CleanupAllProjectedLights();
            return;
        }

        if (_player.LocalEntity is not { } playerUid ||
            !TryComp<CMUZLevelViewerComponent>(playerUid, out _) ||
            !_xformQuery.TryComp(playerUid, out var playerXform) ||
            playerXform.MapUid is not { } playerMapUid ||
            !_mapQuery.TryComp(playerMapUid, out var playerMapComp) ||
            !_zMapQuery.TryComp(playerMapUid, out var playerZMap))
        {
            CleanupAllProjectedLights();
            return;
        }

        var maxPerLevel = Math.Max(0, _config.GetCVar(CMUZLevelsCVars.MaxProjectedLightsPerLevel));
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
        _activeThisFrame.Clear();

        var viewBounds = _eyeManager.GetWorldViewbounds();

        Entity<CMUZLevelMapComponent?> playerZLevelMap = (playerMapUid, playerZMap);

        for (var depthOffset = -maxDepth; depthOffset <= 1; depthOffset++)
        {
            if (depthOffset == 0)
                continue;

            if (!_zLevels.TryMapOffset(playerMapUid, depthOffset, out var adjacentMap, out var adjacentMapComp) ||
                adjacentMapComp.MapId == MapId.Nullspace)
            {
                continue;
            }

            _candidates.Clear();
            CollectCandidates(
                adjacentMap.Value,
                adjacentMapComp.MapId,
                playerMapUid,
                playerMapComp.MapId,
                depthOffset,
                viewBounds,
                attenuationPerDepth,
                attenuationPerTile,
                radiusScale,
                maxRadius,
                minEnergy);

            ApplyLevelCap(maxPerLevel, currentFrame);
        }

        for (var receivingDepth = -1; receivingDepth >= -maxDepth; receivingDepth--)
        {
            if (!_zLevels.TryMapOffset(playerZLevelMap, receivingDepth, out var receivingMap, out var receivingMapComp))
                break;

            if (receivingMap is not { } receiving ||
                receivingMapComp.MapId == MapId.Nullspace)
            {
                continue;
            }

            var sourceDepth = receivingDepth + 1;
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
            CollectCandidates(
                sourceMap,
                sourceMapComp.MapId,
                receiving.Owner,
                receivingMapComp.MapId,
                1,
                viewBounds,
                attenuationPerDepth,
                attenuationPerTile,
                radiusScale,
                maxRadius,
                minEnergy);

            ApplyLevelCap(maxPerLevel, currentFrame);
        }

        CleanupStaleProjectedLights();
    }

    private void ApplyLevelCap(int maxPerLevel, uint currentFrame)
    {
        _candidates.Sort(static (left, right) => right.ProjectedEnergy.CompareTo(left.ProjectedEnergy));

        if (maxPerLevel == 0)
            return;

        if (_candidates.Count <= maxPerLevel)
        {
            foreach (var candidate in _candidates)
            {
                UpdateProjectedLight(candidate, currentFrame);
            }

            return;
        }

        var directCount = Math.Max(0, maxPerLevel - 1);
        for (var i = 0; i < directCount; i++)
        {
            UpdateProjectedLight(_candidates[i], currentFrame);
        }

        UpdateProjectedLight(MergeOverflowCandidates(directCount), currentFrame);
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
        Entity<CMUZLevelMapComponent> adjacentMap,
        MapId adjacentMapId,
        EntityUid playerMapUid,
        MapId playerMapId,
        int depthOffset,
        Box2Rotated viewBounds,
        float attenuationPerDepth,
        float attenuationPerTile,
        float radiusScale,
        float maxRadius,
        float minEnergy)
    {
        var openingMap = GetOpeningMapForProjection(adjacentMap, playerMapUid, depthOffset);
        if (!_mapQuery.TryComp(openingMap, out var openingMapComp) ||
            openingMapComp.MapId == MapId.Nullspace)
        {
            return;
        }

        var lightQuery = EntityQueryEnumerator<PointLightComponent, TransformComponent>();
        while (lightQuery.MoveNext(out var lightUid, out var light, out var lightXform))
        {
            if (lightXform.MapID != adjacentMapId ||
                _projectedQuery.HasComp(lightUid) ||
                !light.Enabled ||
                light.Radius <= 0f ||
                light.Energy <= 0f)
            {
                continue;
            }

            var lightWorldPos = _transform.GetWorldPosition(lightXform);
            var expandedBounds = viewBounds.Enlarged(light.Radius + 2f);
            if (!expandedBounds.Contains(lightWorldPos))
                continue;

            _tempOpenings.Clear();
            FindOpeningsNearPosition(
                openingMapComp.MapId,
                lightWorldPos,
                light.Radius,
                _tempOpenings);

            if (_tempOpenings.Count == 0)
                continue;

            _sourceCandidates.Clear();
            foreach (var (openingCenter, sourceToOpeningDistance) in _tempOpenings)
            {
                // 1. Light Source Occlusion (Top-Down Blockage)
                var rayDirection = openingCenter - lightWorldPos;
                var rayLength = rayDirection.Length();
                if (rayLength > 0.01f)
                {
                    var ray = new CollisionRay(lightWorldPos, rayDirection.Normalized(), (int) CollisionGroup.Opaque);
                    var results = _physics.IntersectRay(adjacentMapId, ray, rayLength, ignoredEnt: lightUid, returnOnFirstHit: true);
                    if (results.Any())
                    {
                        continue;
                    }
                }

                // Smooth attenuation keeps the projected leak from becoming brighter than the source.
                var depth = Math.Abs(depthOffset);
                var s = Math.Clamp(sourceToOpeningDistance / light.Radius, 0f, 1f);
                var s2 = s * s;
                var numerator = (1f - s2) * (1f - s2);
                var denominator = 1f + attenuationPerDepth * depth + attenuationPerTile * sourceToOpeningDistance;
                var factor = numerator / denominator;
                var projectedEnergy = light.Energy * factor;

                if (projectedEnergy < minEnergy)
                    continue;

                var remainingDistance = light.Radius - sourceToOpeningDistance;
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
                    lightUid,
                    adjacentMapId,
                    playerMapId,
                    depthOffset,
                    openingCenter,
                    projectedCenter,
                    projectedRadius,
                    projectedEnergy,
                    light.Color,
                    light.Softness);

                _sourceCandidates.Add(candidate);
            }

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

    private void AddSourceCandidates()
    {
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

                for (var j = 0; j < _sourceCandidates.Count; j++)
                {
                    if (_visitedSourceCandidates[j] ||
                        !AreConnectedOpenings(candidate, _sourceCandidates[j]))
                    {
                        continue;
                    }

                    _visitedSourceCandidates[j] = true;
                    _candidateStack.Add(j);
                }
            }

            AddOpeningComponentCandidates(_componentCandidates);
        }
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

        component.Sort((left, right) =>
            Vector2.Dot(left.OpeningCenter, axis).CompareTo(Vector2.Dot(right.OpeningCenter, axis)));

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
        candidates.Sort(static (left, right) => right.ProjectedEnergy.CompareTo(left.ProjectedEnergy));

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
        var projectedUid = GetOrCreateProjectedLight(candidate);

        _lights.SetRadius(projectedUid, candidate.ProjectedRadius);
        _lights.SetEnergy(projectedUid, candidate.ProjectedEnergy);
        _lights.SetColor(projectedUid, candidate.Color);
        _lights.SetSoftness(projectedUid, candidate.Softness);
        _lights.SetCastShadows(projectedUid, false);
        _lights.SetEnabled(projectedUid, true);
        _transform.SetMapCoordinates(projectedUid, new MapCoordinates(candidate.ProjectedCenter, candidate.ReceivingMapId));

        if (_projectedQuery.TryComp(projectedUid, out var projected))
        {
            projected.OpeningCenter = candidate.OpeningCenter;
            projected.LastActiveFrame = currentFrame;
            projected.SourceMapId = candidate.SourceMapId;
            projected.DepthOffset = candidate.DepthOffset;
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
            projectedUid = Spawn(null, new MapCoordinates(candidate.OpeningCenter, candidate.ReceivingMapId));
            var projectedComp = AddComp<CMUProjectedLightComponent>(projectedUid);
            projectedComp.SourceLight = candidate.SourceLight;
            projectedComp.SourceMapId = candidate.SourceMapId;
            projectedComp.DepthOffset = candidate.DepthOffset;

            AddComp<PointLightComponent>(projectedUid);

            if (candidate.IsMerged)
                _mergedProjectedLights[mergedKey] = projectedUid;
            else
                _projectedLights[key] = projectedUid;
        }

        return projectedUid;
    }

    private void CleanupStaleProjectedLights()
    {
        _toRemove.Clear();
        foreach (var (key, projectedUid) in _projectedLights)
        {
            if (_activeThisFrame.Contains(projectedUid))
                continue;

            _toRemove.Add(key);
            if (Exists(projectedUid))
                Del(projectedUid);
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

            _mergedToRemove.Add(key);
            if (Exists(projectedUid))
                Del(projectedUid);
        }

        foreach (var key in _mergedToRemove)
        {
            _mergedProjectedLights.Remove(key);
        }
    }

    private void CleanupAllProjectedLights()
    {
        foreach (var (_, projectedUid) in _projectedLights)
        {
            if (Exists(projectedUid))
                Del(projectedUid);
        }

        foreach (var (_, projectedUid) in _mergedProjectedLights)
        {
            if (Exists(projectedUid))
                Del(projectedUid);
        }

        _projectedLights.Clear();
        _mergedProjectedLights.Clear();
        _activeThisFrame.Clear();
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
}
