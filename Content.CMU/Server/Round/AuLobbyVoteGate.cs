using Content.Server.GameTicking;

namespace Content.Server.CMU14.Round;

public static class AuLobbyVoteGate
{
    public static bool ShouldStartVoteSequence(
        bool lobbyEnabled,
        GameRunLevel runLevel,
        int playerCount,
        int minimumPlayers)
    {
        if (!lobbyEnabled)
            return false;

        if (runLevel != GameRunLevel.PreRoundLobby)
            return false;

        return LobbyMinimumPlayerGate.HasEnoughPlayers(playerCount, minimumPlayers);
    }
}
