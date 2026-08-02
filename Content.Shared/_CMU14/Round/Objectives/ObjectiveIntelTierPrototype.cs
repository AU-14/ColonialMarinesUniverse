using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Round.Objectives;

[Prototype]
public sealed partial class ObjectiveIntelTierPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>When true, display obj markers on the tactical map for this tier</summary>
    [DataField] public bool DisplayOnTacMap { get; set; }

    /// <summary>
    /// Overrides title when the tier is unlocked:
    ///     When empty: previous tier's title (or obj default)
    ///     If "Unknown": obj is considered unknown/hidden
    /// </summary>
    [DataField("title")]
    public string TitleText { get; set; } = string.Empty;

    /// <summary>
    /// Overrides description when the tier is unlocked:
    ///     When empty: previous tier's description (or default)
    /// </summary>
    [DataField("description")]
    public string DescriptionText { get; set; } = string.Empty;

    /// <summary>When true, list relevant coords in the UI instead/additional to tacmap markers.</summary>
    [DataField] public bool ListCoords { get; set; }

    [DataField] public double CostToUnlock { get; set; } = 0.5;
}
