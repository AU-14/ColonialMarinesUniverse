using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Shared._CMU14.ZLevels;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.Ghost;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using static Content.Server.Chat.Systems.ChatSystem;

namespace Content.Server._CMU14.ZLevels.Chat;

public sealed partial class CMUZLevelsSpeakingSystem : EntitySystem
{
    private const float OpeningHearingRadius = 1.5f;

    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevel = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

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

        foreach (var session in _player.Sessions)
        {
            if (ev.Recipients.ContainsKey(session))
                continue;

            if (session.AttachedEntity is not { Valid: true } listener ||
                !_xformQuery.TryComp(listener, out var listenerXform) ||
                listenerXform.MapUid is not { } listenerMap ||
                listenerMap == sourceMap ||
                !_zMapQuery.TryComp(listenerMap, out var listenerZMap))
            {
                continue;
            }

            if (listenerZMap.NetworkUid != sourceZMap.NetworkUid)
                continue;

            var sourceDepthOffset = sourceZMap.Depth - listenerZMap.Depth;
            if (sourceDepthOffset is not 1 and not -1)
                continue;

            var listenerPosition = _transform.GetWorldPosition(listenerXform, _xformQuery);
            if (!CanHearAcrossZLevel(
                    ev.Source,
                    sourceXform,
                    sourceMap,
                    sourcePosition,
                    listener,
                    listenerXform,
                    listenerZMap,
                    listenerPosition,
                    sourceDepthOffset,
                    ev.VoiceRange,
                    out var distance))
            {
                continue;
            }

            ev.Recipients.TryAdd(session, new ICChatRecipientData(distance, _ghostHearingQuery.HasComp(listener)));
        }
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
        out float distance)
    {
        distance = Vector2.Distance(sourcePosition, listenerPosition);
        if (distance >= voiceRange)
            return false;

        return sourceDepthOffset > 0
            ? CanHearSourceAbove(source, sourceXform, sourcePosition, listener, listenerXform, listenerZMap, listenerPosition, voiceRange)
            : CanHearSourceBelow(source, sourcePosition, listener, listenerXform, sourceMap, listenerPosition, voiceRange);
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
        float voiceRange)
    {
        if (!_viewerQuery.TryComp(listener, out var viewer) ||
            listenerXform.MapUid is not { } listenerMap ||
            !_zLevel.HasZLevelEye(viewer, sourceMap))
        {
            return false;
        }

        if (!_zLevel.TryFindOpeningNear(listenerMap, sourcePosition, OpeningHearingRadius, out var openingPosition))
            return false;

        return CanSeeOnMap(listenerMap, listenerPosition, openingPosition, listener, source, voiceRange);
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
