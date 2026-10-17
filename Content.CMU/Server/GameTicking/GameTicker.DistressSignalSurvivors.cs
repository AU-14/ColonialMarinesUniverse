using Content.Shared.GameTicking;

namespace Content.Server.GameTicking;

public sealed partial class GameTicker
{
    internal static readonly TimeSpan DistressSignalSurvivorAnnouncementLeadTime = TimeSpan.FromMinutes(1);

    private bool _distressSignalSurvivorAnnouncementSent;

    private void TryAnnounceDistressSignalSurvivors()
    {
        if (!IsDistressSignalSurvivorAnnouncementDue(
                LobbyEnabled,
                RunLevel,
                Paused,
                _distressSignalSurvivorAnnouncementSent,
                _roundStartTime,
                _gameTiming.CurTime))
        {
            return;
        }

        TryLockAndAnnounceDistressSignalSurvivors();
    }

    private void EnsureDistressSignalSurvivorAnnouncement()
    {
        if (_distressSignalSurvivorAnnouncementSent)
            return;

        TryLockAndAnnounceDistressSignalSurvivors();
    }

    private bool TryLockAndAnnounceDistressSignalSurvivors()
    {
        if (!_auRoundSystem.TryLockDistressSignalThirdParties(out var survivorCount))
            return false;

        _distressSignalSurvivorAnnouncementSent = true;
        var message = survivorCount > 0
            ? Loc.GetString("cmu-distress-signal-survivors-spawning", ("count", survivorCount))
            : Loc.GetString("cmu-distress-signal-no-survivors");
        _chatManager.DispatchServerAnnouncement(message);
        return true;
    }

    private void ResetDistressSignalSurvivorAnnouncement()
    {
        _distressSignalSurvivorAnnouncementSent = false;
        _auRoundSystem.ResetLockedDistressSignalThirdParties();
    }

    internal static bool IsDistressSignalSurvivorAnnouncementDue(
        bool lobbyEnabled,
        GameRunLevel runLevel,
        bool paused,
        bool announcementSent,
        TimeSpan roundStartTime,
        TimeSpan currentTime)
    {
        if (!lobbyEnabled ||
            runLevel != GameRunLevel.PreRoundLobby ||
            paused ||
            announcementSent ||
            roundStartTime == TimeSpan.Zero)
        {
            return false;
        }

        var timeLeft = roundStartTime - currentTime;
        return timeLeft > TimeSpan.Zero && timeLeft <= DistressSignalSurvivorAnnouncementLeadTime;
    }
}
