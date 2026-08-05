using Content.Shared.GameTicking;

namespace Content.Server.GameTicking;

public sealed partial class GameTicker
{
    internal RoundEndSummaryStats CollectRoundEndSummaryStats()
    {
        var ev = new RoundEndSummaryStatsEvent();
        RaiseLocalEvent(ev);
        return ev.ToSummaryStats();
    }
}
