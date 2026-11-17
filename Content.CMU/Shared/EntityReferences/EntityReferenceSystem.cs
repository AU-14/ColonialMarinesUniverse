namespace Content.Shared.CMU14.EntityReferences;

/// <summary>Notifies reference owners during deletion, before network state can serialize a stale entity.</summary>
public sealed class EntityReferenceSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<ReferencedEntityComponent, EntityTerminatingEvent>(OnTerminating);
    }

    public void Watch(EntityUid owner, EntityUid target)
        => WatchNullable(owner, target);

    public EntityUid? WatchNullable(EntityUid owner, EntityUid? target)
    {
        if (target is not { } uid || TerminatingOrDeleted(uid) || TerminatingOrDeleted(owner))
            return null;

        EnsureComp<ReferencedEntityComponent>(uid).Observers.Add(owner);
        return uid;
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
