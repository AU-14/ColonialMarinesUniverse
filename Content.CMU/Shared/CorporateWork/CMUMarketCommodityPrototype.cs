using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.CorporateWork;

/// <summary>
///     What a listing is. Goods trade on both the corporate terminal and the civilian app;
///     company stock only on the civilian app.
/// </summary>
public enum CMUMarketKind : byte
{
    Goods,
    Stock,
}

/// <summary>
///     Something traded on the markets: a good, commodity or service, or a company's stock.
/// </summary>
[Prototype("cmuMarketCommodity")]
public sealed partial class CMUMarketCommodityPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public CMUMarketKind Kind = CMUMarketKind.Goods;

    /// <summary>
    ///     Ticker shown on the board, e.g. PHR.
    /// </summary>
    [DataField(required: true)]
    public string Symbol = string.Empty;

    [DataField(required: true)]
    public int BasePrice;

    /// <summary>
    ///     Largest random move per market tick, as a fraction of the price.
    /// </summary>
    [DataField]
    public float Volatility = 0.05f;

    /// <summary>
    ///     How much money it takes to move the price. A trade worth this much moves it 100%,
    ///     before the per-trade cap. Lower means a thinner, jumpier market.
    /// </summary>
    [DataField]
    public float Liquidity = 40000f;

    /// <summary>
    ///     For a company: the round forces (job <c>roundForce</c>) that work for it. When any of
    ///     them are in the round, what happens to them moves the stock.
    /// </summary>
    [DataField]
    public List<string> RoundForces = new();

    /// <summary>
    ///     For a company: the lines of business it competes in. When a company in the round has a
    ///     setback in a sector, a rival in that sector that isn't in the round moves to fill the gap.
    /// </summary>
    [DataField]
    public List<string> Sectors = new();
}
