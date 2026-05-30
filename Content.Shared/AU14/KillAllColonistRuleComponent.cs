namespace Content.Shared.AU14;

[RegisterComponent]
public sealed partial class KillAllColonistRuleComponent : Component
{
    /// <summary>
    /// Percentage of AUColonists that must be dead to trigger victory (0-100).
    /// Default 100 preserves original "all dead" behavior.
    /// </summary>
    [DataField("percent")]
    public int Percent = 100;

    /// <summary>
    /// Percentage of AUColonists that must be dead before colony evacuation is enabled.
    /// Set to 0 to disable threshold-based evac trigger.
    /// </summary>
    [DataField]
    public int ColonyEvacThreshold = 50;

    /// <summary>Tracks whether colony evacuation has been triggered by the death threshold.</summary>
    [DataField]
    public bool ColonyEvacTriggered;
}
