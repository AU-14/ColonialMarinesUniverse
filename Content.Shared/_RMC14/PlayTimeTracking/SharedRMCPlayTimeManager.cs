using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using Content.Shared.Players.PlayTimeTracking;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.PlayTimeTracking;

public abstract partial class SharedRMCPlayTimeManager : IPostInjectInit
{
    [Dependency] private ISharedPlaytimeManager _playtime = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    private bool _jobsDataLoaded;
    private FrozenSet<ProtoId<PlayTimeTrackerPrototype>> _humanJobs = [];
    private FrozenSet<ProtoId<PlayTimeTrackerPrototype>> _xenoJobs = [];

    void IPostInjectInit.PostInject()
    {
        PostInject();
    }

    protected virtual void PostInject()
    {
        _prototype.PrototypesReloaded += OnPrototypesReloaded;
    }

    public TimeSpan GetTotalHumanPlaytime(ICommonSession player)
    {
        EnsurePrototypesLoaded();
        return GetTotalPlaytime(player, _humanJobs);
    }

    public TimeSpan GetTotalXenoPlaytime(ICommonSession player)
    {
        EnsurePrototypesLoaded();
        return GetTotalPlaytime(player, _xenoJobs);
    }

    public bool IsHumanJob(string tracker)
    {
        EnsurePrototypesLoaded();
        return _humanJobs.Contains(tracker);
    }

    public bool IsXenoJob(string tracker)
    {
        EnsurePrototypesLoaded();
        return _xenoJobs.Contains(tracker);
    }

    public TimeSpan GetTotalPlaytime(ICommonSession player, IEnumerable<ProtoId<PlayTimeTrackerPrototype>> trackers)
    {
        EnsurePrototypesLoaded();

        var playTimes = _playtime.GetPlayTimes(player);
        var totalTime = TimeSpan.Zero;

        foreach (var (tracker, time) in playTimes)
        {
            if (trackers.Contains(tracker))
                totalTime += time;
        }

        return totalTime;
    }

    private void EnsurePrototypesLoaded()
    {
        if (!_jobsDataLoaded)
            ReloadPrototypes();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<PlayTimeTrackerPrototype>())
            ReloadPrototypes();
    }

    private void ReloadPrototypes()
    {
        var humanJobs = new HashSet<ProtoId<PlayTimeTrackerPrototype>>();
        var xenoJobs = new HashSet<ProtoId<PlayTimeTrackerPrototype>>();

        foreach (var tracker in _prototype.EnumeratePrototypes<PlayTimeTrackerPrototype>())
        {
            if (tracker.IsHumanoid)
                humanJobs.Add(tracker.ID);

            if (tracker.IsXeno)
                xenoJobs.Add(tracker.ID);
        }

        _humanJobs = humanJobs.ToFrozenSet();
        _xenoJobs = xenoJobs.ToFrozenSet();
        _jobsDataLoaded = true;
    }
}
