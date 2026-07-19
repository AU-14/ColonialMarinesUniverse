using Robust.Shared.Prototypes;

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

    /// <summary>
    /// Configurable submittable-for-points table. When non-empty, these entities convert into points at
    /// their own ratios. Set at deploy time from the active INSFOR faction. Empty keeps the classic
    /// "dollars at the built-in rate" behavior.
    /// </summary>
    [DataField]
    public List<AnalyzerConversionEntry> Conversions = new();

    /// <summary>
    /// Whether plain dollars still convert at the built-in rate even when custom conversions are set. Set
    /// from the faction at deploy time. Default true so cash is never silently disabled.
    /// </summary>
    [DataField]
    public bool IncludeDollars = true;

    /// <summary>
    /// Per-entity banked remainder that has not yet reached a whole point, keyed by entity prototype id.
    /// Server-only bookkeeping so partial submissions of different goods do not spill into each other.
    /// </summary>
    public Dictionary<string, int> Banked = new();
}

/// <summary>
/// One configured conversion. In amount-per-point mode <see cref="AmountPerPoint"/> of <see cref="Entity"/>
/// earns one point; in <see cref="PointsPerItemMode"/> each entity is worth <see cref="PointsPerItem"/>.
/// </summary>
[DataDefinition]
public sealed partial class AnalyzerConversionEntry
{
    [DataField]
    public EntProtoId Entity;

    [DataField]
    public bool PointsPerItemMode;

    [DataField]
    public int AmountPerPoint = 15;

    [DataField]
    public int PointsPerItem = 1;
}
