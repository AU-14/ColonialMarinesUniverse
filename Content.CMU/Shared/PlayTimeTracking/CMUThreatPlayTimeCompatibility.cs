using System;
using System.Collections.Generic;
using Content.Shared.Players.PlayTimeTracking;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.PlayTimeTracking;

public static class CMUThreatPlayTimeCompatibility
{
    public static readonly ProtoId<PlayTimeTrackerPrototype> ThreatMemberTracker = "AUJobThreatMember";

    public static Dictionary<string, TimeSpan> GetCompatibleTrackerTimes(
        IReadOnlyDictionary<string, TimeSpan> trackerTimes,
        IPrototypeManager prototypes)
    {
        var compatibleTimes = new Dictionary<string, TimeSpan>(trackerTimes);
        AddXenoTimeToThreatMember(compatibleTimes, prototypes);
        return compatibleTimes;
    }

    private static void AddXenoTimeToThreatMember(
        IDictionary<string, TimeSpan> trackerTimes,
        IPrototypeManager prototypes)
    {
        var xenoTime = TimeSpan.Zero;
        foreach (var (trackerId, time) in trackerTimes)
        {
            if (trackerId == ThreatMemberTracker.Id ||
                time <= TimeSpan.Zero ||
                !prototypes.TryIndex<PlayTimeTrackerPrototype>(trackerId, out var tracker) ||
                !tracker.IsXeno)
            {
                continue;
            }

            xenoTime += time;
        }

        if (xenoTime <= TimeSpan.Zero)
            return;

        trackerTimes.TryGetValue(ThreatMemberTracker.Id, out var threatMemberTime);
        trackerTimes[ThreatMemberTracker.Id] = threatMemberTime + xenoTime;
    }
}
