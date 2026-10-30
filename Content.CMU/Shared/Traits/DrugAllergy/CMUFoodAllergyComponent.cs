using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Traits.DrugAllergy;

/// <summary>
///     Food allergies (nuts, dairy...). Eating anything containing one of an allergen's reagents, or one of its foods
///     that has no matching reagent, starts the same allergic reaction as a drug allergy, cured with epinephrine.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUFoodAllergyComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<ProtoId<CMUFoodAllergenPrototype>> Allergens = new();
}

[Prototype("cmuFoodAllergen")]
public sealed partial class CMUFoodAllergenPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    ///     Shown on the medical alert tag and medical record, e.g. "nuts".
    /// </summary>
    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public HashSet<ProtoId<ReagentPrototype>> Reagents = new();

    [DataField]
    public HashSet<EntProtoId> Foods = new();
}
