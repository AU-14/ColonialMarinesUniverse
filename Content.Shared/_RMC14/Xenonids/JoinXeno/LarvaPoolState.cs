using Robust.Shared.Network;

namespace Content.Shared._RMC14.Xenonids.JoinXeno;

public sealed class LarvaPoolState
{
    private readonly Dictionary<NetUserId, TimeSpan> _joinedAt = [];

    public void Clear()
    {
        _joinedAt.Clear();
    }

    public void RecordJoined(NetUserId user, TimeSpan time)
    {
        _joinedAt.TryAdd(user, time);
    }

    public void RecordDeath(NetUserId user, TimeSpan time, bool preserveTime)
    {
        if (preserveTime && _joinedAt.ContainsKey(user))
            return;

        if (!_joinedAt.TryGetValue(user, out var existing) || time > existing)
            _joinedAt[user] = time;
    }

    public int Compare(NetUserId left, NetUserId right)
    {
        var timeComparison = GetTime(left).CompareTo(GetTime(right));
        return timeComparison != 0
            ? timeComparison
            : left.UserId.CompareTo(right.UserId);
    }

    public int GetPosition(NetUserId user, IEnumerable<NetUserId> eligibleCandidates)
    {
        var position = 1;
        foreach (var candidate in eligibleCandidates)
        {
            if (Compare(candidate, user) < 0)
                position++;
        }

        return position;
    }

    private TimeSpan GetTime(NetUserId user)
    {
        return _joinedAt.GetValueOrDefault(user, TimeSpan.MaxValue);
    }
}
