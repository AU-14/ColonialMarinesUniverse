using Content.Shared.Alert;
using Content.Shared.CMU14.Chemistry.Reagent;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Traits.DrugAllergy;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DrugAllergyComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<ReagentPrototype>? Allergen;

    [DataField]
    public List<ProtoId<ReagentPrototype>> Pool = new()
    {
        "CMInaprovaline",
        "CMUOxycodone",
        "CMUTramadol",
        "CMUParacetamol",
        "CMUFentanyl",
        "CMImidazoline",
        "CMLeporazine",
        "CMEthylredoxrazine",
    };

    /// <summary>
    ///     Chemical properties that cross-react with each allergen in <see cref="Pool"/>. An Allergenic
    ///     reagent carrying one of the listed properties (by its effect type) triggers the reaction as if it
    ///     were the allergen itself, which is how research chemicals, whose IDs are generated at runtime, can
    ///     set off a pre-rolled allergy. Reagents of the allergen's own class cross-react too.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, List<ProtoId<ReagentPropertyPrototype>>> CrossReactiveProperties = new()
    {
        // Collection initialisers, not collection expressions: `List<T> x = [..]` compiles to
        // CollectionsMarshal.SetCount, which the client sandbox rejects.
        ["CMInaprovaline"] = new() { "Cardiostabilizing", "Cardiopeutic" },
        ["CMUOxycodone"] = new() { "Painkilling", "Addictive" },
        ["CMUTramadol"] = new() { "Painkilling" },
        ["CMUParacetamol"] = new() { "Painkilling" },
    };

    [DataField]
    public EntProtoId DogtagPrototype = "CMUAllergyDogtag";

    /// <summary>
    ///     Whether the allergic reaction is currently active. Set when the allergen is
    ///     ingested, cleared entirely when treated with epinephrine.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ReactionActive;

    [DataField, AutoNetworkedField]
    public TimeSpan ReactionStartTime;

    [DataField]
    public TimeSpan TimeBetweenTicks = TimeSpan.FromSeconds(3);

    [DataField, AutoNetworkedField]
    public TimeSpan NextTick;

    [DataField]
    public FixedPoint2 ToxinDamagePerTick = 1.5;

    /// <summary>
    ///     How long after the reaction starts before Asphyxiation damage and random stunning begin.
    /// </summary>
    [DataField]
    public TimeSpan AsphyxiationDelay = TimeSpan.FromSeconds(30);

    [DataField]
    public FixedPoint2 AsphyxiationDamagePerTick = 1.5;

    [DataField]
    public float StunMinDuration = 5;

    [DataField]
    public float StunMaxDuration = 10;

    [DataField]
    public TimeSpan StunIntervalMin = TimeSpan.FromSeconds(45);

    [DataField]
    public TimeSpan StunIntervalMax = TimeSpan.FromSeconds(75);

    [DataField, AutoNetworkedField]
    public TimeSpan NextStunCheck;

    [DataField]
    public ProtoId<AlertPrototype> ReactionAlert = "CMUAllergicReaction";

    /// <summary>
    ///     Visible radius (in tiles) while the reaction dims vision. Lower is darker.
    /// </summary>
    [DataField]
    public float VisionDimRadius = 3f;

    [DataField]
    public TimeSpan MessageCooldown = TimeSpan.FromSeconds(30);

    [ViewVariables]
    public TimeSpan NextMessage;

    /// <summary>
    ///     Minimum time between a reaction being triggered or cured. Prevents a rapid
    ///     trigger/cure loop if the allergen and a cure are both still metabolizing at once.
    /// </summary>
    [DataField]
    public TimeSpan TriggerCooldown = TimeSpan.FromSeconds(7);

    [DataField, AutoNetworkedField]
    public TimeSpan NextTriggerAllowed;
}
