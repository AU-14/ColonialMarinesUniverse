using System.Linq;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Client.Players.PlayTimeTracking;

public sealed partial class JobRequirementsManager
{
    public IEnumerable<KeyValuePair<string, TimeSpan>> FetchPlaytimeJobIdByRoles()
    {
        var jobsToMap = _prototypes.EnumeratePrototypes<JobPrototype>().ToArray();
        var trackers = new HashSet<ProtoId<PlayTimeTrackerPrototype>>();
        var duplicateTrackers = new HashSet<ProtoId<PlayTimeTrackerPrototype>>();

        foreach (var job in jobsToMap)
        {
            if (!trackers.Add(job.PlayTimeTracker))
                duplicateTrackers.Add(job.PlayTimeTracker);
        }

        foreach (var job in jobsToMap)
        {
            if (duplicateTrackers.Contains(job.PlayTimeTracker) && !job.BasePlaytimeTracker)
                continue;

            if (_roles.TryGetValue(job.PlayTimeTracker, out var playtime))
                yield return new KeyValuePair<string, TimeSpan>(job.ID, playtime);
        }
    }
}
