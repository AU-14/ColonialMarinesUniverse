using Content.Shared.GameTicking;

namespace Content.Server.GameTicking;

public sealed partial class GameTicker
{
    /// <summary>
    /// Restarts the pre-round countdown at its configured duration and resumes it if paused.
    /// </summary>
    public bool RestartLobbyCountdown()
    {
        if (RunLevel != GameRunLevel.PreRoundLobby)
            return false;

        _roundStartCountdownHasNotStartedYetDueToNoPlayers = false;
        _roundStartTime = _gameTiming.CurTime + LobbyDuration;
        _pauseTime = default;

        if (Paused)
            PauseStart(false);
        else
            RaiseNetworkEvent(new TickerLobbyCountdownEvent(_roundStartTime, Paused));

        return true;
    }
}
