using Robust.Shared.GameStates;

namespace Content.Shared.AU14;

[RegisterComponent]
public sealed partial class ThreatSurviveRuleComponent : Component
{
    [DataField("minutes", required: true)]
    public float Minutes { get; private set; } = 10f;
}

