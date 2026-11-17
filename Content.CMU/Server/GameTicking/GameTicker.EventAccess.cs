using Content.Server.CMU14.Lobby;
using Robust.Shared.Player;

namespace Content.Server.GameTicking;

public sealed partial class GameTicker
{
    private bool CanSpawnEventJob(ICommonSession player, string? job)
    {
        if (job == null || EntityManager.System<CMUEventAccessSystem>().CanAccessJob(player.UserId, job))
            return true;

        _chatManager.DispatchServerMessage(player, Loc.GetString("cmu-event-job-locked"));
        return false;
    }
}
