using Content.Shared.CMU14.EntityReferences;

namespace Content.Shared._RMC14.Targeting;

public abstract partial class SharedRMCTargetingSystem
{
    [Dependency] private EntityReferenceSystem _cmuReferences = default!;

    private void InitializeCMUTargetingLifetime()
    {
        SubscribeLocalEvent<TargetingComponent, ReferencedEntityTerminatingEvent>(OnCMUTargetingReferenceTerminating);
    }

    private void WatchCMUTargetingLifetime(Entity<TargetingComponent> ent)
    {
        if (_net.IsClient)
            return;

        _cmuReferences.Watch(ent, ent.Comp.User);
        _cmuReferences.Watch(ent, ent.Comp.Origin.EntityId);
    }

    private void OnCMUTargetingReferenceTerminating(Entity<TargetingComponent> ent, ref ReferencedEntityTerminatingEvent args)
    {
        if (_net.IsClient || ent.Comp.User != args.Entity && ent.Comp.Origin.EntityId != args.Entity)
            return;

        var cancelled = new TargetingCancelledEvent();
        RaiseLocalEvent(ent, ref cancelled);
        // Component removal releases the reverse target markers as well.
        RemComp<TargetingComponent>(ent);
    }
}
