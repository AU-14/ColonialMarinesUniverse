using System.Linq;
using Content.Server.CMU14.ColonyEconomy;
using Content.Server.Popups;
using Content.Shared.CMU14.CorporateWork;
using Content.Shared.GameTicking;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.CorporateWork;

/// <summary>
///     One market for the whole round, shared by the corporate terminal and the civilian app.
///     Goods (commodities and services) trade on both; company stock only on the civilian app.
///
///     Prices move in one-minute candles, stepping every few seconds inside each candle. News
///     names a commodity and a direction ahead of the move: the corporate terminal sees it as soon
///     as it breaks, civilians only shortly before the move starts. Every trade moves the price by
///     its size against the commodity's liquidity and fills at the midpoint of that move, so a
///     big buyer pushes the price up for everyone, and no trade can be undone at a profit.
/// </summary>
public sealed partial class CMUMarketsSystem : EntitySystem
{
    [Dependency] private CorporateConsoleSystem _corporate = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public static readonly TimeSpan CandleLength = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan StepLength = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     How long before a move civilians get to read the news about it.
    /// </summary>
    public static readonly TimeSpan CivilianNewsLead = TimeSpan.FromSeconds(10);

    public const float Fee = 0.02f;

    /// <summary>
    ///     Paid to every holder at the close of each candle, as a fraction of the value held.
    /// </summary>
    public const float Dividend = 0.005f;

    public const int CorporateHoldingsCap = 10000;
    public const int CivilianHoldingsCap = 1500;
    public const int MaxTradeUnits = CMUMarketsLimits.MaxTradeUnits;

    /// <summary>
    ///     The most a single trade can move a price.
    /// </summary>
    public const float MaxImpact = 0.08f;

    /// <summary>
    ///     How far a news event pushes its commodity in each candle it lasts.
    /// </summary>
    public const float NewsMove = 0.10f;

    public const string CorporateAccount = "corporate";

    /// <summary>
    ///     The round force whose company stock follows how the corporation is doing.
    /// </summary>
    public const string CorporateForce = "WY";

    private const int NewsCandles = 3;
    private const int NewsKept = 8;
    private const int CandlesKept = 90;
    private const float MeanReversion = 0.05f;
    private const float StepNoise = 0.15f;
    private const float MinPriceFactor = 0.4f;
    private const float MaxPriceFactor = 2.5f;

    private readonly Dictionary<string, Market> _markets = new();
    private readonly List<NewsItem> _news = new();
    private readonly Dictionary<string, Dictionary<string, Position>> _portfolios = new();
    private TimeSpan _nextCandle;
    private TimeSpan _nextStep;
    private TimeSpan _nextNews;
    private bool _started;
    private int _civilianNewsShown;

    private sealed class Market
    {
        public required CMUMarketCommodityPrototype Commodity;
        public float Price;

        /// <summary>
        ///     What the market thinks this is worth, which prices drift back towards. Starts at the
        ///     base price; lasting events in the round (a withdrawal, research income) move it.
        /// </summary>
        public float Fundamental;
        public readonly List<CMUMarketCandle> Candles = new();
        public CMUMarketCandle Current;

        /// <summary>
        ///     Change spread over the steps of the current candle.
        /// </summary>
        public float StepDrift;

        /// <summary>
        ///     Signed push per candle from the current news, when it starts, and candles left.
        /// </summary>
        public float Push;
        public TimeSpan PushFrom;
        public int PushCandles;

        /// <summary>
        ///     Candles closed since the round started, so news can be pinned to a candle.
        /// </summary>
        public int Closed;
        public readonly List<int> NewsAt = new();
    }

    private sealed class Position
    {
        public int Held;
        public float CostBasis;
    }

    private sealed record NewsItem(TimeSpan PublishedAt, TimeSpan MovesAt, string Headline, CMUMarketKind Kind);

    /// <summary>
    ///     Who is trading and where their money comes from.
    /// </summary>
    public sealed record Trader(string Key, string Name, int Cap, bool Corporate);

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => Reset());
        SubscribeLocalEvent<CMUMarketsTerminalComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<CMUMarketsTerminalComponent, CMUMarketsTradeBuiMsg>(OnTrade);
        InitializeEvents();
    }

    private void Reset()
    {
        _markets.Clear();
        _news.Clear();
        _portfolios.Clear();
        _firstDeathReported.Clear();
        _massCasualtyReported.Clear();
        _deadUnclaimed.Clear();
        _destroyedUnclaimed.Clear();
        _corporateMarketFlow = 0;
        _started = false;
        _civilianNewsShown = 0;
    }

    private void EnsureMarkets()
    {
        if (_started)
            return;

        _started = true;
        var now = _timing.CurTime;
        foreach (var commodity in _prototypes.EnumeratePrototypes<CMUMarketCommodityPrototype>().OrderBy(c => c.ID))
        {
            var market = new Market { Commodity = commodity, Price = commodity.BasePrice, Fundamental = commodity.BasePrice };
            market.Current = new CMUMarketCandle(market.Price, market.Price, market.Price, market.Price, 0);
            _markets[commodity.ID] = market;
            PlanCandle(market, now);
        }

        _nextCandle = now + CandleLength;
        _nextStep = now + StepLength;
        _nextNews = now + NextNewsDelay();
        _lastCorporateBudget = _corporate.GetCorporateBudget();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Only run the market while someone could trade on it.
        if (!_started && !AnyTerminal())
            return;

        EnsureMarkets();
        var now = _timing.CurTime;
        var changed = false;

        if (now >= _nextNews)
        {
            _nextNews = now + NextNewsDelay();
            StartNews(now);
        }

        while (now >= _nextStep)
        {
            _nextStep += StepLength;
            if (_nextStep > _nextCandle)
                continue;

            foreach (var market in _markets.Values)
                Step(market);

            changed = true;
        }

        if (now >= _nextCandle)
        {
            foreach (var market in _markets.Values)
                CloseCandle(market);

            _nextCandle += CandleLength;
            _nextStep = now + StepLength;
            foreach (var market in _markets.Values)
                PlanCandle(market, now);

            PayDividends();
            UpdateRoundEvents();
            changed = true;
        }

        // Civilians' view of the news changes on its own schedule.
        var civilianNews = _news.Count(n => now >= n.MovesAt - CivilianNewsLead);
        if (civilianNews != _civilianNewsShown)
        {
            _civilianNewsShown = civilianNews;
            changed = true;
        }

        if (changed)
            UpdateAllUi();
    }

    private bool AnyTerminal()
    {
        var terminals = EntityQueryEnumerator<CMUMarketsTerminalComponent>();
        return terminals.MoveNext(out _, out _);
    }

    private TimeSpan NextNewsDelay()
    {
        return TimeSpan.FromSeconds(_random.Next(150, 271));
    }

    private void StartNews(TimeSpan now)
    {
        // Pick the kind first so goods news stays as frequent however many stocks are listed.
        var kind = _random.Prob(0.5f) ? CMUMarketKind.Goods : CMUMarketKind.Stock;
        var candidates = _markets.Values.Where(m => m.Commodity.Kind == kind).ToList();
        if (candidates.Count == 0)
            candidates = _markets.Values.ToList();

        BreakNews(_random.Pick(candidates).Commodity.ID, _random.Prob(0.5f));
    }

    /// <summary>
    ///     Publishes news that <paramref name="commodity"/> is about to move. Returns when the move starts.
    /// </summary>
    internal TimeSpan BreakNews(string commodity, bool up)
    {
        EnsureMarkets();
        var now = _timing.CurTime;
        var market = _markets[commodity];

        // The move starts one full candle after the next one opens.
        var movesAt = _nextCandle + CandleLength;
        market.Push = up ? NewsMove : -NewsMove;
        market.PushFrom = movesAt;
        market.PushCandles = NewsCandles;
        // Pinned to the candle forming now.
        market.NewsAt.Add(market.Closed);
        market.NewsAt.RemoveAll(n => n < market.Closed - CandlesKept);

        var headline = Loc.GetString(up ? "cmu-markets-news-up" : "cmu-markets-news-down",
            ("commodity", Loc.GetString(market.Commodity.Name)),
            ("symbol", market.Commodity.Symbol),
            ("story", Loc.GetString($"cmu-markets-{(market.Commodity.Kind == CMUMarketKind.Stock ? "stock-" : "")}story-{(up ? "up" : "down")}-{_random.Next(1, 5)}")));
        AddNews(now, movesAt, headline, market.Commodity.Kind);
        UpdateAllUi();
        return movesAt;
    }

    private void AddNews(TimeSpan publishedAt, TimeSpan movesAt, string headline, CMUMarketKind kind)
    {
        _news.Insert(0, new NewsItem(publishedAt, movesAt, headline, kind));
        if (_news.Count > NewsKept)
            _news.RemoveAt(_news.Count - 1);
    }

    /// <summary>
    ///     The news a trader can read right now. The corporate side sees it as it breaks;
    ///     civilians only <see cref="CivilianNewsLead"/> before the move.
    /// </summary>
    public List<CMUMarketNews> GetVisibleNews(bool corporate)
    {
        var now = _timing.CurTime;
        return _news
            .Where(n => corporate ? n.Kind == CMUMarketKind.Goods : now >= n.MovesAt - CivilianNewsLead)
            .Select(n => new CMUMarketNews(corporate ? n.PublishedAt : n.MovesAt - CivilianNewsLead, n.Headline))
            .ToList();
    }

    /// <summary>
    ///     Decides where a commodity is heading over the next candle.
    /// </summary>
    private void PlanCandle(Market market, TimeSpan candleStart)
    {
        var change = _random.NextFloat(-market.Commodity.Volatility, market.Commodity.Volatility);
        change += MeanReversion * (market.Fundamental - market.Price) / market.Fundamental;

        if (market.PushCandles > 0 && candleStart >= market.PushFrom)
        {
            change += market.Push;
            market.PushCandles--;
        }

        market.StepDrift = change / (float) (CandleLength / StepLength);
    }

    private void Step(Market market)
    {
        var noise = _random.NextFloat(-market.Commodity.Volatility, market.Commodity.Volatility) * StepNoise;
        SetPrice(market, market.Price * (1 + market.StepDrift + noise));
    }

    private void SetPrice(Market market, float price)
    {
        var basePrice = market.Commodity.BasePrice;
        market.Price = Math.Clamp(price, basePrice * MinPriceFactor, basePrice * MaxPriceFactor);
        var c = market.Current;
        market.Current = c with
        {
            High = MathF.Max(c.High, market.Price),
            Low = MathF.Min(c.Low, market.Price),
            Close = market.Price,
        };
    }

    private void CloseCandle(Market market)
    {
        market.Candles.Add(market.Current);
        market.Closed++;
        if (market.Candles.Count > CandlesKept)
            market.Candles.RemoveAt(0);

        market.Current = new CMUMarketCandle(market.Price, market.Price, market.Price, market.Price, 0);
    }

    private void OnUiOpened(Entity<CMUMarketsTerminalComponent> ent, ref BoundUIOpenedEvent args)
    {
        EnsureMarkets();
        UpdateUi(ent, args.Actor);
    }

    private void OnTrade(Entity<CMUMarketsTerminalComponent> ent, ref CMUMarketsTradeBuiMsg args)
    {
        if (GetTrader(ent, args.Actor) is not { } trader)
        {
            _popup.PopupEntity(Loc.GetString("cmu-markets-refuse-no-account"), ent, args.Actor);
            return;
        }

        TryTrade(args.Commodity, args.Units, args.Buy, trader, ent, args.Actor);
    }

    /// <summary>
    ///     Buys or sells <paramref name="units"/> for <paramref name="trader"/>. Returns the money
    ///     moved (negative when buying), or null if the trade was refused.
    /// </summary>
    public int? TryTrade(string commodity, int units, bool buy, Trader trader, EntityUid terminal, EntityUid user)
    {
        EnsureMarkets();
        if (!_markets.TryGetValue(commodity, out var market) || units <= 0)
            return null;

        // The company doesn't play the stock market from a site terminal.
        if (trader.Corporate && market.Commodity.Kind != CMUMarketKind.Goods)
            return null;

        units = Math.Min(units, MaxTradeUnits);
        var position = GetPosition(trader.Key, commodity);
        if (!buy && position.Held < units)
        {
            _popup.PopupEntity(Loc.GetString("cmu-markets-refuse-held"), terminal, user);
            return null;
        }

        // The trade moves the price and fills halfway along that move.
        var before = market.Price;
        var impact = MathF.Min(MaxImpact, units * before / market.Commodity.Liquidity);
        var after = before * (buy ? 1 + impact : 1 - impact);
        var fill = (before + after) / 2;
        int moved;

        if (buy)
        {
            var cost = (int) MathF.Ceiling(units * fill * (1 + Fee));
            if (HoldingsValue(trader.Key) + units * fill > trader.Cap)
            {
                _popup.PopupEntity(Loc.GetString("cmu-markets-refuse-cap", ("cap", trader.Cap)), terminal, user);
                return null;
            }

            if (!TryDebit(trader, cost))
            {
                _popup.PopupEntity(Loc.GetString("cmu-markets-refuse-funds"), terminal, user);
                return null;
            }

            position.Held += units;
            position.CostBasis += cost;
            moved = -cost;
        }
        else
        {
            moved = (int) MathF.Floor(units * fill * (1 - Fee));
            position.CostBasis -= position.CostBasis * units / position.Held;
            position.Held -= units;
            Credit(trader, moved);
        }

        SetPrice(market, after);
        market.Current = market.Current with { Volume = market.Current.Volume + units };
        UpdateAllUi();
        return moved;
    }

    private Position GetPosition(string account, string commodity)
    {
        if (!_portfolios.TryGetValue(account, out var portfolio))
            _portfolios[account] = portfolio = new Dictionary<string, Position>();

        if (!portfolio.TryGetValue(commodity, out var position))
            portfolio[commodity] = position = new Position();

        return position;
    }

    public int HoldingsValue(string account)
    {
        if (!_portfolios.TryGetValue(account, out var portfolio))
            return 0;

        return (int) portfolio.Sum(p => _markets.TryGetValue(p.Key, out var m) ? p.Value.Held * m.Price : 0);
    }

    /// <summary>
    ///     Pays the dividend on everything every account holds. Returns the total paid.
    /// </summary>
    public int PayDividends()
    {
        var total = 0;
        foreach (var account in _portfolios.Keys.ToList())
        {
            var paid = (int) MathF.Floor(HoldingsValue(account) * Dividend);
            if (paid <= 0)
                continue;

            if (CreditAccount(account, paid))
                total += paid;
        }

        return total;
    }

    /// <summary>
    ///     Moves a listing at once by <paramref name="change"/> (e.g. -0.15), for events elsewhere in
    ///     the round that the market would react to. A lasting shock also moves what the listing is
    ///     worth, so the price doesn't drift back. Does nothing before the market opens.
    /// </summary>
    public void Shock(string commodity, float change, bool lasting = true)
    {
        if (!_started || !_markets.TryGetValue(commodity, out var market))
            return;

        SetPrice(market, market.Price * (1 + change));
        if (lasting)
        {
            var basePrice = market.Commodity.BasePrice;
            market.Fundamental = Math.Clamp(market.Fundamental * (1 + change), basePrice * MinPriceFactor, basePrice * MaxPriceFactor);
        }

        UpdateAllUi();
    }

    /// <summary>
    ///     Current price of a commodity, for tests and admin tools.
    /// </summary>
    public float? GetPrice(string commodity)
    {
        EnsureMarkets();
        return _markets.TryGetValue(commodity, out var market) ? market.Price : null;
    }

    private void UpdateAllUi()
    {
        var query = EntityQueryEnumerator<CMUMarketsTerminalComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            foreach (var actor in _ui.GetActors(uid, CMUMarketsTerminalUi.Key))
                UpdateUi((uid, comp), actor);
        }
    }

    private void UpdateUi(Entity<CMUMarketsTerminalComponent> ent, EntityUid actor)
    {
        var trader = GetTrader(ent, actor);
        var now = _timing.CurTime;
        var corporate = ent.Comp.Corporate;
        Dictionary<string, Position>? portfolio = null;
        if (trader != null)
            _portfolios.TryGetValue(trader.Key, out portfolio);

        var quotes = _markets.Values
            .Where(m => !corporate || m.Commodity.Kind == CMUMarketKind.Goods)
            .Select(m =>
            {
                var held = portfolio?.GetValueOrDefault(m.Commodity.ID);
                var previous = m.Candles.Count > 0 ? m.Candles[^1].Close : m.Current.Open;
                var candles = new List<CMUMarketCandle>(m.Candles) { m.Current };
                var firstClosed = m.Closed - m.Candles.Count;
                var news = m.NewsAt.Select(n => n - firstClosed).Where(n => n >= 0 && n < candles.Count).ToList();
                return new CMUMarketQuote(
                    m.Commodity.ID,
                    m.Commodity.Kind,
                    m.Commodity.Symbol,
                    m.Commodity.Name,
                    m.Price,
                    previous > 0 ? (m.Price - previous) * 100 / previous : 0,
                    held?.Held ?? 0,
                    held is { Held: > 0 } ? (int) MathF.Round(held.CostBasis / held.Held) : 0,
                    candles,
                    corporate ? news : new List<int>());
            })
            .ToList();

        var visibleNews = GetVisibleNews(corporate);

        var state = new CMUMarketsTerminalBuiState(
            quotes,
            visibleNews,
            corporate,
            trader?.Name,
            trader == null ? 0 : GetBalance(trader),
            trader == null ? 0 : HoldingsValue(trader.Key),
            trader?.Cap ?? (corporate ? CorporateHoldingsCap : CivilianHoldingsCap),
            Fee * 100,
            Dividend * 100,
            _nextCandle);

        _ui.ServerSendUiMessage(ent.Owner, CMUMarketsTerminalUi.Key, state, actor);
    }
}
