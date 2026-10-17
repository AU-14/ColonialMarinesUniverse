namespace Content.Shared.CMU14.EntityReferences;

/// <summary>Notifies reference owners during deletion, before network state can serialize a stale entity.</summary>
public sealed class EntityReferenceSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<ReferencedEntityComponent, EntityTerminatingEvent>(OnTerminating);
    }

    public void Watch(EntityUid owner, EntityUid target)
    {
        if (!TerminatingOrDeleted(owner) && !TerminatingOrDeleted(target))
            EnsureComp<ReferencedEntityComponent>(target).Observers.Add(owner);
    }

    private void OnTerminating(Entity<ReferencedEntityComponent> ent, ref EntityTerminatingEvent args)
    {
        foreach (var owner in ent.Comp.Observers)
        {
            if (TerminatingOrDeleted(owner))
                continue;

            var ev = new ReferencedEntityTerminatingEvent(ent.Owner);
            RaiseLocalEvent(owner, ref ev);
        }
    }
}
