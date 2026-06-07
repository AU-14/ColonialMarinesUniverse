using Robust.Shared.Network;

namespace Content.Shared._RMC14.Xenonids.JoinXeno;

public sealed class LarvaQueueState
{
    private readonly List<NetUserId> _ready = [];
    private readonly Dictionary<NetUserId, TimeSpan> _waiting = [];

    public IReadOnlyList<NetUserId> ReadyUsers => _ready;
    public int ReadyCount => _ready.Count;
    public int WaitingCount => _waiting.Count;
    public bool Empty => ReadyCount == 0 && WaitingCount == 0;

    public bool Contains(NetUserId user)
    {
        return _ready.Contains(user) || _waiting.ContainsKey(user);
    }

    public bool AddReady(NetUserId user)
    {
        if (Contains(user))
            return false;

        _ready.Add(user);
        return true;
    }

    public bool AddReadyFirst(NetUserId user)
    {
        if (Contains(user))
            return false;

        _ready.Insert(0, user);
        return true;
    }

    public bool AddWaiting(NetUserId user, TimeSpan readyAt)
    {
        if (Contains(user))
            return false;

        _waiting[user] = readyAt;
        return true;
    }

    public bool Remove(NetUserId user)
    {
        return _waiting.Remove(user) || _ready.Remove(user);
    }

    public bool TryGetUserStatus(NetUserId user, out LarvaQueueUserStatus status)
    {
        var readyIndex = _ready.IndexOf(user);
        if (readyIndex >= 0)
        {
            status = new LarvaQueueUserStatus(readyIndex + 1);
            return true;
        }

        if (_waiting.ContainsKey(user))
        {
            status = new LarvaQueueUserStatus(null);
            return true;
        }

        status = default;
        return false;
    }

    public bool TryDequeueReady(out NetUserId user)
    {
        if (_ready.Count == 0)
        {
            user = default;
            return false;
        }

        user = _ready[0];
        _ready.RemoveAt(0);
        return true;
    }

    public List<NetUserId> PromoteWaiting(TimeSpan time)
    {
        var promoted = new List<NetUserId>();
        foreach (var (user, readyAt) in _waiting)
        {
            if (time >= readyAt)
                promoted.Add(user);
        }

        foreach (var user in promoted)
        {
            _waiting.Remove(user);
            _ready.Add(user);
        }

        return promoted;
    }
}

public readonly record struct LarvaQueueUserStatus(int? Position);
