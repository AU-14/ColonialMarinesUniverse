namespace Content.Shared.AU14;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class AnalyzerComponent : Component
{
    /// <summary>
    /// The faction this analyzer belongs to (e.g. "govfor", "opfor", "clf", "scientist").
    /// Only fetch objectives assigned to this faction (or faction-neutral objectives) will be
    /// picked up by the Scan action. Leave empty to match all factions.
    /// </summary>
    [DataField("faction")]
    public string Faction { get; set; } = string.Empty;


    public int CashStored = 0;
}
