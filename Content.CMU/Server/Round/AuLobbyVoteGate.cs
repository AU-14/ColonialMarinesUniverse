using Content.Server.GameTicking;

namespace Content.Server.AU14.Round;

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

        return runLevel != GameRunLevel.PreRoundLobby ||
               LobbyMinimumPlayerGate.HasEnoughPlayers(playerCount, minimumPlayers);
    }
}
