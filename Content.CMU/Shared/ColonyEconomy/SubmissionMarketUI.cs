using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ColonyEconomy;

[Serializable, NetSerializable]
public enum SubmissionMarketUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum CommodityEventKind : byte
{
    None,
    Boom,
    Glut,
}

[Serializable, NetSerializable]
public sealed class SubmissionMarketRow
{
    public ProtoId<CMUCommodityPrototype> Commodity { get; }

    // market multiplier on the base price, events included. trade pacts are on the state
    public float Multiplier { get; }

    // one multiplier sample a minute, oldest first
    public float[] History { get; }

    public CommodityEventKind Event { get; }

    // what the boom or glut alone does to the price, 1 when there's none
    public float EventMultiplier { get; }
    public TimeSpan EventEnds { get; }

    public SubmissionMarketRow(ProtoId<CMUCommodityPrototype> commodity, float multiplier, float[] history, CommodityEventKind ev, float eventMultiplier, TimeSpan eventEnds)
    {
        Commodity = commodity;
        Multiplier = multiplier;
        History = history;
        Event = ev;
        EventMultiplier = eventMultiplier;
        EventEnds = eventEnds;
    }
}

[Serializable, NetSerializable]
public sealed class SubmissionMarketBuiState : BoundUserInterfaceState
{
    // what this point buys
    public List<SubmissionMarketRow> Rows { get; }

    // booms and gluts on things another point buys, so people know to walk over
    public List<SubmissionMarketRow> Elsewhere { get; }

    public float TradeMultiplier { get; }
    public float Tariff { get; }

    public SubmissionMarketBuiState(List<SubmissionMarketRow> rows, List<SubmissionMarketRow> elsewhere, float tradeMultiplier, float tariff)
    {
        Rows = rows;
        Elsewhere = elsewhere;
        TradeMultiplier = tradeMultiplier;
        Tariff = tariff;
    }
}
