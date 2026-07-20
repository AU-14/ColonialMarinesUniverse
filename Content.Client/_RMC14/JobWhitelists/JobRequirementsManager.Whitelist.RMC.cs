using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Client.Players.PlayTimeTracking;

public sealed partial class JobRequirementsManager
{
    private bool IsRMCWhitelisted(ProtoId<JobPrototype> job)
    {
        var visited = new HashSet<ProtoId<JobPrototype>>();
        while (visited.Add(job))
        {
            if (_jobWhitelists.Contains(job.Id))
                return true;

            if (!_prototypes.Resolve(job, out var jobPrototype) ||
                jobPrototype.WhitelistParent is not { } parent)
            {
                return false;
            }

            job = parent;
        }

        _sawmill.Error("Whitelist parent cycle detected for job {Job}", job);
        return false;
    }
}
