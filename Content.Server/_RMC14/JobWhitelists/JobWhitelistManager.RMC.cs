using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server.Players.JobWhitelist;

public sealed partial class JobWhitelistManager
{
    private bool IsRMCWhitelistAllowed(NetUserId player, ProtoId<JobPrototype> job)
    {
        if (!_prototypes.Resolve(job, out var jobPrototype) || !jobPrototype.Whitelisted)
            return true;

        var visited = new HashSet<ProtoId<JobPrototype>>();
        while (visited.Add(job))
        {
            if (IsWhitelisted(player, job))
                return true;

            if (!_prototypes.Resolve(job, out jobPrototype) ||
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
