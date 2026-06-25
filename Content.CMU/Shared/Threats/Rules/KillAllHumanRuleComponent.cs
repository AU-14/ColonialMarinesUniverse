namespace Content.Shared._CMU14.Threats.Rules;

[RegisterComponent]
public sealed partial class KillAllHumanRuleComponent : Component
{
    /// <summary>
    ///     Percentage of humans (humanoid mobs) that must be dead/arrested to trigger victory (0-100).
    ///     Default 100 preserves original "all dead" behavior.
    /// </summary>
    [DataField("percent")]
    public int Percent = 100;

    [DataField("winMessage")]
    public string? WinMessage;

    /// <summary>
    ///     If true, arrested (cuffed) humans count as eliminated when calculating victory percentage.
    /// </summary>
    [DataField("arrest")]
    public bool Arrest { get; set; }
}
