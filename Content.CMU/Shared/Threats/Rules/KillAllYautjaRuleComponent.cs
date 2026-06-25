namespace Content.Shared._CMU14.Threats.Rules;

[RegisterComponent]
public sealed partial class KillAllYautjaRuleComponent : Component
{
    [DataField("percent")]
    public int Percent = 100;
}
