using System.Text;
using Content.Shared._CMU14.Traits.DrugAllergy;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared._CMU14.CharacterDescription;

namespace Content.Server._CMU14.CharacterDescription;

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
            entManager.TryGetComponent(uid, out DrugAllergyComponent? allergy) &&
            allergy.Allergen is { } allergen &&
            reagentSystem.TryIndex(allergen, out var reagent))
        {
            if (builder.Length > 0)
            {
                builder.Append('\n');
                builder.Append('\n');
            }

            builder.Append(Loc.GetString("medical-record-allergy-line", ("reagent", reagent.LocalizedName)));
        }

        return builder.ToString();
    }
}
