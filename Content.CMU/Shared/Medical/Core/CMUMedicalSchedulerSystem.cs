using Content.Shared.GameTicking;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._CMU14.Medical.Core;

/// <summary>
///     Dispatches sparse medical work at due times without feature-specific entity scans.
/// </summary>
public sealed partial class CMUMedicalSchedulerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;

    private const int QueueCompactionMinimumStaleEntries = 256;
    private const int QueueCompactionRatio = 4;

    private readonly Dictionary<EntityUid, HashSet<CMUMedicalWorkKey>> _keysByEntity = new();
    private readonly List<PendingEnqueue> _pendingEnqueues = new();
    private readonly PriorityQueue<QueuedWork> _queue = new(QueuedWorkComparer.Instance);
    private readonly Dictionary<ScheduledWork, ScheduledState> _scheduled = new();

    private bool _dispatching;
    private TimeSpan _dispatchTime;
    private ulong _nextVersion;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUMedicalSchedulerComponent, EntityPausedEvent>(OnEntityPaused);
        SubscribeLocalEvent<CMUMedicalSchedulerComponent, EntityUnpausedEvent>(OnEntityUnpaused);
        SubscribeLocalEvent<CMUMedicalSchedulerComponent, EntityTerminatingEvent>(OnEntityTerminating);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    /// <summary>
    ///     Schedules one keyed work item at an absolute game time, replacing any existing deadline for the same
    ///     entity and key. Work scheduled in the past is dispatched on the next system update.
    /// </summary>
    /// <returns><see langword="false"/> when the target cannot accept work because it is invalid or terminating.</returns>
    public bool Schedule(EntityUid target, CMUMedicalWorkKey key, TimeSpan dueAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Id);

        if (_net.IsClient || !target.IsValid() || TerminatingOrDeleted(target))
            return false;

        EnsureComp<CMUMedicalSchedulerComponent>(target);

        var work = new ScheduledWork(target, key);
        var version = NextVersion();
        var now = _timing.CurTime;
        if (Paused(target))
        {
            _scheduled[work] = ScheduledState.CreatePaused(version, Remaining(dueAt, now));
        }
        else
        {
            _scheduled[work] = ScheduledState.CreateActive(version, dueAt);
            Enqueue(new QueueEntry(work, version), dueAt);
        }

        if (!_keysByEntity.TryGetValue(target, out var keys))
        {
            keys = new HashSet<CMUMedicalWorkKey>();
            _keysByEntity.Add(target, keys);
        }

        keys.Add(key);
        return true;
    }

    /// <summary>
    ///     Cancels the current work item for an entity and key. Already-enqueued stale deadlines are ignored.
    /// </summary>
    /// <returns><see langword="true"/> when scheduled work was removed.</returns>
    public bool Cancel(EntityUid target, CMUMedicalWorkKey key)
    {
        if (_net.IsClient)
            return false;

        var work = new ScheduledWork(target, key);
        if (!_scheduled.Remove(work))
            return false;

        RemoveEntityKey(target, key);
        CompactQueueIfNeeded();
        return true;
    }

    /// <summary>
    ///     Dispatches work due at the current game time. The server ordering anchor calls this
    ///     after damage and respiration so feature callbacks retain their required update phase.
    /// </summary>
    public void DispatchDue()
    {
        if (_net.IsClient)
            return;

        _dispatchTime = _timing.CurTime;
        _dispatching = true;
        try
        {
            while (_queue.Count > 0 && _queue.Peek().DueAt <= _dispatchTime)
            {
                var queued = _queue.Take();
                var entry = queued.Entry;

                if (!_scheduled.TryGetValue(entry.Work, out var state) ||
                    state.Version != entry.Version ||
                    state.Paused)
                {
                    continue;
                }

                _scheduled.Remove(entry.Work);

                if (TerminatingOrDeleted(entry.Work.Target))
                {
                    RemoveEntityKey(entry.Work.Target, entry.Work.Key);
                    continue;
                }

                var ev = new CMUMedicalWorkDueEvent(entry.Work.Key);
                RaiseLocalEvent(entry.Work.Target, ref ev);

                if (!_scheduled.ContainsKey(entry.Work))
                    RemoveEntityKey(entry.Work.Target, entry.Work.Key);
            }
        }
        finally
        {
            _dispatching = false;
            foreach (var pending in _pendingEnqueues)
                _queue.Add(new QueuedWork(pending.Entry, pending.DueAt));

            _pendingEnqueues.Clear();
            CompactQueueIfNeeded();
        }
    }

    private void OnEntityPaused(Entity<CMUMedicalSchedulerComponent> ent, ref EntityPausedEvent args)
    {
        if (!_keysByEntity.TryGetValue(ent.Owner, out var keys))
            return;

        var now = _timing.CurTime;
        foreach (var key in keys)
        {
            var work = new ScheduledWork(ent.Owner, key);
            if (!_scheduled.TryGetValue(work, out var state) || state.Paused)
                continue;

            _scheduled[work] = ScheduledState.CreatePaused(NextVersion(), Remaining(state.DueAt, now));
        }
    }

    private void OnEntityUnpaused(Entity<CMUMedicalSchedulerComponent> ent, ref EntityUnpausedEvent args)
    {
        if (!_keysByEntity.TryGetValue(ent.Owner, out var keys))
            return;

        var now = _timing.CurTime;
        foreach (var key in keys)
        {
            var work = new ScheduledWork(ent.Owner, key);
            if (!_scheduled.TryGetValue(work, out var state) || !state.Paused)
                continue;

            var version = NextVersion();
            var dueAt = now + state.Remaining;
            _scheduled[work] = ScheduledState.CreateActive(version, dueAt);
            Enqueue(new QueueEntry(work, version), dueAt);
        }
    }

    private void OnEntityTerminating(Entity<CMUMedicalSchedulerComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!_keysByEntity.Remove(ent.Owner, out var keys))
            return;

        foreach (var key in keys)
            _scheduled.Remove(new ScheduledWork(ent.Owner, key));

        CompactQueueIfNeeded();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _keysByEntity.Clear();
        _pendingEnqueues.Clear();
        _queue.Clear();
        _scheduled.Clear();
    }

    private void RemoveEntityKey(EntityUid target, CMUMedicalWorkKey key)
    {
        if (!_keysByEntity.TryGetValue(target, out var keys))
            return;

        keys.Remove(key);
        if (keys.Count == 0)
            _keysByEntity.Remove(target);
    }

    private ulong NextVersion()
    {
        return ++_nextVersion;
    }

    private void Enqueue(QueueEntry entry, TimeSpan dueAt)
    {
        if (_dispatching && dueAt <= _dispatchTime)
        {
            _pendingEnqueues.Add(new PendingEnqueue(entry, dueAt));
            return;
        }

        _queue.Add(new QueuedWork(entry, dueAt));
        CompactQueueIfNeeded();
    }

    private void CompactQueueIfNeeded()
    {
        if (_dispatching)
            return;

        // Paused work has no live queue entry, so this deliberately underestimates stale entries.
        var staleEntries = _queue.Count - _scheduled.Count;
        var scheduledEntries = Math.Max(1, _scheduled.Count);
        if (staleEntries < QueueCompactionMinimumStaleEntries ||
            (long) _queue.Count <= (long) scheduledEntries * QueueCompactionRatio)
        {
            return;
        }

        _queue.Clear();
        foreach (var (work, state) in _scheduled)
        {
            if (state.Paused)
                continue;

            _queue.Add(new QueuedWork(new QueueEntry(work, state.Version), state.DueAt));
        }
    }

    private static TimeSpan Remaining(TimeSpan dueAt, TimeSpan now)
    {
        return dueAt > now ? dueAt - now : TimeSpan.Zero;
    }

    private readonly record struct ScheduledWork(EntityUid Target, CMUMedicalWorkKey Key);

    private readonly record struct QueueEntry(ScheduledWork Work, ulong Version);

    private readonly record struct QueuedWork(QueueEntry Entry, TimeSpan DueAt);

    private readonly record struct PendingEnqueue(QueueEntry Entry, TimeSpan DueAt);

    private readonly record struct ScheduledState(ulong Version, TimeSpan DueAt, TimeSpan Remaining, bool Paused)
    {
        public static ScheduledState CreateActive(ulong version, TimeSpan dueAt)
        {
            return new ScheduledState(version, dueAt, TimeSpan.Zero, false);
        }

        public static ScheduledState CreatePaused(ulong version, TimeSpan remaining)
        {
            return new ScheduledState(version, TimeSpan.Zero, remaining, true);
        }
    }

    private sealed class QueuedWorkComparer : IComparer<QueuedWork>
    {
        public static readonly QueuedWorkComparer Instance = new();

        public int Compare(QueuedWork x, QueuedWork y)
        {
            var dueAt = y.DueAt.CompareTo(x.DueAt);
            return dueAt != 0
                ? dueAt
                : y.Entry.Version.CompareTo(x.Entry.Version);
        }
    }
}
