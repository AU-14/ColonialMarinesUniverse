namespace Content.Shared.AU14;

[RegisterComponent]
public sealed partial class KillAllHumanRuleComponent : Component
{
    /// <summary>
    /// Percentage of humans (humanoid mobs) that must be dead/arrested to trigger victory (0-100).
    /// Default 100 preserves original "all dead" behavior.
    /// </summary>
    [DataField("percent")]
    public int Percent = 100;

    /// <summary>
    /// If true, arrested (cuffed) humans count as eliminated when calculating victory percentage.
    /// </summary>
    [DataField("arrest")]
    public bool Arrest { get; set; } = false;

    [DataField("winMessage")]
    public string? WinMessage;
}

