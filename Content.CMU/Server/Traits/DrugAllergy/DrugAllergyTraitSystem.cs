using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared._RMC14.Marines.Dogtags;
using Content.Shared._CMU14.Traits.DrugAllergy;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Random;

namespace Content.Server._CMU14.Traits.DrugAllergy;

public sealed partial class DrugAllergyTraitSystem : EntitySystem
{
    [Dependency] private RMCReagentSystem _reagent = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DrugAllergyComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<DrugAllergyComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.Allergen == null && ent.Comp.Pool.Count > 0)
        {
            ent.Comp.Allergen = _random.Pick(ent.Comp.Pool);
            Dirty(ent);
        }

        if (ent.Comp.Allergen is not { } allergen || !_reagent.TryIndex(allergen, out var reagent))
            return;

        var tag = Spawn(ent.Comp.DogtagPrototype, Transform(ent).Coordinates);
        var tags = EnsureComp<InformationTagsComponent>(tag);
        tags.Tags.Add(new InfoTagInfo
        {
            Name = Loc.GetString("au14-drug-allergy-tag-name"),
            Assignment = Loc.GetString("au14-drug-allergy-tag-text", ("reagent", reagent.LocalizedName)),
            BloodType = "",
        });
        Dirty(tag, tags);

        _hands.TryPickupAnyHand(ent.Owner, tag);
    }
}
