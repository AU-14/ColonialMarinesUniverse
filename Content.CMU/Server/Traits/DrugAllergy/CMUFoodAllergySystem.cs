using Content.Shared.CMU14.Traits.DrugAllergy;
using Content.Shared.Nutrition;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Traits.DrugAllergy;

public sealed class CMUFoodAllergySystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private AllergicReactionSystem _allergicReaction = default!;
    [Dependency] private DrugAllergyTraitSystem _drugAllergy = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUFoodAllergyComponent, IngestingEvent>(OnIngesting);
    }

    /// <summary>
    ///     Adds a food allergy, or a random one the entity doesn't already have if <paramref name="allergen"/> is null.
    /// </summary>
    public void AddAllergen(EntityUid uid, ProtoId<CMUFoodAllergenPrototype>? allergen)
    {
        var comp = EnsureComp<CMUFoodAllergyComponent>(uid);
        if (allergen == null)
        {
            var pool = new List<ProtoId<CMUFoodAllergenPrototype>>();
            foreach (var option in _prototypes.EnumeratePrototypes<CMUFoodAllergenPrototype>())
            {
                if (!comp.Allergens.Contains(option.ID))
                    pool.Add(option.ID);
            }

            if (pool.Count == 0)
                return;

            allergen = _random.Pick(pool);
        }

        if (comp.Allergens.Contains(allergen.Value) || !_prototypes.TryIndex(allergen.Value, out var proto))
            return;

        comp.Allergens.Add(allergen.Value);
        Dirty(uid, comp);
        _drugAllergy.AddTagLine(uid, Loc.GetString(proto.Name));
    }

    private void OnIngesting(Entity<CMUFoodAllergyComponent> ent, ref IngestingEvent args)
    {
        if (FindAllergen(ent.Comp, args) is not { } allergen)
            return;

        // The reaction itself lives on DrugAllergyComponent. Added here rather than by the trait so it can't replace
        // or be replaced by a drug allergy trait; an empty pool keeps it from rolling a random drug allergen.
        if (!HasComp<DrugAllergyComponent>(ent))
        {
            var allergy = Factory.GetComponent<DrugAllergyComponent>();
            allergy.Pool.Clear();
            AddComp(ent, allergy);
        }

        _allergicReaction.TriggerReaction(ent, Loc.GetString(allergen.Name));
    }

    private CMUFoodAllergenPrototype? FindAllergen(CMUFoodAllergyComponent comp, IngestingEvent args)
    {
        var food = MetaData(args.Food).EntityPrototype?.ID;
        foreach (var id in comp.Allergens)
        {
            if (!_prototypes.TryIndex(id, out var allergen))
                continue;

            if (food != null && allergen.Foods.Contains(food))
                return allergen;

            foreach (var reagent in args.Split.Contents)
            {
                if (allergen.Reagents.Contains(reagent.Reagent.Prototype))
                    return allergen;
            }
        }

        return null;
    }
}

/// <summary>
///     Trait special that adds one food allergy, so several allergy traits stack on one component. Leave
///     <see cref="Allergen"/> empty for a random one.
/// </summary>
public sealed partial class CMUAddFoodAllergySpecial : JobSpecial
{
    [DataField]
    public ProtoId<CMUFoodAllergenPrototype>? Allergen;

    public override void AfterEquip(EntityUid mob)
    {
        IoCManager.Resolve<IEntityManager>().System<CMUFoodAllergySystem>().AddAllergen(mob, Allergen);
    }
}
