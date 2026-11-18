using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.ColonyEconomy;

/// <summary>
/// Defines the one canonical ASRS unit price for a tangible RUMC product.
/// Each price refers to a stack type or a specific non-stack entity prototype.
/// </summary>
[Prototype("cmuIndustryPrice")]
public sealed partial class CMUIndustryPricePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Product { get; private set; } = string.Empty;

    [DataField]
    public ProtoId<StackPrototype>? Stack { get; private set; }

    [DataField]
    public EntProtoId? Entity { get; private set; }

    [DataField(required: true)]
    public double Price { get; private set; }
}
