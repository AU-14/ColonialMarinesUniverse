using Content.Shared.GameTicking;

namespace Content.Server.GameTicking;

public sealed partial class GameTicker
{
    internal static readonly TimeSpan DistressSignalSurvivorAnnouncementLeadTime = TimeSpan.FromMinutes(1);

    private int? _announcedDistressSignalSurvivorCount;

    private void TryAnnounceDistressSignalSurvivors()
    {
        if (!IsDistressSignalSurvivorAnnouncementDue(
                LobbyEnabled,
                RunLevel,
                Paused,
                _announcedDistressSignalSurvivorCount.HasValue,
                _roundStartTime,
                _gameTiming.CurTime))
        {
            return;
        }

        TryLockAndAnnounceDistressSignalSurvivors();
    }

    private void EnsureDistressSignalSurvivorAnnouncement()
    {
        TryLockAndAnnounceDistressSignalSurvivors(revalidate: true);
    }

    private bool TryLockAndAnnounceDistressSignalSurvivors(bool revalidate = false)
    {
        if (!_auRoundSystem.TryLockDistressSignalThirdParties(out var survivorCount, revalidate))
            return false;

        if (_announcedDistressSignalSurvivorCount == survivorCount)
            return true;

        _announcedDistressSignalSurvivorCount = survivorCount;
        var message = survivorCount > 0
            ? Loc.GetString("cmu-distress-signal-survivors-spawning", ("count", survivorCount))
            : Loc.GetString("cmu-distress-signal-no-survivors");
        _chatManager.DispatchServerAnnouncement(message);
        return true;
    }

    private void ResetDistressSignalSurvivorAnnouncement()
    {
        _announcedDistressSignalSurvivorCount = null;
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
