namespace Content.Shared.AU14;

[RegisterComponent]
public sealed partial class KillAllClfRuleComponent : Component
{
    /// <summary>
    /// Percentage of CLF that must be dead to trigger victory (0-100).
    /// Default 100 preserves original "all dead" behavior.
    /// </summary>
    [DataField("percent")]
    public int Percent = 85;

    /// <summary>
    /// If true, arrested (cuffed) CLF count as killed when calculating victory percentage.
    /// </summary>
    [DataField("arrest")]
    public bool Arrest  {get; set; } =  true;

    [DataField("winMessage")]
    public string? WinMessage;
}

