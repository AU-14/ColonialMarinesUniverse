using Content.Shared.CMU14.EntityReferences;

namespace Content.Shared._RMC14.Marines.Squads;

public sealed partial class SquadSystem
{
    [Dependency] private EntityReferenceSystem _references = default!;

    private void InitializeCardOwnerLifetime()
    {
        if (_net.IsClient)
            return;

        SubscribeLocalEvent<IdCardOwnerComponent, ComponentStartup>(OnCardOwnerStartup);
        SubscribeLocalEvent<IdCardOwnerComponent, ReferencedEntityTerminatingEvent>(OnCardOwnerTerminating);
    }

    private void SetCardOwner(Entity<IdCardOwnerComponent> card, EntityUid owner)
    {
        card.Comp.Id = _net.IsClient ? owner : _references.WatchNullable(card, owner) ?? EntityUid.Invalid;
    }

    private void OnCardOwnerStartup(Entity<IdCardOwnerComponent> card, ref ComponentStartup args)
    {
        if (card.Comp.Id.IsValid())
            SetCardOwner(card, card.Comp.Id);
    }

    private void OnCardOwnerTerminating(Entity<IdCardOwnerComponent> card, ref ReferencedEntityTerminatingEvent args)
    {
        if (card.Comp.Id != args.Entity)
            return;

        card.Comp.Id = EntityUid.Invalid;
        Dirty(card);
    }
}
