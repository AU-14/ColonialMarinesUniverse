using Content.Shared.Alert;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Traits.DrugAllergy;

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
    };

    [DataField]
    public EntProtoId DogtagPrototype = "CMUAllergyDogtag";

    /// <summary>
    ///     Whether the allergic reaction is currently active. Set when the allergen is
    ///     ingested, cleared entirely when treated with naloxone.
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
