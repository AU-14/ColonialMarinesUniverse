namespace Content.Shared.AU14;

[RegisterComponent]
public sealed partial class KillAllApeRuleComponent : Component
{
    /// <summary>
    /// Percentage of Apes that must be dead to trigger victory (0-100).
    /// Default 100 preserves original "all dead" behavior.
    /// </summary>
    [DataField("percent")]
    public int Percent = 100;
}

