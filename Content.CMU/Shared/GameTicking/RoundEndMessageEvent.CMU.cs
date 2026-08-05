using Robust.Shared.Audio;

namespace Content.Shared.GameTicking;

public sealed partial class RoundEndMessageEvent
{
    public RoundEndSummaryStats SummaryStats { get; } = RoundEndSummaryStats.Empty;

    public RoundEndMessageEvent(
        string gamemodeTitle,
        string roundEndText,
        TimeSpan roundDuration,
        int roundId,
        int playerCount,
        RoundEndPlayerInfo[] allPlayersEndInfo,
        ResolvedSoundSpecifier? restartSound,
        RoundEndSummaryStats summaryStats)
        : this(
            gamemodeTitle,
            roundEndText,
            roundDuration,
            roundId,
            playerCount,
            allPlayersEndInfo,
            restartSound)
    {
        SummaryStats = summaryStats;
    }
}
