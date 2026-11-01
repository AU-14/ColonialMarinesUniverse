using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.ColonyEconomy;

// something the submission points buy. the live price is this times the market multiplier,
// see SubmissionMarketSystem
[Prototype("cmuCommodity")]
public sealed partial class CMUCommodityPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    // entity drawn next to the price on the board
    [DataField(required: true)]
    public EntProtoId Icon;

    [DataField(required: true)]
    public List<ProtoId<CMUCommodityCategoryPrototype>> Categories = new();

    // base price per unit for each tag. several tags can share one market, the salvage grades do
    [DataField(required: true)]
    public Dictionary<ProtoId<TagPrototype>, float> Prices = new();

    // only paid when no regular commodity matches. for the generic fruit/vegetable tags,
    // otherwise a tomato would sell at the generic price
    [DataField]
    public bool Fallback;

    // base dollars sold back to back that halve the price. null uses cmu.market_saturation
    [DataField]
    public float? Saturation;

    // can roll buyer booms and gluts
    [DataField]
    public bool Events = true;
}

[Prototype("cmuCommodityCategory")]
public sealed partial class CMUCommodityCategoryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    // board section order, low first
    [DataField]
    public int Order;
}
