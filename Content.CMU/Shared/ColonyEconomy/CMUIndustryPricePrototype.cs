using Content.Shared.Chemistry.Reagent;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.ColonyEconomy;

/// <summary>
/// Defines the one canonical ASRS unit price for a tangible RUMC product.
/// Each price refers to a stack type, a specific non-stack entity prototype or a liquid reagent.
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

    // price per unit of a liquid sold in a tank or canister. only liquids listed here pay, so food or
    // chem dispenser reagents poured into a tank are worth nothing
    [DataField]
    public ProtoId<ReagentPrototype>? Reagent { get; private set; }

    [DataField(required: true)]
    public double Price { get; private set; }

    /// <summary>False retains an intermediate's production cost without offering it for sale.</summary>
    [DataField]
    public bool Sellable { get; private set; } = true;

    /// <summary>Maximum ASRS unit payout for ordinary stock of this stack, including requisitions supplies.</summary>
    [DataField]
    public double? UnmarkedPriceCap { get; private set; }
}
