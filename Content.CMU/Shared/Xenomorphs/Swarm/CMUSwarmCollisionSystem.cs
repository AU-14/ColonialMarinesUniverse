using Content.Shared._RMC14.Xenonids;
using Content.Shared.NPC;
using Robust.Shared.Physics.Events;

namespace Content.Shared.CMU14.Xenomorphs.Swarm;

public sealed class CMUSwarmCollisionSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<CMUSwarmLesserComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnPreventCollide(Entity<CMUSwarmLesserComponent> ent, ref PreventCollideEvent args)
    {
        if (HasComp<XenoComponent>(args.OtherEntity) || HasComp<ActiveNPCComponent>(args.OtherEntity))
            args.Cancelled = true;
    }
}
