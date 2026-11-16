using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.ColonyEconomy;

/// <summary>
/// Defines the one canonical ASRS unit price for a tangible RUMC product.
/// Product names are resolved to stack and entity prototypes when the server starts.
/// </summary>
[Prototype("cmuIndustryPrice")]
public sealed partial class CMUIndustryPricePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Product { get; private set; } = string.Empty;

    [DataField(required: true)]
    public double Price { get; private set; }
}
