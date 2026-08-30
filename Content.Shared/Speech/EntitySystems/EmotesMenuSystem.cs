using Content.Shared._RMC14.Emote;
using Content.Shared.Chat;

namespace Content.Shared.Speech.EntitySystems;

public sealed partial class EmotesMenuSystem : EntitySystem
{
    [Dependency] private SharedChatSystem _chat = default!;
    [Dependency] private SharedRMCEmoteSystem _rmcEmote = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeAllEvent<PlayEmoteMessage>(OnPlayEmote);
    }

    private void OnPlayEmote(PlayEmoteMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;
        if (!player.HasValue)
            return;

        if (!ProtoMan.Resolve(msg.ProtoId, out var proto) || proto.ChatTriggers.Count == 0)
            return;

        if (!_rmcEmote.TryEmote(player.Value))
            return;

        _chat.TryEmoteWithChat(player.Value, msg.ProtoId);
    }
}
