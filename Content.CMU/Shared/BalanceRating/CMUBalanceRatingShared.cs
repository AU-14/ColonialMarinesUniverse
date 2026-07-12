using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.BalanceRating;

[Serializable, NetSerializable]
public enum CMUBalanceRatingTarget : byte
{
    Weapon,
    Xeno,
    Map,
}

[Serializable, NetSerializable]
public enum CMUBalanceRatingMetric : byte
{
    Power,
    Fun,
}

[Serializable, NetSerializable]
public sealed class CMUBalanceRatingOpenEvent(
    long pollId,
    CMUBalanceRatingTarget target,
    CMUBalanceRatingMetric metric,
    string targetId,
    string targetName,
    TimeSpan duration) : EntityEventArgs
{
    public readonly long PollId = pollId;
    public readonly CMUBalanceRatingTarget Target = target;
    public readonly CMUBalanceRatingMetric Metric = metric;
    public readonly string TargetId = targetId;
    public readonly string TargetName = targetName;
    public readonly TimeSpan Duration = duration;
}

[Serializable, NetSerializable]
public sealed class CMUBalanceRatingResponseEvent(long pollId, byte rating) : EntityEventArgs
{
    public readonly long PollId = pollId;
    public readonly byte Rating = rating;
}

[Serializable, NetSerializable]
public sealed class CMUBalanceRatingCloseEvent(long pollId) : EntityEventArgs
{
    public readonly long PollId = pollId;
}

[Serializable, NetSerializable]
public readonly record struct CMUBalanceRatingStatisticsEntry(
    CMUBalanceRatingTarget Target,
    CMUBalanceRatingMetric Metric,
    string TargetId,
    string TargetName,
    int Polls,
    int Rating1,
    int Rating2,
    int Rating3,
    int Rating4,
    int Rating5,
    DateTime LastRatedAt)
{
    public int Responses => Rating1 + Rating2 + Rating3 + Rating4 + Rating5;

    public double Average => Responses == 0
        ? 0
        : (Rating1 + Rating2 * 2d + Rating3 * 3d + Rating4 * 4d + Rating5 * 5d) / Responses;
}

[Serializable, NetSerializable]
public sealed record CMUBalanceRatingDashboard(
    List<CMUBalanceRatingStatisticsEntry> Entries,
    int TotalPolls,
    int TotalResponses);

[Serializable, NetSerializable]
public sealed class CMUBalanceRatingEuiState(CMUBalanceRatingDashboard dashboard) : EuiStateBase
{
    public readonly CMUBalanceRatingDashboard Dashboard = dashboard;
}

[Serializable, NetSerializable]
public sealed class CMUBalanceRatingRefreshMessage : EuiMessageBase;
