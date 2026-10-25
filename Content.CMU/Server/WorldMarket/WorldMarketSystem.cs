// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.CMU14.Chat;
using Content.Server.CMU14.ColonyEconomy;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.CMU14.Trade;
using Content.Shared.CMU14.WorldMarket;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.WorldMarket;

// The world outside the colony as one table: the indices of the prices wander and are shaken by events; the events are told as news; a company can refuse to trade for a while; the
// colony signs agreements that make its prices better; the mother company asks quotas; the voyages come by a schedule; and the organizations outside the law sell and ask. Nothing of it is
// simulated beyond the numbers. The trade platforms ask this system what a good is worth and when the next voyage is; the console of the colony administration shows and changes it.
public sealed partial class WorldMarketSystem : EntitySystem
{
    [Dependency] private readonly ColonyBudgetSystem _budget = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    // The import duty that the colony pays on what it buys of the companies, in percent, before the agreements and the standing change it.
    public const int BaseDutyPercent = 15;

    public static readonly TimeSpan TickEvery = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan VoyageEvery = TimeSpan.FromMinutes(6);
    public static readonly TimeSpan FirstVoyage = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan UrgentLead = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan UrgentCooldown = TimeSpan.FromMinutes(4);
    public const int UrgentCost = 900;
    public const int HistoryLength = 36;
    public const int NewsLength = 14;

    private readonly Dictionary<string, string> _categoryOfContract = new();
    private TimeSpan _nextSecond;

    public override void Initialize()
    {
        base.Initialize();

        // The system of the console already listens for its map init, and a component has one listener of an event per system; the table is made when the component starts.
        SubscribeLocalEvent<AdminConsoleComponent, ComponentStartup>(OnConsoleStartup);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(_ => _categoryOfContract.Clear());
    }

    private void OnConsoleStartup(Entity<AdminConsoleComponent> console, ref ComponentStartup args)
    {
        EnsureMarket();
    }

    #region The table

    public bool TryMarket(out Entity<WorldMarketComponent> market)
    {
        var query = EntityQueryEnumerator<WorldMarketComponent>();
        if (query.MoveNext(out var uid, out var comp))
        {
            market = (uid, comp);
            return true;
        }

        market = default;
        return false;
    }

    // The table of the round: made by the first console that is built, and gone with the round.
    public Entity<WorldMarketComponent> EnsureMarket()
    {
        if (TryMarket(out var known))
            return known;

        var uid = Spawn(null, MapCoordinates.Nullspace);
        var comp = AddComp<WorldMarketComponent>(uid);
        Start(comp);
        return (uid, comp);
    }

    private void Start(WorldMarketComponent market)
    {
        var now = _timing.CurTime;
        market.MapSeed = _random.Next(1, int.MaxValue);

        foreach (var category in _proto.EnumeratePrototypes<WorldMarketCategoryPrototype>())
        {
            market.Index[category.ID] = category.Start;
            market.History[category.ID] = new List<float> { category.Start };
            market.LastChange[category.ID] = 0f;
        }

        foreach (var org in _proto.EnumeratePrototypes<UnderworldOrgPrototype>())
            market.Underworld[org.ID] = new UnderworldState();

        market.NextVoyage = now + FirstVoyage;
        market.NextEvent = now + TimeSpan.FromSeconds(_random.Next(120, 180));
        market.NextQuota = now + TimeSpan.FromSeconds(45);
        market.NextTick = now + TickEvery;
        market.UrgentReadyAt = now;
        market.Started = true;
    }

    private string? CategoryOfContract(string contract)
    {
        if (_categoryOfContract.Count == 0)
        {
            foreach (var category in _proto.EnumeratePrototypes<WorldMarketCategoryPrototype>())
            {
                foreach (var id in category.Contracts)
                    _categoryOfContract[id] = category.ID;
            }

            // A marker so that the map is not filled again for nothing when no contract is listed.
            _categoryOfContract.TryAdd(string.Empty, string.Empty);
        }

        return _categoryOfContract.GetValueOrDefault(contract);
    }

    public float IndexOf(string category)
    {
        return TryMarket(out var market) && market.Comp.Index.TryGetValue(category, out var value) ? value : 100f;
    }

    #endregion

    #region The prices

    // What a company pays for what the colony sells to it, as a share of its usual price: the index of its goods, and the agreement. An embargoed company pays nothing.
    public float SellFactor(TradeContractPrototype contract)
    {
        if (!TryMarket(out var market))
            return 1f;

        if (Embargoed(market, contract.Company.Id))
            return 0f;

        var factor = IndexFor(market, contract.ID) / 100f;
        if (AgreementOf(market, contract.Company.Id) is { } agreement)
            factor *= 1f + agreement.SellBonus / 100f;

        return factor;
    }

    // What a company asks of the colony for what it sells, as a share of its usual price: the index of its goods and the import duty on top.
    public float BuyFactor(TradeContractPrototype contract)
    {
        if (!TryMarket(out var market))
            return 1f;

        return IndexFor(market, contract.ID) / 100f * (1f + DutyPercent(market, contract.Company.Id) / 100f);
    }

    public bool Embargoed(TradeContractPrototype contract)
    {
        return TryMarket(out var market) && Embargoed(market, contract.Company.Id);
    }

    private bool Embargoed(Entity<WorldMarketComponent> market, string company)
    {
        return market.Comp.Embargo.TryGetValue(company, out var until) && until > _timing.CurTime;
    }

    private float IndexFor(Entity<WorldMarketComponent> market, string contract)
    {
        return CategoryOfContract(contract) is { } category && market.Comp.Index.TryGetValue(category, out var value) ? value : 100f;
    }

    // The duty that the colony pays on the goods of a company, in percent.
    public int DutyPercent(string company)
    {
        return TryMarket(out var market) ? DutyPercent(market, company) : BaseDutyPercent;
    }

    private int DutyPercent(Entity<WorldMarketComponent> market, string company)
    {
        var duty = BaseDutyPercent;

        if (market.Comp.Standing >= 3)
            duty -= 5;
        else if (market.Comp.Standing <= -2)
            duty += 5;

        if (market.Comp.InspectionUntil > _timing.CurTime)
            duty += 10;

        if (AgreementOf(market, company) is { } agreement)
            duty -= agreement.DutyCut;

        // The trade hubs of the map that the colony has won.
        duty -= CityDutyCut(market);

        // A holiday from the duty that an ambassador has ordered of the mother faction.
        duty -= (int) MathF.Round(TaxBreakPoints(WorldTaxKind.Duty));

        return Math.Max(0, duty);
    }

    // The agreement that is signed with a company and has not run out, or null.
    private TradeAgreementPrototype? AgreementOf(Entity<WorldMarketComponent> market, string company)
    {
        var now = _timing.CurTime;
        foreach (var signed in market.Comp.Agreements)
        {
            if (signed.Until > now && _proto.TryIndex<TradeAgreementPrototype>(signed.Id, out var proto) && proto.Company.Id == company)
                return proto;
        }

        return null;
    }

    #endregion

    #region The news

    public void AddNews(Entity<WorldMarketComponent> market, string text)
    {
        market.Comp.News.Insert(0, new NewsItem(text, _timing.CurTime));
        if (market.Comp.News.Count > NewsLength)
            market.Comp.News.RemoveRange(NewsLength, market.Comp.News.Count - NewsLength);
    }

    // The news told to the colony as a message of the radio: it has no sound, so that it is read and not heard.
    public void Announce(string text)
    {
        _chat.DispatchFilteredAnnouncement(
            ColonyAnnouncements.Recipients(EntityManager),
            text,
            sender: Loc.GetString("rumc-world-news-sender"),
            playSound: false);
    }

    private void Tell(Entity<WorldMarketComponent> market, string text, bool announce = true)
    {
        AddNews(market, text);
        if (announce)
            Announce(text);
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextSecond || !TryMarket(out var market))
            return;

        _nextSecond = now + TimeSpan.FromSeconds(1);

        var comp = market.Comp;
        if (now >= comp.NextTick)
        {
            comp.NextTick = now + TickEvery;
            Tick(market);
        }

        if (now >= comp.NextEvent)
        {
            comp.NextEvent = now + TimeSpan.FromSeconds(_random.Next(210, 420));
            PickEvent(market);
        }

        comp.Active.RemoveAll(active => active.Until <= now);
        foreach (var company in comp.Embargo.Where(pair => pair.Value <= now).Select(pair => pair.Key).ToList())
        {
            comp.Embargo.Remove(company);
            Tell(market, Loc.GetString("rumc-world-embargo-ended", ("company", Loc.GetString(company))));
        }

        comp.Agreements.RemoveAll(signed =>
        {
            if (signed.Until > now)
                return false;

            if (_proto.TryIndex<TradeAgreementPrototype>(signed.Id, out var proto))
                Tell(market, Loc.GetString("rumc-world-agreement-ended", ("name", Loc.GetString(proto.Name))));

            return true;
        });

        UpdateQuotas(market);
        UpdateVoyage(market);
        UpdateUnderworld(market);
        UpdateCities(market, now);
        UpdateMother(market, now);
        UpdateWorld(market, now);
    }

    // Every tick the indices wander: a step of chance and a pull back to the usual price.
    private void Tick(Entity<WorldMarketComponent> market)
    {
        foreach (var category in _proto.EnumeratePrototypes<WorldMarketCategoryPrototype>())
        {
            var old = market.Comp.Index.GetValueOrDefault(category.ID, category.Start);
            var noise = (_random.NextFloat() * 2f - 1f) * category.Volatility * old / 100f;
            var next = Math.Clamp(old + (category.Start - old) * category.Pull + noise, category.Min, category.Max);

            SetIndex(market, category, next, old);
        }
    }

    private void SetIndex(Entity<WorldMarketComponent> market, WorldMarketCategoryPrototype category, float value, float old)
    {
        market.Comp.Index[category.ID] = value;
        market.Comp.LastChange[category.ID] = value - old;

        if (!market.Comp.History.TryGetValue(category.ID, out var history))
            market.Comp.History[category.ID] = history = new List<float>();

        history.Add(value);
        if (history.Count > HistoryLength)
            history.RemoveAt(0);
    }

    #region The events

    private void PickEvent(Entity<WorldMarketComponent> market)
    {
        var events = _proto.EnumeratePrototypes<WorldMarketEventPrototype>().ToList();
        if (events.Count == 0)
            return;

        var total = events.Sum(e => e.Weight);
        var roll = _random.NextFloat() * total;
        foreach (var proto in events)
        {
            roll -= proto.Weight;
            if (roll <= 0f)
            {
                Trigger(market, proto);
                return;
            }
        }

        Trigger(market, events[^1]);
    }

    // Lets an event happen at once. The tests and the console of an admin of the server can call it.
    public void Trigger(Entity<WorldMarketComponent> market, WorldMarketEventPrototype proto)
    {
        var now = _timing.CurTime;

        foreach (var (id, percent) in proto.Shifts)
        {
            if (!_proto.TryIndex<WorldMarketCategoryPrototype>(id, out var category) || !market.Comp.Index.TryGetValue(id, out var old))
                continue;

            SetIndex(market, category, Math.Clamp(old * (1f + percent / 100f), category.Min, category.Max), old);
        }

        var shielded = new List<string>();
        foreach (var company in proto.Embargo)
        {
            // A company that the colony has an agreement with does not close to it.
            if (AgreementOf(market, company) != null)
            {
                shielded.Add(company);
                continue;
            }

            var until = now + TimeSpan.FromSeconds(proto.DurationSeconds);
            if (!market.Comp.Embargo.TryGetValue(company, out var known) || known < until)
                market.Comp.Embargo[company] = until;
        }

        if (proto.VoyageDelaySeconds > 0)
        {
            var start = market.Comp.NextVoyage > now ? market.Comp.NextVoyage : now;
            var delayed = start + TimeSpan.FromSeconds(proto.VoyageDelaySeconds);
            if (delayed > market.Comp.DelayUntil)
                market.Comp.DelayUntil = delayed;
        }

        market.Comp.Active.Add(new ActiveEvent(proto.ID, now + TimeSpan.FromSeconds(proto.DurationSeconds)));

        var text = Loc.GetString(proto.Headline);
        Tell(market, text);

        foreach (var company in shielded)
            Tell(market, Loc.GetString("rumc-world-embargo-shielded", ("company", Loc.GetString(company))), announce: false);
    }

    #endregion
}
