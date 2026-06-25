using System.Numerics;
using Content.Shared._CMU14.ZLevels;
using Content.Shared._CMU14.ZLevels.Core.Components;
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

    private readonly HashSet<EntityUid> _zLevelAudioProcessed = new();
    private readonly HashSet<EntityUid> _zLevelAudioProjections = new();
    private readonly HashSet<Entity<ActorComponent>> _zAudioActorLookup = new();
    private EntityQuery<TransformComponent> _zAudioXformQuery;
    private bool _crossZAudioEnabled = true;
    private bool _creatingZLevelAudioProjection;

    private void InitAudio()
    {
        _zAudioXformQuery = GetEntityQuery<TransformComponent>();

        Subs.CVar(_config, CMUZLevelsCVars.CrossZAudio, OnCrossZAudioChanged, true);

        SubscribeLocalEvent<AudioComponent, MoveEvent>(OnAudioMove);
        SubscribeLocalEvent<AudioComponent, ComponentShutdown>(OnAudioShutdown);
    }

    private void OnAudioMove(Entity<AudioComponent> ent, ref MoveEvent args)
    {
        if (_creatingZLevelAudioProjection ||
            _zLevelAudioProjections.Contains(ent) ||
            !_zLevelsEnabled ||
            !_crossZAudioEnabled ||
            ent.Comp.Global ||
            ent.Comp.IncludedEntities != null ||
            string.IsNullOrEmpty(ent.Comp.FileName))
        {
            return;
        }

        var xform = args.Component;
        if (xform.MapUid is not { } sourceMap ||
            !TryComp<CMUZLevelMapComponent>(sourceMap, out var sourceZMap))
        {
            return;
        }

        if (!_zLevelAudioProcessed.Add(ent))
            return;

        var sourcePosition = _transform.GetWorldPosition(xform);
        ProjectCrossZAudio((ent.Owner, ent.Comp), (sourceMap, sourceZMap), sourcePosition);
    }

    private void OnAudioShutdown(Entity<AudioComponent> ent, ref ComponentShutdown args)
    {
        _zLevelAudioProcessed.Remove(ent);
        _zLevelAudioProjections.Remove(ent);
    }

    private void OnCrossZAudioChanged(bool enabled)
    {
        _crossZAudioEnabled = enabled;
    }

    private void ProjectCrossZAudio(
        Entity<AudioComponent> source,
        Entity<CMUZLevelMapComponent> sourceMap,
        Vector2 sourcePosition)
    {
        var maxDepth = Math.Min(_maxRenderDepth, MaxZLevelsBelowRendering);
        if (maxDepth <= 0 ||
            source.Comp.Params.MaxDistance <= 0f)
        {
            return;
        }

        ResolvedSoundSpecifier? specifier = null;
        ProjectCrossZAudioDirection(source.Comp, sourceMap, sourcePosition, ref specifier, -1, maxDepth);
        ProjectCrossZAudioDirection(source.Comp, sourceMap, sourcePosition, ref specifier, 1, maxDepth);
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
            _audioSystem.PlayPredicted(sound, source, user);
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
            _audioSystem.PlayPvs(sound, source);
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

            _audioSystem.PlayPvs(sound, new EntityCoordinates(targetMap.Value.Owner, sourcePosition));
            currentMap = (targetMap.Value.Owner, targetMap.Value.Comp);
        }
    }

    private void ProjectCrossZAudioDirection(
        AudioComponent source,
        Entity<CMUZLevelMapComponent> sourceMap,
        Vector2 sourcePosition,
        ref ResolvedSoundSpecifier? specifier,
        int step,
        int maxDepth)
    {
        Entity<CMUZLevelMapComponent?> currentMap = (sourceMap.Owner, sourceMap.Comp);
        var projectedPosition = sourcePosition;

        if (step < 0 &&
            !TryFindOpeningNear(sourceMap.Owner, sourcePosition, CrossZAudioOpeningRadius, out projectedPosition))
        {
            return;
        }

        for (var depth = step; Math.Abs(depth) <= maxDepth; depth += step)
        {
            if (!TryMapOffset(currentMap, step, out var targetMap))
                return;

            if (!TryFindOpeningNear(targetMap.Value.Owner, sourcePosition, CrossZAudioOpeningRadius, out projectedPosition))
                return;

            var filter = BuildCrossZAudioFilter(source, targetMap.Value, projectedPosition);
            if (filter.Count == 0)
            {
                currentMap = (targetMap.Value.Owner, targetMap.Value.Comp);
                continue;
            }

            specifier ??= new ResolvedPathSpecifier(source.FileName);
            CreateZLevelAudioProjection(source, specifier, filter, targetMap.Value, projectedPosition);
            currentMap = (targetMap.Value.Owner, targetMap.Value.Comp);
        }
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

    private void CreateZLevelAudioProjection(
        AudioComponent source,
        ResolvedSoundSpecifier specifier,
        Filter filter,
        EntityUid targetMap,
        Vector2 sourcePosition)
    {
        CreateZLevelAudioProjection(source.Params, source.Flags, specifier, filter, targetMap, sourcePosition);
    }

    private void CreateZLevelAudioProjection(
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
                return;

            _zLevelAudioProjections.Add(projected.Entity);
            projected.Component.Flags = flags;

            Dirty(projected.Entity, projected.Component);
        }
        finally
        {
            _creatingZLevelAudioProjection = false;
        }
    }
}
