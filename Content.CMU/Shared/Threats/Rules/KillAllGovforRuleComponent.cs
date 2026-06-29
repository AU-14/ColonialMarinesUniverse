namespace Content.Shared._CMU14.Threats.Rules;

[RegisterComponent]
public sealed partial class KillAllGovforRuleComponent : Component
{
    /// <summary>
    ///     Percentage of Govfor that must be dead to trigger victory (0-100).
    ///     Default 100 preserves original "all dead" behavior.
    /// </summary>
    [DataField("percent")]
    public int Percent = 100;

    [DataField("winMessage")]
    public string? WinMessage;

    /// <summary>
    /// </summary>
    [DataField("arrest")]
    public bool Arrest { get; set; }
}
