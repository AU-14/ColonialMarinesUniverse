using System.Numerics;
using Content.Shared._CMU14.ZLevels;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server._CMU14.ZLevels.Core;

public sealed partial class CMUZLevelsSystem
{
    private const float CrossZAudioOpeningRadius = 1.5f;

    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;

    private readonly HashSet<EntityUid> _zLevelAudioProcessed = new();
    private readonly HashSet<EntityUid> _zLevelAudioProjections = new();
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

        var specifier = new ResolvedPathSpecifier(source.Comp.FileName);
        Entity<CMUZLevelMapComponent?> nullableSourceMap = (sourceMap.Owner, sourceMap.Comp);

        for (var depth = -maxDepth; depth <= maxDepth; depth++)
        {
            if (depth == 0)
                continue;

            if (!TryMapOffset(nullableSourceMap, depth, out var targetMap))
                continue;

            if (!TryFindCrossZAudioOpening(sourceMap, depth, sourcePosition, out var projectedPosition))
                continue;

            var filter = BuildCrossZAudioFilter(source.Comp, targetMap.Value, projectedPosition);
            if (filter.Count == 0)
                continue;

            CreateZLevelAudioProjection(source.Comp, specifier, filter, targetMap.Value, projectedPosition);
        }
    }

    private bool TryFindCrossZAudioOpening(
        Entity<CMUZLevelMapComponent> sourceMap,
        int targetDepth,
        Vector2 sourcePosition,
        out Vector2 openingPosition)
    {
        openingPosition = sourcePosition;
        var step = Math.Sign(targetDepth);
        if (step == 0)
            return false;

        Entity<CMUZLevelMapComponent?> nullableSourceMap = (sourceMap.Owner, sourceMap.Comp);
        var startDepth = step < 0 ? 0 : step;

        for (var depth = startDepth;
             step < 0 ? depth > targetDepth : depth <= targetDepth;
             depth += step)
        {
            EntityUid openingMap;
            if (depth == 0)
            {
                openingMap = sourceMap.Owner;
            }
            else
            {
                if (!TryMapOffset(nullableSourceMap, depth, out var offsetMap))
                    return false;

                openingMap = offsetMap.Value.Owner;
            }

            if (!TryFindOpeningNear(openingMap, sourcePosition, CrossZAudioOpeningRadius, out openingPosition))
                return false;
        }

        return true;
    }

    private Filter BuildCrossZAudioFilter(
        AudioComponent source,
        EntityUid targetMap,
        Vector2 sourcePosition)
    {
        var maxDistance = source.Params.MaxDistance;
        var maxDistanceSquared = maxDistance * maxDistance;

        var filter = Filter.Empty();
        foreach (var session in _playerManager.NetworkedSessions)
        {
            if (session.AttachedEntity is not { } attached ||
                source.ExcludedEntity == attached ||
                !_zAudioXformQuery.TryComp(attached, out var xform) ||
                xform.MapUid != targetMap)
            {
                continue;
            }

            var listenerPosition = _transform.GetWorldPosition(xform);
            if (Vector2.DistanceSquared(listenerPosition, sourcePosition) <= maxDistanceSquared)
                filter.AddPlayer(session);
        }

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
