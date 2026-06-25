namespace Content.Shared._CMU14.Threats.Rules;

[RegisterComponent]
public sealed partial class KillAllClfRuleComponent : Component
{
    /// <summary>
    ///     Percentage of CLF that must be dead to trigger victory (0-100).
    ///     Default 100 preserves original "all dead" behavior.
    /// </summary>
    [DataField("percent")]
    public int Percent = 85;

    [DataField("winMessage")]
    public string? WinMessage;

    /// <summary>
    ///     If true, arrested (cuffed) CLF count as killed when calculating victory percentage.
    /// </summary>
    [DataField("arrest")]
    public bool Arrest { get; set; } = true;
}
