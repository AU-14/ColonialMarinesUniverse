using System.Collections.Generic;
using Content.Server.AU14.Round;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server.Station.Systems;

public sealed partial class StationJobsSystem
{
    [Dependency] private AuJobSelectionSystem _cmuJobSelection = default!;

    private void AssignCmuForcedJobs(
        Dictionary<NetUserId, HumanoidCharacterProfile> profiles,
        IReadOnlyList<EntityUid> stations,
        bool useRoundStartJobs,
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assigned)
    {
        var forcedToRemove = new List<NetUserId>();
        foreach (var (player, jobId) in _cmuJobSelection.ForcedJobAssignments)
        {
            if (!profiles.ContainsKey(player))
                continue;

            EntityUid? assignedStation = null;
            ProtoId<JobPrototype>? job = null;
            foreach (var station in stations)
            {
                var jobs = useRoundStartJobs ? GetRoundStartJobs(station) : GetJobs(station);
                if (!jobs.TryGetValue(jobId, out var slots) || slots <= 0)
                    continue;

                assignedStation = station;
                job = jobId;
                break;
            }

            // Threat utility jobs are not necessarily listed on the station, but still need
            // a station UID so the normal assignment dictionary can carry them to ThreatSystem.
            if (assignedStation == null && stations.Count > 0)
            {
                assignedStation = stations[0];
                job = jobId;
            }

            if (assignedStation == null)
                continue;

            assigned[player] = (job, assignedStation.Value);
            forcedToRemove.Add(player);
        }

        foreach (var player in forcedToRemove)
        {
            profiles.Remove(player);
        }
    }
}
