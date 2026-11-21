using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.CorporateWork;

[Serializable, NetSerializable]
public enum CMUMarketsTerminalUi : byte
{
    Key,
}

/// <summary>
///     One minute of trading. The last candle in a quote is still forming.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct CMUMarketCandle(float Open, float High, float Low, float Close, int Volume);

[Serializable, NetSerializable]
public sealed class CMUMarketQuote(
    string id,
    CMUMarketKind kind,
    string symbol,
    string name,
    float price,
    float changePercent,
    int held,
    int averageCost,
    List<CMUMarketCandle> candles,
    List<int> newsCandles)
{
    public readonly string Id = id;
    public readonly CMUMarketKind Kind = kind;
    public readonly string Symbol = symbol;
    public readonly string Name = name;
    public readonly float Price = price;
    public readonly float ChangePercent = changePercent;
    public readonly int Held = held;
    public readonly int AverageCost = averageCost;
    public readonly List<CMUMarketCandle> Candles = candles;

    /// <summary>
    ///     Indices into <see cref="Candles"/> where news about this commodity broke.
    /// </summary>
    public readonly List<int> NewsCandles = newsCandles;
}

[Serializable, NetSerializable]
public readonly record struct CMUMarketNews(TimeSpan At, string Headline);

/// <summary>
///     Everything one trader sees: the shared board plus their own account and positions. Sent
///     to each viewer separately, since two civilians at one desktop see different accounts.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMUMarketsTerminalBuiState(
    List<CMUMarketQuote> quotes,
    List<CMUMarketNews> news,
    bool corporate,
    string? accountName,
    int balance,
    int holdingsValue,
    int holdingsCap,
    float feePercent,
    float dividendPercent,
    TimeSpan nextCandleAt) : BoundUserInterfaceMessage
{
    public readonly List<CMUMarketQuote> Quotes = quotes;
    public readonly List<CMUMarketNews> News = news;

    /// <summary>
    ///     True for the corporate terminal, which trades the corporate budget.
    /// </summary>
    public readonly bool Corporate = corporate;

    /// <summary>
    ///     The account being traded, or null if a civilian has no account to trade from.
    /// </summary>
    public readonly string? AccountName = accountName;

    public readonly int Balance = balance;
    public readonly int HoldingsValue = holdingsValue;
    public readonly int HoldingsCap = holdingsCap;
    public readonly float FeePercent = feePercent;
    public readonly float DividendPercent = dividendPercent;
    public readonly TimeSpan NextCandleAt = nextCandleAt;
}

[Serializable, NetSerializable]
public sealed class CMUMarketsTradeBuiMsg(string commodity, int units, bool buy) : BoundUserInterfaceMessage
{
    public readonly string Commodity = commodity;
    public readonly int Units = units;
    public readonly bool Buy = buy;
}

/// <summary>
///     Limits the client needs to know to build an order.
/// </summary>
public static class CMUMarketsLimits
{
    public const int MaxTradeUnits = 50;
}
