using System.Text;
using Content.Shared.CMU14.Traits.DrugAllergy;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.CMU14.CharacterDescription;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.CharacterDescription;

public static class MedicalRecordBuilder
{
    public static string Build(IEntityManager entManager, RMCReagentSystem reagentSystem, EntityUid uid, CharacterDescriptionComponent desc)
    {
        var builder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(desc.MedicalRecord))
            builder.Append(desc.MedicalRecord);

        foreach (var traitName in desc.DisabilityTraitNames)
        {
            if (builder.Length > 0)
            {
                builder.Append('\n');
                builder.Append('\n');
            }

            builder.Append(Loc.GetString("medical-record-disability-line", ("trait", traitName)));
        }

        if (desc.HasDrugAllergyTrait &&
            entManager.TryGetComponent(uid, out DrugAllergyComponent? allergy))
        {
            var allergens = new List<ProtoId<ReagentPrototype>>(allergy.ExtraAllergens);
            if (allergy.Allergen is { } allergen)
                allergens.Insert(0, allergen);

            foreach (var id in allergens)
            {
                if (!reagentSystem.TryIndex(id, out var reagent))
                    continue;

                if (builder.Length > 0)
                {
                    builder.Append('\n');
                    builder.Append('\n');
                }

                builder.Append(Loc.GetString("medical-record-allergy-line", ("reagent", reagent.LocalizedName)));
            }
        }

        if (entManager.TryGetComponent(uid, out CMUFoodAllergyComponent? foodAllergy))
        {
            var prototypes = IoCManager.Resolve<IPrototypeManager>();
            foreach (var id in foodAllergy.Allergens)
            {
                if (!prototypes.TryIndex(id, out var foodAllergen))
                    continue;

                if (builder.Length > 0)
                {
                    builder.Append('\n');
                    builder.Append('\n');
                }

                builder.Append(Loc.GetString("medical-record-allergy-line", ("reagent", Loc.GetString(foodAllergen.Name))));
            }
        }

        return builder.ToString();
    }
}
