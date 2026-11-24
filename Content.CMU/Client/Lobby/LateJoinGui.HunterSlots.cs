using Content.Client.CMU14.Yautja.Lobby;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Client.LateJoin;

public sealed partial class LateJoinGui
{
    private static readonly ProtoId<JobPrototype> CmuYautjaHunterJob = "CMUYautjaHunter";

    // leaders and council don't take a hunter slot (cmss13 too), the server lets them in at 0, so the button stays live
    private bool CmuJobOpenAtZero(ProtoId<JobPrototype> job)
    {
        return job == CmuYautjaHunterJob &&
               _entitySystem.GetEntitySystem<YautjaHuntLobbySystem>().BypassesSlotCap;
    }
}
