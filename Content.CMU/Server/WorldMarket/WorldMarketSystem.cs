// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.CMU14.Chat;
using Content.Server.CMU14.ColonyEconomy;
using Content.Server.CMU14.Government;
using Content.Shared.CMU14.ColonyEconomy;
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
    [Dependency] private readonly ColonyGovernmentSystem _government = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    // The import duty that the colony pays on what it buys of the companies, in percent, before the trade hubs of the map and the holidays change it.
    public const int BaseDutyPercent = 15;

    public static readonly TimeSpan TickEvery = TimeSpan.FromSeconds(20);
    public const int HistoryLength = 36;

    private TimeSpan _nextSecond;

    public override void Initialize()
    {
        base.Initialize();

        // The system of the console already listens for its map init, and a component has one listener of an event per system; the table is made when the component starts.
        SubscribeLocalEvent<AdminConsoleComponent, ComponentStartup>(OnConsoleStartup);
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

        market.NextTick = now + TickEvery;
        market.Started = true;
    }

    public float IndexOf(string category)
    {
        return TryMarket(out var market) && market.Comp.Index.TryGetValue(category, out var value) ? value : 100f;
    }

    #endregion

    #region The prices

    // The duty that the colony pays on the goods of a company, in percent.
    public int DutyPercent(string company)
    {
        return TryMarket(out var market) ? DutyPercent(market, company) : BaseDutyPercent;
    }

    private int DutyPercent(Entity<WorldMarketComponent> market, string company)
    {
        var duty = BaseDutyPercent;

        // The trade hubs of the map that the colony has won.
        duty -= CityDutyCut(market);

        // A holiday from the duty that an ambassador has ordered of the mother faction.
        duty -= (int) MathF.Round(TaxBreakPoints(WorldTaxKind.Duty));

        // The reforms and the treaties of the government of the colony.
        duty -= _government.DutyCut();

        return Math.Max(0, duty);
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

}
