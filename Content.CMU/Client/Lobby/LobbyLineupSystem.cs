using Content.Shared.CMU14.Lobby;

namespace Content.Client.CMU14.Lobby;

public sealed class LobbyLineupSystem : EntitySystem
{
    public event Action<LobbyLineupEmoteEvent>? EmoteReceived;
    public event Action<LobbyPartyShowEvent>? ShowReceived;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<LobbyLineupEmoteEvent>(OnEmote);
        SubscribeNetworkEvent<LobbyPartyShowEvent>(ev => ShowReceived?.Invoke(ev));
    }

    private void OnEmote(LobbyLineupEmoteEvent ev)
    {
        EmoteReceived?.Invoke(ev);
    }

    public void RequestEmote(LobbyLineupEmote emote)
    {
        RaiseNetworkEvent(new LobbyLineupEmoteRequest(emote));
    }

    public void RequestShow(LobbyPartyShow show)
    {
        RaiseNetworkEvent(new LobbyPartyShowRequest(show));
    }
}
