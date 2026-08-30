namespace Content.Shared.CMU14.Threats.Rules;

[RegisterComponent]
public sealed partial class KillAllYautjaRuleComponent : Component
{
    [DataField("percent")]
    public int Percent = 100;
}
