using Content.Server.GameTicking;

namespace Content.Server.AU14.Round;

/// <summary>
/// Determines whether the current lobby generation can begin its CMU vote sequence.
/// </summary>
public static class AuLobbyVoteGate
{
    /// <summary>
    /// Rejects disabled lobbies, non-lobby run levels, generations already frozen for preload,
    /// and populations below the configured threshold.
    /// </summary>
    public static bool ShouldStartVoteSequence(
        bool lobbyEnabled,
        GameRunLevel runLevel,
        bool acceptingSelections,
        int playerCount,
        int minimumPlayers)
    {
        if (!lobbyEnabled)
            return false;

        return runLevel == GameRunLevel.PreRoundLobby &&
               acceptingSelections &&
               playerCount >= minimumPlayers;
    }
}
