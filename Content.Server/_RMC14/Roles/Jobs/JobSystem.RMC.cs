using Content.Shared.Roles;
using Robust.Shared.Player;

namespace Content.Server.Roles.Jobs;

public sealed partial class JobSystem
{
    private bool TrySendRMCGreeting(ICommonSession session, JobPrototype job)
    {
        if (job.Greeting is not { } greeting)
            return false;

        _chat.DispatchServerMessage(session,
            Loc.GetString(greeting, ("jobName", job.LocalizedName)));
        return true;
    }
}
