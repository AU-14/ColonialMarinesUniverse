using System.Collections.Generic;
using Robust.Shared.Serialization;

namespace Content.Shared.GameTicking;

[Serializable, NetSerializable, DataDefinition]
public partial struct RoundEndSummaryStat
{
    [DataField]
    public string Label;

    [DataField]
    public string Detail;

    [DataField]
    public int Value;

    [DataField]
    public RoundEndSummaryStatColor Color;

    public RoundEndSummaryStat(
        string label,
        string detail,
        int value,
        RoundEndSummaryStatColor color)
    {
        Label = label;
        Detail = detail;
        Value = value;
        Color = color;
    }
}

[Serializable, NetSerializable, DataDefinition]
public partial struct RoundEndSummaryStats
{
    [DataField]
    public RoundEndSummaryStat[] InjuryStats;

    [DataField]
    public RoundEndSummaryStat[] OddityStats;

    public RoundEndSummaryStats(
        RoundEndSummaryStat[] injuryStats,
        RoundEndSummaryStat[] oddityStats)
    {
        InjuryStats = injuryStats;
        OddityStats = oddityStats;
    }

    public static RoundEndSummaryStats Empty => new(
        Array.Empty<RoundEndSummaryStat>(),
        Array.Empty<RoundEndSummaryStat>());
}

[Serializable, NetSerializable]
public enum RoundEndSummaryStatColor : byte
{
    Blue,
    Red,
    Gold,
    Purple,
    Cyan,
    Green,
}

public sealed partial class RoundEndSummaryStatsEvent
{
    private readonly List<RoundEndSummaryStat> _injuryStats = new();
    private readonly List<RoundEndSummaryStat> _oddityStats = new();

    public void AddInjuryStat(
        string label,
        string detail,
        int value,
        RoundEndSummaryStatColor color)
    {
        AddStat(_injuryStats, label, detail, value, color);
    }

    public void AddOddityStat(
        string label,
        string detail,
        int value,
        RoundEndSummaryStatColor color)
    {
        AddStat(_oddityStats, label, detail, value, color);
    }

    public RoundEndSummaryStats ToSummaryStats()
    {
        return new RoundEndSummaryStats(
            _injuryStats.ToArray(),
            _oddityStats.ToArray());
    }

    private static void AddStat(
        List<RoundEndSummaryStat> stats,
        string label,
        string detail,
        int value,
        RoundEndSummaryStatColor color)
    {
        if (value < 0)
            return;

        stats.Add(new RoundEndSummaryStat(label, detail, value, color));
    }
}
