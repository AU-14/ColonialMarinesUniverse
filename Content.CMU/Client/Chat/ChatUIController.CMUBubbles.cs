using Content.Client.Chat.UI;
using Content.Client.CMU14.ThreeD.Scene;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client.UserInterface.Systems.Chat;

public sealed partial class ChatUIController
{
    private readonly List<EntityUid> _cmuEmptySpeechQueues = new();
    private readonly List<(EntityUid Sender, SpeechBubble Bubble)> _cmuExpiredBubbles = new();

    private void CMUUpdateSpeechBubbles(FrameEventArgs delta)
    {
        // Invisible controls do not receive FrameUpdate. Their lifetime belongs to the controller.
        foreach (var (sender, bubbles) in _activeSpeechBubbles)
        {
            foreach (var bubble in bubbles)
            {
                if (bubble.CMUHasExpired)
                    _cmuExpiredBubbles.Add((sender, bubble));
            }
        }

        foreach (var (sender, bubble) in _cmuExpiredBubbles)
            RemoveSpeechBubble(sender, bubble);
        _cmuExpiredBubbles.Clear();

        if (_examine == null)
            return;

        // Defer removals so waiting queues can be traversed without copying the dictionary each frame.
        foreach (var (entity, queue) in _queuedSpeechBubbles)
        {
            if (!EntityManager.EntityExists(entity))
            {
                _cmuEmptySpeechQueues.Add(entity);
                continue;
            }

            queue.TimeLeft -= delta.DeltaSeconds;
            if (queue.TimeLeft > 0)
                continue;

            if (queue.MessageQueue.Count == 0)
            {
                _cmuEmptySpeechQueues.Add(entity);
                continue;
            }

            var message = queue.MessageQueue.Dequeue();
            queue.TimeLeft += BubbleDelayBase + message.Message.Message.Length * BubbleDelayFactor;
            CreateSpeechBubble(entity, message);
        }

        foreach (var entity in _cmuEmptySpeechQueues)
            _queuedSpeechBubbles.Remove(entity);
        _cmuEmptySpeechQueues.Clear();

        // The last queued message can finish well before its visible bubble expires.
        if (_activeSpeechBubbles.Count == 0)
            return;

        var player = _player.LocalEntity;
        var playerPos = player != null ? _eye.CurrentEye.Position : MapCoordinates.Nullspace;
        var occluded = player != null && _examine.IsOccluded(player.Value);

        foreach (var (entity, bubbles) in _activeSpeechBubbles)
        {
            if (entity == player)
            {
                SetBubbles(bubbles, true);
                continue;
            }

            if (!CanShowSpeechBubble(entity, out var sameMap))
            {
                SetBubbles(bubbles, false);
                continue;
            }

            var otherPos = _transform?.GetMapCoordinates(entity) ?? MapCoordinates.Nullspace;
            // First-person speech controls check occlusion against the visible 3D solids.
            var visible = !sameMap || !occluded ||
                          _eye.MainViewport is CMU3DSceneControl { FirstPerson: true } ||
                          _examine.InRangeUnOccluded(playerPos, otherPos, 0f, (entity, player),
                              static (uid, state) => uid == state.entity || uid == state.player);
            SetBubbles(bubbles, visible);
        }
    }

    private void CMUClearSpeechBubbles()
    {
        _queuedSpeechBubbles.Clear();
        foreach (var bubbles in _activeSpeechBubbles.Values)
        {
            foreach (var bubble in bubbles)
            {
                bubble.OnDied -= SpeechBubbleDied;
                bubble.CMURetire();
            }
        }

        _activeSpeechBubbles.Clear();
        _cmuEmptySpeechQueues.Clear();
        _cmuExpiredBubbles.Clear();
        _speechBubbleRoot.Orphan();
    }
}
