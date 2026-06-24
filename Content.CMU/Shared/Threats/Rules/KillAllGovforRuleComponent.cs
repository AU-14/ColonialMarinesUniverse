namespace Content.Shared.AU14;

[RegisterComponent]
public sealed partial class KillAllGovforRuleComponent : Component
{
    /// <summary>
    /// Percentage of Govfor that must be dead to trigger victory (0-100).
    /// Default 100 preserves original "all dead" behavior.
    /// </summary>
    [DataField("percent")]
    public int Percent = 100;

    /// <summary>
    /// </summary>
    [DataField("arrest")]
    public bool Arrest { get; set; } = false;

    [DataField("winMessage")]
    public string? WinMessage;
}
