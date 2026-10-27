using Content.Shared.CMU14.Lobby;

namespace Content.Client.CMU14.Lobby;

public sealed class CmuLobbyRosterSystem : EntitySystem
{
    public event Action<CmuLobbyRosterResponse>? Updated;

    public override void Initialize() => SubscribeNetworkEvent<CmuLobbyRosterResponse>(message => Updated?.Invoke(message));

    public void Request(NetEntity station) => RaiseNetworkEvent(new CmuLobbyRosterRequest(station));
}
