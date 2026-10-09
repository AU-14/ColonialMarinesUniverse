using Content.Shared.CMU14.Threats;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.Antag;

public sealed partial class AntagSelectionSystem
{
    private bool IsJobInBlacklistGroup(ProtoId<JobPrototype> job, AntagJobBlacklistPrototype group)
    {
        return group.Jobs.Contains(job) ||
            group.RoundSides.Count > 0 && ProtoMan.TryIndex(job, out var prototype) &&
            group.RoundSides.Contains(prototype.RoundSide);
    }

    private IEnumerable<ProtoId<JobPrototype>> GetJobsInBlacklistGroup(AntagJobBlacklistPrototype group)
    {
        foreach (var job in group.Jobs)
            yield return job;

        if (group.RoundSides.Count == 0)
            yield break;

        foreach (var job in ProtoMan.EnumeratePrototypes<JobPrototype>())
        {
            if (group.RoundSides.Contains(job.RoundSide))
                yield return job.ID;
        }
    }
}
