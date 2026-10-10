using System.Numerics;
using Content.Shared.CMU14.Lobby;

namespace Content.Client.CMU14.Lobby;

public sealed partial class LobbyLineupCard
{
    private LobbyLineupCard? _interactionTarget;
    private readonly List<(LobbyLineupReaction Kind, float Age)> _reactions = new();
    public event Action<LobbyLineupCard, LobbyLineupEmote>? AmbientEmote;
    public int EmoteRevision { get; private set; }
    public bool HasInteractionAim => _interactionTarget is { Parent: not null, StageMoving: false };
    public bool IsReacting => _reactions.Count > 0;
    public Vector2 InteractionPosition => GlobalPosition + Preview.Position + Preview.Size / 2;

    public void AimAt(LobbyLineupCard target) => _interactionTarget = target;

    public LobbyLineupReaction React(LobbyLineupEmote emote, int seed)
    {
        var kind = LobbyLineupInteractionChoreography.Reaction(emote, _gesture, seed);
        if (StageMoving || Parent == null)
            return kind;
        // Refresh a matching reaction instead of accumulating unbounded recoil during a volley.
        _reactions.RemoveAll(reaction => reaction.Kind == kind);
        _reactions.Add((kind, 0));
        return kind;
    }

    public void ClearInteractions()
    {
        _interactionTarget = null;
        _reactions.Clear();
    }

    private void ApplyInteractions(ref (Direction Facing, float Rotation, Vector2 Offset) pose, float delta, bool reduced)
    {
        if (HasInteractionAim && !reduced)
        {
            var left = _interactionTarget!.InteractionPosition.X < InteractionPosition.X;
            pose.Facing = left ? Direction.West : Direction.East;
            if (left)
                pose.Offset.X *= -1;
        }
        for (var i = _reactions.Count - 1; i >= 0; i--)
        {
            var reaction = _reactions[i];
            reaction.Age += delta;
            if (reaction.Age >= LobbyLineupInteractionChoreography.ReactionDuration)
            {
                _reactions.RemoveAt(i);
                continue;
            }
            _reactions[i] = reaction;
            var response = LobbyLineupInteractionChoreography.Sample(reaction.Kind, reaction.Age, reduced);
            pose.Rotation += response.Rotation;
            pose.Offset += response.Offset;
        }
        pose.Offset = Vector2.Clamp(pose.Offset, new Vector2(-0.2f, -0.23f), new Vector2(0.2f, 0.23f));
    }
}
