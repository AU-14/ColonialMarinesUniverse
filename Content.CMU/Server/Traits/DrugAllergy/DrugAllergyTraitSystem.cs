using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared._RMC14.Marines.Dogtags;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.CMU14.Traits.DrugAllergy;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Traits.DrugAllergy;

public sealed partial class DrugAllergyTraitSystem : EntitySystem
{
    private static readonly EntProtoId DogtagPrototype = "CMUAllergyDogtag";

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

        if (ent.Comp.Allergen is { } allergen && _reagent.TryIndex(allergen, out var reagent))
            AddTagLine(ent, reagent.LocalizedName);
    }

    /// <summary>
    ///     Adds a drug allergy, or a random one from the pool if <paramref name="allergen"/> is null. Stacks with an
    ///     allergy the entity already has.
    /// </summary>
    public void AddAllergy(EntityUid uid, ProtoId<ReagentPrototype>? allergen)
    {
        if (!TryComp(uid, out DrugAllergyComponent? comp))
        {
            comp = Factory.GetComponent<DrugAllergyComponent>();
            comp.Allergen = allergen;
            AddComp(uid, comp);
            return;
        }

        if (allergen == null)
        {
            var pool = new List<ProtoId<ReagentPrototype>>();
            foreach (var option in comp.Pool)
            {
                if (!comp.IsAllergen(option))
                    pool.Add(option);
            }

            if (pool.Count == 0)
                return;

            allergen = _random.Pick(pool);
        }

        if (comp.IsAllergen(allergen.Value))
            return;

        if (comp.Allergen == null)
            comp.Allergen = allergen;
        else
            comp.ExtraAllergens.Add(allergen.Value);

        Dirty(uid, comp);

        if (_reagent.TryIndex(allergen.Value, out var reagent))
            AddTagLine(uid, reagent.LocalizedName);
    }

    /// <summary>
    ///     Writes an allergy onto the entity's medical alert tag, spawning the tag into their hands the first time.
    /// </summary>
    public void AddTagLine(EntityUid uid, string allergen)
    {
        var holder = EnsureComp<CMUAllergyTagHolderComponent>(uid);
        var newTag = !Exists(holder.Tag);
        if (newTag)
            holder.Tag = Spawn(DogtagPrototype, Transform(uid).Coordinates);

        var tag = holder.Tag!.Value;
        var tags = EnsureComp<InformationTagsComponent>(tag);
        tags.Tags.Add(new InfoTagInfo
        {
            Name = Loc.GetString("au14-drug-allergy-tag-name"),
            Assignment = Loc.GetString("au14-drug-allergy-tag-text", ("reagent", allergen)),
            BloodType = "",
        });
        Dirty(tag, tags);

        if (newTag)
            _hands.TryPickupAnyHand(uid, tag);
    }
}

/// <summary>
///     The medical alert tag an allergic character spawned with, so every allergy goes on the same tag.
/// </summary>
[RegisterComponent]
public sealed partial class CMUAllergyTagHolderComponent : Component
{
    [DataField]
    public EntityUid? Tag;
}

/// <summary>
///     Trait special that adds a drug allergy, so two allergy traits stack instead of the second being dropped.
///     Leave <see cref="Allergen"/> empty for a random one.
/// </summary>
public sealed partial class CMUAddDrugAllergySpecial : JobSpecial
{
    [DataField]
    public ProtoId<ReagentPrototype>? Allergen;

    public override void AfterEquip(EntityUid mob)
    {
        IoCManager.Resolve<IEntityManager>().System<DrugAllergyTraitSystem>().AddAllergy(mob, Allergen);
    }
}
