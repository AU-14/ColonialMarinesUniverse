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
        AudioComponent source,
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
                source.Params);

            if (projectedAudio is not { } projected)
                return;

            _zLevelAudioProjections.Add(projected.Entity);
            projected.Component.Flags = source.Flags;

            Dirty(projected.Entity, projected.Component);
        }
        finally
        {
            _creatingZLevelAudioProjection = false;
        }
    }
}
