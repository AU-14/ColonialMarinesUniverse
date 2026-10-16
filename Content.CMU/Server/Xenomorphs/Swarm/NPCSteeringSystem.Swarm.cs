using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.Xenomorphs.Swarm;
using Content.Shared.NPC;

namespace Content.Server.NPC.Systems;

public sealed partial class NPCSteeringSystem
{
    private bool IsSwarmLesser(EntityUid uid)
    {
        return HasComp<CMUSwarmLesserComponent>(uid);
    }

    private bool IsSwarmPassable(EntityUid uid, EntityUid obstacle)
    {
        // Steering must agree with the swarm's physical collision rules, including
        // anchored queens and moving lessers sharing a narrow passage.
        return IsSwarmLesser(uid) && (HasComp<XenoComponent>(obstacle) || HasComp<ActiveNPCComponent>(obstacle));
    }
}
