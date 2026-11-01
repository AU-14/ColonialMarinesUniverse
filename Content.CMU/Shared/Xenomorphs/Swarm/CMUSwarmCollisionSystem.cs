using Content.Shared._RMC14.Xenonids;
using Content.Shared.NPC;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Events;

namespace Content.Shared.CMU14.Xenomorphs.Swarm;

public sealed partial class CMUSwarmCollisionSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUSwarmLesserComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnPreventCollide(Entity<CMUSwarmLesserComponent> ent, ref PreventCollideEvent args)
    {
        if (HasComp<XenoComponent>(args.OtherEntity) || HasComp<ActiveNPCComponent>(args.OtherEntity))
            args.Cancelled = true;

        // NPC world movement is authoritative on the server. Clients only need
        // these static contacts when predicting a locally pulled or controlled lesser.
        if (_net.IsClient && !args.OurBody.Predict && args.OtherBody.BodyType == BodyType.Static)
            args.Cancelled = true;
    }
}
