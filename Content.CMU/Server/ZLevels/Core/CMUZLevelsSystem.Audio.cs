using System.Numerics;
using Content.Shared.CMU14.ZLevels;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Spawners;

namespace Content.Server.CMU14.ZLevels.Core;

public sealed partial class CMUZLevelsSystem
{
    private const float CrossZAudioOpeningRadius = 1.5f;
    private static readonly TimeSpan CrossZAudioRefreshInterval = TimeSpan.FromMilliseconds(200);

    [Dependency] private SharedAudioSystem _audioSystem = default!;

    private readonly HashSet<EntityUid> _pendingZLevelAudio = new();
    private readonly Dictionary<EntityUid, ZLevelAudioSource> _zLevelAudioSources = new();
    private readonly HashSet<EntityUid> _zLevelAudioProjections = new();
    private readonly HashSet<EntityUid> _manualZLevelAudioSources = new();
    private readonly HashSet<Entity<ActorComponent>> _zAudioActorLookup = new();
    private readonly List<EntityUid> _zAudioSourceBuffer = new();
    private readonly List<EntityUid> _zAudioMapBuffer = new();
    private readonly HashSet<EntityUid> _zAudioWantedMaps = new();
    private EntityQuery<TransformComponent> _zAudioXformQuery;
    private bool _crossZAudioEnabled = true;
    private bool _creatingZLevelAudioProjection;
    private bool _zAudioWasEnabled;

    private sealed class ZLevelAudioSource
    {
        public readonly Dictionary<EntityUid, EntityUid> Projections = new();
        public TimeSpan NextRefresh;
        public TimeSpan? AudioStart;
        public TimeSpan? PauseTime;
        public AudioState PlaybackState;
    }

    private void InitAudio()
    {
        _zAudioXformQuery = GetEntityQuery<TransformComponent>();

        Subs.CVar(_config, CMUZLevelsCVars.CrossZAudio, OnCrossZAudioChanged, true);

        SubscribeLocalEvent<AudioComponent, MoveEvent>(OnAudioMove);
        SubscribeLocalEvent<AudioComponent, ComponentShutdown>(OnAudioShutdown);
        SubscribeLocalEvent<CMUZLevelNetworkUpdatedEvent>(OnAudioNetworkUpdated);
    }

    private void OnAudioMove(Entity<AudioComponent> ent, ref MoveEvent args)
    {
        if (_creatingZLevelAudioProjection || _zLevelAudioProjections.Contains(ent) || _manualZLevelAudioSources.Contains(ent) ||
            !_zLevelsEnabled || !_crossZAudioEnabled || args.Component.MapUid is not { } map ||
            !TryGetZNetwork(map, out _))
            return;

        // PlayEntity/PlayStatic/PlayPredicted install audience restrictions after
        // their coordinate setter raises MoveEvent. Inspect only after the call returns.
        _pendingZLevelAudio.Add(ent);
    }

    private void OnAudioShutdown(Entity<AudioComponent> ent, ref ComponentShutdown args)
    {
        _pendingZLevelAudio.Remove(ent);
        RemoveZLevelAudioSource(ent);
        _zLevelAudioProjections.Remove(ent);
        _manualZLevelAudioSources.Remove(ent);
    }

    private void OnCrossZAudioChanged(bool enabled)
    {
        _crossZAudioEnabled = enabled;
    }

    private void OnAudioNetworkUpdated(ref CMUZLevelNetworkUpdatedEvent args)
    {
        QueueExistingCrossZAudio(args.Network.Owner);
    }

    private void QueueExistingCrossZAudio(EntityUid? network = null)
    {
        if (!_zLevelsEnabled || !_crossZAudioEnabled)
            return;

        // Only topology/enabling needs discovery. Ordinary updates never scan all audio.
        var query = EntityQueryEnumerator<AudioComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var audio, out var transform))
        {
            if (_zLevelAudioProjections.Contains(uid) || _manualZLevelAudioSources.Contains(uid) ||
                audio.Global || audio.IncludedEntities != null || TerminatingOrDeleted(uid) ||
                EntityManager.IsQueuedForDeletion(uid) || transform.MapUid is not { } map ||
                !TryGetZNetwork(map, out var membership) ||
                network is { } expected && membership.Value.Owner != expected)
                continue;

            _pendingZLevelAudio.Add(uid);
        }
    }

    private void RemoveZLevelAudioSource(EntityUid source)
    {
        if (!_zLevelAudioSources.Remove(source, out var state))
            return;

        foreach (var projection in state.Projections.Values)
            QueueDel(projection);
    }

    private void UpdateAudio()
    {
        var enabled = _zLevelsEnabled && _crossZAudioEnabled;
        if (enabled && !_zAudioWasEnabled)
            QueueExistingCrossZAudio();
        _zAudioWasEnabled = enabled;

        foreach (var uid in _pendingZLevelAudio)
        {
            if (!_zLevelAudioSources.TryGetValue(uid, out var state))
                _zLevelAudioSources.Add(uid, new ZLevelAudioSource());
            else
                state.NextRefresh = TimeSpan.Zero;
        }
        _pendingZLevelAudio.Clear();

        // The source list is a snapshot: removing audio may invoke shutdown callbacks.
        _zAudioSourceBuffer.Clear();
        _zAudioSourceBuffer.AddRange(_zLevelAudioSources.Keys);
        foreach (var uid in _zAudioSourceBuffer)
        {
            if (!_zLevelAudioSources.TryGetValue(uid, out var state))
                continue;

            if (!_zLevelsEnabled || !_crossZAudioEnabled || TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid) ||
                !TryComp<AudioComponent>(uid, out var audio) || audio.Global ||
                audio.IncludedEntities != null || string.IsNullOrEmpty(audio.FileName) ||
                Transform(uid).MapUid is not { } sourceMap || !TryGetZNetwork(sourceMap, out _))
            {
                RemoveZLevelAudioSource(uid);
                continue;
            }

            // A stopped source can be started again without a MoveEvent.
            if (audio.State == AudioState.Stopped)
            {
                ClearZLevelAudioProjections(state);
                state.NextRefresh = TimeSpan.Zero;
                continue;
            }

            if (_gameTiming.CurTime < state.NextRefresh)
                continue;

            state.NextRefresh = _gameTiming.CurTime + CrossZAudioRefreshInterval;
            var playbackState = audio.State;
            var pauseTime = audio.PauseTime;
            var metadata = MetaData(uid);
            if (metadata.EntityPaused)
            {
                playbackState = AudioState.Paused;
                var entityPauseTime = _gameTiming.CurTime - _meta.GetPauseTime(uid, metadata);
                if (pauseTime == null || entityPauseTime < pauseTime.Value)
                    pauseTime = entityPauseTime;
            }

            _zAudioWantedMaps.Clear();
            var xform = Transform(uid);
            var maxDepth = Math.Min(_maxRenderDepth, MaxZLevelsBelowRendering);
            if (maxDepth > 0 && audio.Params.MaxDistance > 0f)
            {
                var sourcePosition = _transform.GetWorldPosition(xform);
                ProjectCrossZAudioDirection((uid, audio), sourceMap, sourcePosition, state, playbackState, pauseTime, -1, maxDepth);
                ProjectCrossZAudioDirection((uid, audio), sourceMap, sourcePosition, state, playbackState, pauseTime, 1, maxDepth);
            }
            state.AudioStart = audio.AudioStart;
            state.PauseTime = pauseTime;
            state.PlaybackState = playbackState;

            _zAudioMapBuffer.Clear();
            foreach (var (map, _) in state.Projections)
                if (!_zAudioWantedMaps.Contains(map))
                    _zAudioMapBuffer.Add(map);

            foreach (var map in _zAudioMapBuffer)
            {
                QueueDel(state.Projections[map]);
                state.Projections.Remove(map);
            }
        }
        _zAudioSourceBuffer.Clear();
    }

    private void ClearZLevelAudioProjections(ZLevelAudioSource state)
    {
        foreach (var projection in state.Projections.Values)
            QueueDel(projection);
        state.Projections.Clear();
    }

    public override bool PlayPredictedDirectlyAcrossZ(
        SoundSpecifier? sound,
        EntityUid source,
        EntityUid? user,
        int maxDepth = 1)
    {
        if (sound == null)
            return false;

        _creatingZLevelAudioProjection = true;

        try
        {
            var original = _audioSystem.PlayPredicted(sound, source, user);
            if (original is { } played)
                _manualZLevelAudioSources.Add(played.Entity);
            var resolved = _audioSystem.ResolveSound(sound);
            ProjectPredictedDirectlyAcrossZ(resolved, sound.Params, source, user, maxDepth);
            return true;
        }
        finally
        {
            _creatingZLevelAudioProjection = false;
        }
    }

    private void ProjectPredictedDirectlyAcrossZ(
        ResolvedSoundSpecifier sound,
        AudioParams audioParams,
        EntityUid source,
        EntityUid? excludedEntity,
        int maxDepth)
    {
        if (!_zLevelsEnabled ||
            !_crossZAudioEnabled ||
            maxDepth <= 0 ||
            audioParams.MaxDistance <= 0f)
        {
            return;
        }

        var xform = Transform(source);
        if (xform.MapUid is not { } sourceMap ||
            !TryComp<CMUZLevelMapComponent>(sourceMap, out var sourceZMap))
        {
            return;
        }

        var sourcePosition = _transform.GetWorldPosition(xform);
        Entity<CMUZLevelMapComponent?> currentMap = (sourceMap, sourceZMap);

        ProjectPredictedDirectlyAcrossZDirection(sound, audioParams, excludedEntity, currentMap, sourcePosition, -1, maxDepth);
        ProjectPredictedDirectlyAcrossZDirection(sound, audioParams, excludedEntity, currentMap, sourcePosition, 1, maxDepth);
    }

    private void ProjectPredictedDirectlyAcrossZDirection(
        ResolvedSoundSpecifier sound,
        AudioParams audioParams,
        EntityUid? excludedEntity,
        Entity<CMUZLevelMapComponent?> sourceMap,
        Vector2 sourcePosition,
        int step,
        int maxDepth)
    {
        var currentMap = sourceMap;

        for (var depth = step; Math.Abs(depth) <= maxDepth; depth += step)
        {
            if (!TryMapOffset(currentMap, step, out var targetMap))
                return;

            var filter = BuildCrossZAudioFilter(audioParams, excludedEntity, targetMap.Value.Owner, sourcePosition);
            if (filter.Count > 0)
                CreateZLevelAudioProjection(audioParams, AudioFlags.None, sound, filter, targetMap.Value.Owner, sourcePosition);

            currentMap = (targetMap.Value.Owner, targetMap.Value.Comp);
        }
    }

    public void PlayPvsDirectlyAcrossZ(SoundSpecifier sound, EntityUid source, int maxDepth = 1)
    {
        _creatingZLevelAudioProjection = true;

        try
        {
            var original = _audioSystem.PlayPvs(sound, source);
            if (original is { } played)
                _manualZLevelAudioSources.Add(played.Entity);
            ProjectDirectlyAcrossZ(sound, source, maxDepth, requireCrossZAudio: false);
        }
        finally
        {
            _creatingZLevelAudioProjection = false;
        }
    }

    private void ProjectDirectlyAcrossZ(
        SoundSpecifier sound,
        EntityUid source,
        int maxDepth,
        bool requireCrossZAudio)
    {
        if (!_zLevelsEnabled ||
            maxDepth <= 0 ||
            requireCrossZAudio && !_crossZAudioEnabled)
        {
            return;
        }

        var xform = Transform(source);
        if (xform.MapUid is not { } sourceMap ||
            !TryComp<CMUZLevelMapComponent>(sourceMap, out var sourceZMap))
        {
            return;
        }

        var sourcePosition = _transform.GetWorldPosition(xform);
        Entity<CMUZLevelMapComponent?> currentMap = (sourceMap, sourceZMap);

        PlayPvsDirectlyAcrossZDirection(sound, currentMap, sourcePosition, -1, maxDepth);
        PlayPvsDirectlyAcrossZDirection(sound, currentMap, sourcePosition, 1, maxDepth);
    }

    private void PlayPvsDirectlyAcrossZDirection(
        SoundSpecifier sound,
        Entity<CMUZLevelMapComponent?> sourceMap,
        Vector2 sourcePosition,
        int step,
        int maxDepth)
    {
        var currentMap = sourceMap;

        for (var depth = step; Math.Abs(depth) <= maxDepth; depth += step)
        {
            if (!TryMapOffset(currentMap, step, out var targetMap))
                return;

            var projected = _audioSystem.PlayPvs(sound, new EntityCoordinates(targetMap.Value.Owner, sourcePosition));
            if (projected is { } played)
                _zLevelAudioProjections.Add(played.Entity);
            currentMap = (targetMap.Value.Owner, targetMap.Value.Comp);
        }
    }

    private void ProjectCrossZAudioDirection(
        Entity<AudioComponent> source,
        EntityUid sourceMap,
        Vector2 sourcePosition,
        ZLevelAudioSource state,
        AudioState playbackState,
        TimeSpan? pauseTime,
        int step,
        int maxDepth)
    {
        Entity<CMUZLevelMapComponent?> currentMap = sourceMap;
        var projectedPosition = sourcePosition;

        for (var depth = step; Math.Abs(depth) <= maxDepth; depth += step)
        {
            if (!TryMapOffset(currentMap, step, out var targetMap))
                return;

            // The separating surface belongs to the upper map. A solid floor in
            // the destination room must not suppress sound entering from above.
            var surfaceMap = step > 0 ? targetMap.Value.Owner : currentMap.Owner;
            if (!TryFindOpeningNear(surfaceMap, projectedPosition, CrossZAudioOpeningRadius, out projectedPosition))
                return;

            var filter = BuildCrossZAudioFilter(source.Comp, targetMap.Value, projectedPosition);
            if (filter.Count == 0)
            {
                currentMap = (targetMap.Value.Owner, targetMap.Value.Comp);
                continue;
            }

            _zAudioWantedMaps.Add(targetMap.Value.Owner);
            SynchronizeZLevelAudioProjection(source, state, playbackState, pauseTime, filter, targetMap.Value.Owner, projectedPosition);
            currentMap = (targetMap.Value.Owner, targetMap.Value.Comp);
        }
    }

    private void SynchronizeZLevelAudioProjection(
        Entity<AudioComponent> source,
        ZLevelAudioSource state,
        AudioState playbackState,
        TimeSpan? pauseTime,
        Filter filter,
        EntityUid map,
        Vector2 position)
    {
        // SetupAudio already sampled pitch variation on the source. Sampling again
        // would desynchronize the same sound between floors.
        var parameters = source.Comp.Params;
        parameters.Variation = null;
        AudioComponent? projection = null;
        var existing = state.Projections.TryGetValue(map, out var uid) &&
                       !TerminatingOrDeleted(uid) && TryComp(uid, out projection);
        var existingParameters = projection?.Params;
        if (existing && (projection!.FileName != source.Comp.FileName ||
                         !existingParameters.Equals(parameters) || !HasSameAudioAudience(projection, filter)))
        {
            QueueDel(uid);
            existing = false;
        }

        if (!existing)
        {
            var result = CreateZLevelAudioProjection(parameters, source.Comp.Flags,
                new ResolvedPathSpecifier(source.Comp.FileName), filter, map, position);
            if (result is not { } created)
                return;
            uid = created.Entity;
            projection = created.Component;
            state.Projections[map] = uid;
        }

        _transform.SetCoordinates(uid, new EntityCoordinates(map, position));
        _audioSystem.SetState(uid, playbackState, component: projection);
        // Server audio's dummy source does not mirror Params.Loop into Looping.
        // Owned loops follow the original entity's lifetime, including after resume.
        if (parameters.Loop)
            RemComp<TimedDespawnComponent>(uid);
        if (!existing || state.AudioStart != source.Comp.AudioStart || state.PauseTime != pauseTime ||
            state.PlaybackState != playbackState)
        {
            var elapsed = (float) ((pauseTime ?? _gameTiming.CurTime) - source.Comp.AudioStart).TotalSeconds;
            _audioSystem.SetPlaybackPosition(new Entity<AudioComponent?>(uid, projection), elapsed);
        }
        if (projection!.Flags != source.Comp.Flags)
        {
            projection.Flags = source.Comp.Flags;
            Dirty(uid, projection);
        }
    }

    private static bool HasSameAudioAudience(AudioComponent projection, Filter filter)
    {
        var included = projection.IncludedEntities;
        if (included == null || included.Count != filter.Count)
            return false;

        foreach (var recipient in filter.Recipients)
            if (recipient.AttachedEntity is not { } entity || !included.Contains(entity))
                return false;

        return true;
    }

    private Filter BuildCrossZAudioFilter(
        AudioComponent source,
        Entity<CMUZLevelMapComponent> targetMap,
        Vector2 sourcePosition)
    {
        return BuildCrossZAudioFilter(source.Params, source.ExcludedEntity, targetMap.Owner, sourcePosition);
    }

    private Filter BuildCrossZAudioFilter(
        AudioParams audioParams,
        EntityUid? excludedEntity,
        EntityUid targetMap,
        Vector2 sourcePosition)
    {
        var maxDistance = audioParams.MaxDistance;
        var maxDistanceSquared = maxDistance * maxDistance;
        var filter = Filter.Empty();

        if (!TryGetMapCoordinates(targetMap, sourcePosition, out var targetCoordinates))
            return filter;

        _zAudioActorLookup.Clear();
        _entityLookup.GetEntitiesInRange(targetCoordinates, maxDistance, _zAudioActorLookup, LookupFlags.All);

        foreach (var listener in _zAudioActorLookup)
        {
            if (excludedEntity == listener.Owner ||
                !_zAudioXformQuery.TryComp(listener.Owner, out var xform) ||
                xform.MapUid != targetMap)
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

    private (EntityUid Entity, AudioComponent Component)? CreateZLevelAudioProjection(
        AudioParams audioParams,
        AudioFlags flags,
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
                audioParams);

            if (projectedAudio is not { } projected)
                return null;

            _zLevelAudioProjections.Add(projected.Entity);
            projected.Component.Flags = flags;

            Dirty(projected.Entity, projected.Component);
            return projected;
        }
        finally
        {
            _creatingZLevelAudioProjection = false;
        }
    }
}
