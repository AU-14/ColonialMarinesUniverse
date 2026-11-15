using Content.Client.Interactable.Components;
using Content.Shared._RMC14.Stealth;

namespace Content.Client.CMU14.Stealth;

// marks an entity whose hover outline we pulled while it was cloaked, so it comes back on uncloak
[RegisterComponent]
public sealed partial class CMUCloakHiddenOutlineComponent : Component;

public sealed partial class CMUCloakHoverOutlineSystem : EntitySystem
{
    // the keyed post-shader API stacks the hover outline on top of the cloak shader,
    // so mousing over a cloaked pred or scout drew a crisp outline of them.
    // polled because YautjaCloakSystem already owns EntityActiveInvisible startup/shutdown
    public override void FrameUpdate(float frameTime)
    {
        var cloaked = EntityQueryEnumerator<EntityActiveInvisibleComponent, InteractionOutlineComponent>();
        while (cloaked.MoveNext(out var uid, out _, out _))
        {
            EnsureComp<CMUCloakHiddenOutlineComponent>(uid);
            RemCompDeferred<InteractionOutlineComponent>(uid);
        }

        var hidden = EntityQueryEnumerator<CMUCloakHiddenOutlineComponent>();
        while (hidden.MoveNext(out var uid, out _))
        {
            if (HasComp<EntityActiveInvisibleComponent>(uid) || TerminatingOrDeleted(uid))
                continue;

            RemCompDeferred<CMUCloakHiddenOutlineComponent>(uid);
            EnsureComp<InteractionOutlineComponent>(uid);
        }
    }
}
