using System.Numerics;
using System.Threading;
using Content.Shared._CMU14.ZLevels;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server._CMU14.ZLevels.Core;

public sealed partial class CMUZLevelsSystem
{
    private const float CrossZAudioOpeningRadius = 1.5f;

    [Dependency] private SharedAudioSystem _audioSystem = default!;

    private readonly HashSet<Entity<ActorComponent>> _zAudioActorLookup = new();
    private readonly HashSet<EntityUid> _zLevelAudioSources = new();
    private readonly HashSet<EntityUid> _zLevelAudioPendingSources = new();
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _zLevelAudioProjectionsBySource = new();
    private readonly Dictionary<EntityUid, EntityUid> _zLevelAudioSourceByProjection = new();
    private readonly List<CMUZLevelAcousticPathStep> _zLevelAudioPath = new();
    private readonly List<EntityUid> _zLevelAudioSourceScratch = new();
    private EntityQuery<TransformComponent> _zAudioXformQuery;
    private int _crossZAudioEnabled = 1;
    private bool _creatingZLevelAudioProjection;
    private int _maxAudioDepth = 1;
    private int _audioConfigurationDirty;
    private bool CrossZAudioEnabled => Interlocked.CompareExchange(ref _crossZAudioEnabled, 0, 0) != 0;

    private void InitAudio()
    {
        _zAudioXformQuery = GetEntityQuery<TransformComponent>();

        Subs.CVar(_config, CMUZLevelsCVars.CrossZAudio, OnCrossZAudioChanged, true);
        Subs.CVar(_config, CMUZLevelsCVars.Enabled, OnZLevelAudioSystemEnabledChanged, true);
        Subs.CVar(_config, CMUZLevelsCVars.MaxAudioDepth, OnCrossZAudioDepthChanged, true);

        SubscribeLocalEvent<AudioComponent, MoveEvent>(OnAudioMove);
        SubscribeLocalEvent<AudioComponent, ComponentShutdown>(OnAudioShutdown);
        SubscribeLocalEvent<CMUZLevelNetworkUpdatedEvent>(OnAudioNetworkUpdated);
    }

    private void OnAudioMove(Entity<AudioComponent> ent, ref MoveEvent args)
    {
        if (_creatingZLevelAudioProjection ||
            _zLevelAudioSourceByProjection.ContainsKey(ent.Owner))
        {
            return;
        }

        _zLevelAudioPendingSources.Add(ent);
    }

    private void ProcessPendingCrossZAudioSources()
    {
        if (_zLevelAudioPendingSources.Count == 0)
            return;

        _zLevelAudioSourceScratch.Clear();
        _zLevelAudioSourceScratch.AddRange(_zLevelAudioPendingSources);
        _zLevelAudioPendingSources.Clear();

        foreach (var uid in _zLevelAudioSourceScratch)
        {
            if (!TryComp<AudioComponent>(uid, out var audio) ||
                !_zAudioXformQuery.TryComp(uid, out var xform) ||
                !CanProjectCrossZAudio((uid, audio), xform, out var sourceMap))
            {
                RemoveCrossZAudioSource(uid);
                continue;
            }

            _zLevelAudioSources.Add(uid);
            RebuildCrossZAudioSource((uid, audio), (xform.MapUid!.Value, sourceMap), xform);
        }

        _zLevelAudioSourceScratch.Clear();
    }

    private bool CanProjectCrossZAudio(
        Entity<AudioComponent> source,
        TransformComponent xform,
        out CMUZLevelMapComponent sourceMap)
    {
        sourceMap = default!;

        if (source.Comp.Global ||
            source.Comp.IncludedEntities != null ||
            source.Comp.Params.Loop ||
            string.IsNullOrEmpty(source.Comp.FileName) ||
            xform.MapUid is not { } map ||
            !TryComp<CMUZLevelMapComponent>(map, out var foundSourceMap) ||
            foundSourceMap == null)
        {
            return false;
        }

        sourceMap = foundSourceMap;
        return true;
    }

    private void RebuildCrossZAudioSource(
        Entity<AudioComponent> source,
        Entity<CMUZLevelMapComponent> sourceMap,
        TransformComponent xform)
    {
        StopCrossZAudioProjections(source);

        if (!ZLevelsEnabled ||
            !CrossZAudioEnabled ||
            source.Comp.State != AudioState.Playing ||
            TerminatingOrDeleted(source))
        {
            return;
        }

        var sourcePosition = _transform.GetWorldPosition(xform);
        ProjectCrossZAudio(source, sourceMap, sourcePosition);
    }

    private void OnAudioShutdown(Entity<AudioComponent> ent, ref ComponentShutdown args)
    {
        if (_zLevelAudioSourceByProjection.Remove(ent, out var source))
        {
            if (_zLevelAudioProjectionsBySource.TryGetValue(source, out var projections))
            {
                projections.Remove(ent);
                if (projections.Count == 0)
                    _zLevelAudioProjectionsBySource.Remove(source);
            }

            return;
        }

        RemoveCrossZAudioSource(ent);
    }

    private void OnCrossZAudioChanged(bool enabled)
    {
        Interlocked.Exchange(ref _crossZAudioEnabled, enabled ? 1 : 0);
        Interlocked.Exchange(ref _audioConfigurationDirty, 1);
    }

    private void OnZLevelAudioSystemEnabledChanged(bool enabled)
    {
        Interlocked.Exchange(ref _audioConfigurationDirty, 1);
    }

    private void OnCrossZAudioDepthChanged(int value)
    {
        _maxAudioDepth = Math.Clamp(value, 0, MaxZLevelTraversalDepth);
        Interlocked.Exchange(ref _audioConfigurationDirty, 1);
    }

    private void ApplyPendingAudioConfiguration()
    {
        if (Interlocked.Exchange(ref _audioConfigurationDirty, 0) == 0)
            return;

        RefreshCrossZAudioSources();
    }

    private void OnAudioNetworkUpdated(ref CMUZLevelNetworkUpdatedEvent args)
    {
        RefreshCrossZAudioSources();
    }

    private void RefreshCrossZAudioSources()
    {
        if (!ZLevelsEnabled || !CrossZAudioEnabled)
        {
            StopAllCrossZAudioProjections();
            return;
        }

        _zLevelAudioSourceScratch.Clear();
        _zLevelAudioSourceScratch.AddRange(_zLevelAudioSources);

        foreach (var uid in _zLevelAudioSourceScratch)
        {
            if (!TryComp<AudioComponent>(uid, out var audio) ||
                !_zAudioXformQuery.TryComp(uid, out var xform) ||
                !CanProjectCrossZAudio((uid, audio), xform, out var sourceMap))
            {
                RemoveCrossZAudioSource(uid);
                continue;
            }

            RebuildCrossZAudioSource((uid, audio), (xform.MapUid!.Value, sourceMap), xform);
        }

        _zLevelAudioSourceScratch.Clear();
    }

    private void RemoveCrossZAudioSource(EntityUid source)
    {
        _zLevelAudioPendingSources.Remove(source);
        _zLevelAudioSources.Remove(source);
        StopCrossZAudioProjections(source);
    }

    private void StopAllCrossZAudioProjections()
    {
        _zLevelAudioSourceScratch.Clear();
        _zLevelAudioSourceScratch.AddRange(_zLevelAudioProjectionsBySource.Keys);

        foreach (var source in _zLevelAudioSourceScratch)
        {
            StopCrossZAudioProjections(source);
        }

        _zLevelAudioSourceScratch.Clear();
    }

    private void StopCrossZAudioProjections(EntityUid source)
    {
        if (!_zLevelAudioProjectionsBySource.Remove(source, out var projections))
            return;

        foreach (var projection in projections)
        {
            _zLevelAudioSourceByProjection.Remove(projection);
            _audioSystem.Stop(projection);
        }
    }

    public void PlayPvsDirectlyAcrossZ(SoundSpecifier sound, EntityUid source, int maxDepth = 1)
    {
        _creatingZLevelAudioProjection = true;
        try
        {
            _audioSystem.PlayPvs(sound, source);
            var xform = Transform(source);
            if (xform.MapUid is not { } sourceMap ||
                !TryComp<CMUZLevelMapComponent>(sourceMap, out var sourceZMap))
                return;

            var position = _transform.GetWorldPosition(xform);
            Entity<CMUZLevelMapComponent?> current = (sourceMap, sourceZMap);
            for (var direction = -1; direction <= 1; direction += 2)
            {
                current = (sourceMap, sourceZMap);
                for (var depth = 0; depth < maxDepth; depth++)
                {
                    if (!TryMapOffset(current, direction, out var target))
                        break;
                    _audioSystem.PlayPvs(sound, new EntityCoordinates(target.Value.Owner, position));
                    current = (target.Value.Owner, target.Value.Comp);
                }
            }
        }
        finally
        {
            _creatingZLevelAudioProjection = false;
        }
    }

    private void ProjectCrossZAudio(
        Entity<AudioComponent> source,
        Entity<CMUZLevelMapComponent> sourceMap,
        Vector2 sourcePosition)
    {
        var maxDepth = _maxAudioDepth;
        if (maxDepth <= 0 ||
            source.Comp.Params.MaxDistance <= 0f)
        {
            return;
        }

        ResolvedSoundSpecifier? specifier = null;
        ProjectCrossZAudioDirection(source, sourceMap, sourcePosition, ref specifier, -1, maxDepth);
        ProjectCrossZAudioDirection(source, sourceMap, sourcePosition, ref specifier, 1, maxDepth);
    }

    private void ProjectCrossZAudioDirection(
        Entity<AudioComponent> source,
        Entity<CMUZLevelMapComponent> sourceMap,
        Vector2 sourcePosition,
        ref ResolvedSoundSpecifier? specifier,
        int step,
        int maxDepth)
    {
        Entity<CMUZLevelMapComponent?> acousticSourceMap = (sourceMap.Owner, sourceMap.Comp);
        BuildAcousticPath(
            acousticSourceMap,
            sourcePosition,
            step,
            maxDepth,
            CrossZAudioOpeningRadius,
            _zLevelAudioPath);

        foreach (var pathStep in _zLevelAudioPath)
        {
            var filter = BuildCrossZAudioFilter(source.Comp, pathStep.TargetMap, pathStep.OpeningPosition);
            if (filter.Count == 0)
                continue;

            specifier ??= new ResolvedPathSpecifier(source.Comp.FileName);
            CreateZLevelAudioProjection(
                source,
                specifier,
                filter,
                pathStep.TargetMap,
                pathStep.OpeningPosition);
        }
    }

    private Filter BuildCrossZAudioFilter(
        AudioComponent source,
        Entity<CMUZLevelMapComponent> targetMap,
        Vector2 sourcePosition)
    {
        var maxDistance = source.Params.MaxDistance;
        var maxDistanceSquared = maxDistance * maxDistance;
        var filter = Filter.Empty();

        if (!TryGetMapCoordinates(targetMap.Owner, sourcePosition, out var targetCoordinates))
            return filter;

        _zAudioActorLookup.Clear();
        _entityLookup.GetEntitiesInRange(targetCoordinates, maxDistance, _zAudioActorLookup, LookupFlags.All);

        foreach (var listener in _zAudioActorLookup)
        {
            if (source.ExcludedEntity == listener.Owner ||
                !_zAudioXformQuery.TryComp(listener.Owner, out var xform) ||
                xform.MapUid != targetMap.Owner)
            {
                continue;
            }

            var listenerPosition = _transform.GetWorldPosition(xform);
            if (Vector2.DistanceSquared(listenerPosition, sourcePosition) <= maxDistanceSquared)
                filter.AddPlayer(listener.Comp.PlayerSession);
        }

        _zAudioActorLookup.Clear();
        return filter;
    }

    private void CreateZLevelAudioProjection(
        Entity<AudioComponent> source,
        ResolvedSoundSpecifier specifier,
        Filter filter,
        EntityUid targetMap,
        Vector2 sourcePosition)
    {
        _creatingZLevelAudioProjection = true;

        try
        {
            var projectedAudio = _audioSystem.PlayStatic(
                specifier,
                filter,
                new EntityCoordinates(targetMap, sourcePosition),
                false,
                source.Comp.Params);

            if (projectedAudio is not { } projected)
                return;

            if (!_zLevelAudioProjectionsBySource.TryGetValue(source, out var projections))
            {
                projections = new HashSet<EntityUid>();
                _zLevelAudioProjectionsBySource[source] = projections;
            }

            projections.Add(projected.Entity);
            _zLevelAudioSourceByProjection[projected.Entity] = source;
            projected.Component.Flags = source.Comp.Flags;

            Dirty(projected.Entity, projected.Component);

            var playbackPosition = (float) (_gameTiming.CurTime - source.Comp.AudioStart).TotalSeconds;
            Entity<AudioComponent?> nullableProjection = (projected.Entity, projected.Component);
            _audioSystem.SetPlaybackPosition(nullableProjection, playbackPosition);
        }
        finally
        {
            _creatingZLevelAudioProjection = false;
        }
    }
}
