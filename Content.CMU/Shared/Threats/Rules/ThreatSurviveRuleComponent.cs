namespace Content.Shared.CMU14.Threats.Rules;

[RegisterComponent]
public sealed partial class ThreatSurviveRuleComponent : Component
{
    [DataField("minutes", required: true)]
    public float Minutes { get; private set; } = 10f;
}
