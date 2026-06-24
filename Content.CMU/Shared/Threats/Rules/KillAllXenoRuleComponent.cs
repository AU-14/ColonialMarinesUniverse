namespace Content.Shared.AU14;

[RegisterComponent]
public sealed partial class KillAllXenoRuleComponent : Component
{
    /// <summary>
    /// Percentage of Xenos that must be dead to trigger victory (0-100).
    /// Default 100 preserves original "all dead" behavior.
    /// </summary>
    [DataField("percentXeno")]
    public int PercentXeno = 100;

    /// <summary>
    /// Percentage of Cultists that must be dead to trigger victory (0-100).
    /// Default 100 preserves original "all dead" behavior.
    /// </summary>
    [DataField("percentCultist")]
    public int PercentCultist = 60;

    // Backwards-compatible field. If a prototype still uses 'percent', deserialize here
    // and apply it to both PercentXeno and PercentCultist.

}


