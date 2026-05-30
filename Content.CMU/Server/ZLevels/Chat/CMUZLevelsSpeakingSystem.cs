using System.Collections.Generic;
using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Shared._CMU14.ZLevels;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.Ghost;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using static Content.Server.Chat.Systems.ChatSystem;

namespace Content.Server._CMU14.ZLevels.Chat;

public sealed partial class CMUZLevelsSpeakingSystem : EntitySystem
{
    private const float OpeningHearingRadius = 1.5f;

    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevel = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly HashSet<Entity<ActorComponent>> _nearbyActors = new();
    private EntityQuery<CMUZLevelMapComponent> _zMapQuery;
    private EntityQuery<CMUZLevelViewerComponent> _viewerQuery;
    private EntityQuery<GhostHearingComponent> _ghostHearingQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();

        _zMapQuery = GetEntityQuery<CMUZLevelMapComponent>();
        _viewerQuery = GetEntityQuery<CMUZLevelViewerComponent>();
        _ghostHearingQuery = GetEntityQuery<GhostHearingComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<ExpandICChatRecipientsEvent>(OnExpandRecipients);
    }

    private void OnExpandRecipients(ExpandICChatRecipientsEvent ev)
    {
        if (!_config.GetCVar(CMUZLevelsCVars.Enabled))
            return;

        if (!_xformQuery.TryComp(ev.Source, out var sourceXform) ||
            sourceXform.MapUid is not { } sourceMap ||
            !_zMapQuery.TryComp(sourceMap, out var sourceZMap))
        {
            return;
        }

        var sourcePosition = _transform.GetWorldPosition(sourceXform, _xformQuery);
        if (ev.VoiceRange <= 0f)
            return;

        Dictionary<EntityUid, (bool Found, Vector2 Position)>? openingLookupCache = null;
        AddRecipientsOnAdjacentMap(ev, sourceXform, sourceMap, sourceZMap, sourcePosition, 1, ref openingLookupCache);
        AddRecipientsOnAdjacentMap(ev, sourceXform, sourceMap, sourceZMap, sourcePosition, -1, ref openingLookupCache);
    }

    private void AddRecipientsOnAdjacentMap(
        ExpandICChatRecipientsEvent ev,
        TransformComponent sourceXform,
        EntityUid sourceMap,
        CMUZLevelMapComponent sourceZMap,
        Vector2 sourcePosition,
        int offset,
        ref Dictionary<EntityUid, (bool Found, Vector2 Position)>? openingLookupCache)
    {
        Entity<CMUZLevelMapComponent?> sourceMapEnt = (sourceMap, sourceZMap);
        if (!_zLevel.TryMapOffset(sourceMapEnt, offset, out var adjacentMap) ||
            !_zLevel.TryGetMapCoordinates(adjacentMap.Value.Owner, sourcePosition, out var lookupCoords))
        {
            return;
        }

        var sourceDepthOffset = sourceZMap.Depth - adjacentMap.Value.Comp.Depth;
        if (adjacentMap.Value.Comp.NetworkUid != sourceZMap.NetworkUid ||
            sourceDepthOffset is not 1 and not -1)
        {
            return;
        }

        _nearbyActors.Clear();
        _lookup.GetEntitiesInRange(lookupCoords, ev.VoiceRange, _nearbyActors, LookupFlags.All);

        foreach (var actor in _nearbyActors)
        {
            var session = actor.Comp.PlayerSession;
            if (ev.Recipients.ContainsKey(session))
                continue;

            var listener = actor.Owner;
            if (!_xformQuery.TryComp(listener, out var listenerXform) ||
                listenerXform.MapUid != adjacentMap.Value.Owner)
            {
                continue;
            }

            var listenerPosition = _transform.GetWorldPosition(listenerXform, _xformQuery);
            if (!CanHearAcrossZLevel(
                    ev.Source,
                    sourceXform,
                    sourceMap,
                    sourcePosition,
                    listener,
                    listenerXform,
                    adjacentMap.Value.Comp,
                    listenerPosition,
                    sourceDepthOffset,
                    ev.VoiceRange,
                    ref openingLookupCache,
                    out var distance))
            {
                continue;
            }

            ev.Recipients.TryAdd(session, new ICChatRecipientData(distance, _ghostHearingQuery.HasComp(listener)));
        }

        _nearbyActors.Clear();
    }

    private bool CanHearAcrossZLevel(
        EntityUid source,
        TransformComponent sourceXform,
        EntityUid sourceMap,
        Vector2 sourcePosition,
        EntityUid listener,
        TransformComponent listenerXform,
        CMUZLevelMapComponent listenerZMap,
        Vector2 listenerPosition,
        int sourceDepthOffset,
        float voiceRange,
        ref Dictionary<EntityUid, (bool Found, Vector2 Position)>? openingLookupCache,
        out float distance)
    {
        distance = Vector2.Distance(sourcePosition, listenerPosition);
        if (distance >= voiceRange)
            return false;

        if (sourceDepthOffset > 0)
            return CanHearSourceAbove(source, sourceXform, sourcePosition, listener, listenerXform, listenerZMap, listenerPosition, voiceRange);

        return CanHearSourceBelow(source, sourcePosition, listener, listenerXform, sourceMap, listenerPosition, voiceRange, ref openingLookupCache);
    }

    private bool CanHearSourceAbove(
        EntityUid source,
        TransformComponent sourceXform,
        Vector2 sourcePosition,
        EntityUid listener,
        TransformComponent listenerXform,
        CMUZLevelMapComponent listenerZMap,
        Vector2 listenerPosition,
        float voiceRange)
    {
        if (!_viewerQuery.TryComp(listener, out var viewer) ||
            (!viewer.LookUp && !viewer.StairPreviewUp) ||
            sourceXform.MapUid is not { } sourceMap ||
            !_zLevel.HasZLevelEye(viewer, sourceMap))
        {
            return false;
        }

        if (viewer.LookUp &&
            !viewer.StairPreviewUp &&
            _zLevel.HasOpaqueAbove(listener, (listenerXform.MapUid!.Value, listenerZMap)))
        {
            return false;
        }

        return CanSeeOnMap(sourceMap, listenerPosition, sourcePosition, source, listener, voiceRange);
    }

    private bool CanHearSourceBelow(
        EntityUid source,
        Vector2 sourcePosition,
        EntityUid listener,
        TransformComponent listenerXform,
        EntityUid sourceMap,
        Vector2 listenerPosition,
        float voiceRange,
        ref Dictionary<EntityUid, (bool Found, Vector2 Position)>? openingLookupCache)
    {
        if (!_viewerQuery.TryComp(listener, out var viewer) ||
            listenerXform.MapUid is not { } listenerMap ||
            !_zLevel.HasZLevelEye(viewer, sourceMap))
        {
            return false;
        }

        if (!TryFindCachedOpeningNear(listenerMap, sourcePosition, ref openingLookupCache, out var openingPosition))
            return false;

        return CanSeeOnMap(listenerMap, listenerPosition, openingPosition, listener, source, voiceRange);
    }

    private bool TryFindCachedOpeningNear(
        EntityUid map,
        Vector2 sourcePosition,
        ref Dictionary<EntityUid, (bool Found, Vector2 Position)>? openingLookupCache,
        out Vector2 openingPosition)
    {
        openingLookupCache ??= new Dictionary<EntityUid, (bool Found, Vector2 Position)>();

        if (openingLookupCache.TryGetValue(map, out var cached))
        {
            openingPosition = cached.Position;
            return cached.Found;
        }

        var found = _zLevel.TryFindOpeningNear(map, sourcePosition, OpeningHearingRadius, out openingPosition);
        openingLookupCache[map] = (found, openingPosition);
        return found;
    }

    private bool CanSeeOnMap(
        EntityUid map,
        Vector2 from,
        Vector2 to,
        EntityUid fromEntity,
        EntityUid toEntity,
        float range)
    {
        if (!_zLevel.TryGetMapCoordinates(map, from, out var fromCoords) ||
            !_zLevel.TryGetMapCoordinates(map, to, out var toCoords))
        {
            return false;
        }

        return _examine.InRangeUnOccluded(
            fromCoords,
            toCoords,
            range,
            ent => ent == fromEntity || ent == toEntity);
    }
}
