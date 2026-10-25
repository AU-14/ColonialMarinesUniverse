// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Server.CMU14.ColonyEconomy;
using Content.Server.CMU14.Round;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.CMU14.Threats;
using Content.Shared.CMU14.Trade;
using Content.Shared.CMU14.WorldMarket;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.WorldMarket;

// The window of the console of the colony administration: it gathers what the console shows (the treasury and the taxes, the indices, the news, the companies, the agreements, the quotas, the
// voyages, the black market, the services) and does what the window asks. The taxes and the calls of the services are done by the system of the console as before; the rest is here.
public sealed class ColonyConsoleSystem : EntitySystem
{
    [Dependency] private readonly AdminConsoleSystem _admin = default!;
    [Dependency] private readonly AuRoundSystem _auRound = default!;
    [Dependency] private readonly ColonyBudgetSystem _budget = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly WorldMarketSystem _market = default!;

    private TimeSpan _nextRefresh;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ColonyConsoleComponent, BoundUIOpenedEvent>(OnOpened);
        SubscribeLocalEvent<ColonyConsoleComponent, SignAgreementMessage>(OnSign);
        SubscribeLocalEvent<ColonyConsoleComponent, BreakAgreementMessage>(OnBreak);
        SubscribeLocalEvent<ColonyConsoleComponent, UrgentVoyageMessage>(OnUrgent);
        SubscribeLocalEvent<ColonyConsoleComponent, BuyWareMessage>(OnBuyWare);
        SubscribeLocalEvent<ColonyConsoleComponent, TakeOrderMessage>(OnTakeOrder);
    }

    private void OnOpened(Entity<ColonyConsoleComponent> console, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey is ColonyConsoleUi && TryComp<AdminConsoleComponent>(console, out var admin))
            Send((console.Owner, admin));
    }

    private void OnSign(Entity<ColonyConsoleComponent> console, ref SignAgreementMessage msg)
    {
        if (_market.TryMarket(out var market))
            _market.SignAgreement(market, msg.Agreement);

        _nextRefresh = TimeSpan.Zero;
    }

    private void OnBreak(Entity<ColonyConsoleComponent> console, ref BreakAgreementMessage msg)
    {
        if (_market.TryMarket(out var market))
            _market.BreakAgreement(market, msg.Agreement);

        _nextRefresh = TimeSpan.Zero;
    }

    private void OnUrgent(Entity<ColonyConsoleComponent> console, ref UrgentVoyageMessage msg)
    {
        if (_market.TryMarket(out var market))
            _market.CallUrgentVoyage(market);

        _nextRefresh = TimeSpan.Zero;
    }

    private void OnBuyWare(Entity<ColonyConsoleComponent> console, ref BuyWareMessage msg)
    {
        if (_market.TryMarket(out var market))
            _market.BuyWare(market, msg.Org, msg.Ware);

        _nextRefresh = TimeSpan.Zero;
    }

    private void OnTakeOrder(Entity<ColonyConsoleComponent> console, ref TakeOrderMessage msg)
    {
        if (_market.TryMarket(out var market))
            _market.TakeOrder(market, msg.Org, msg.Order);

        _nextRefresh = TimeSpan.Zero;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextRefresh)
            return;

        _nextRefresh = now + TimeSpan.FromSeconds(1);

        var consoles = EntityQueryEnumerator<ColonyConsoleComponent, AdminConsoleComponent>();
        while (consoles.MoveNext(out var uid, out _, out var comp))
        {
            if (_ui.IsUiOpen(uid, ColonyConsoleUi.Key))
                Send((uid, comp));
        }
    }

    private void Send(Entity<AdminConsoleComponent> console)
    {
        _ui.SetUiState(console.Owner, ColonyConsoleUi.Key, Build(console.Comp));
    }

    public ColonyConsoleState Build(AdminConsoleComponent console)
    {
        var now = _timing.CurTime;
        var econ = _admin.BuildEconomyStatus();

        if (!_market.TryMarket(out var market))
        {
            return new ColonyConsoleState(
                _budget.GetBudget(), console.SalesTaxPercent, console.IncomeTaxPercent, console.TradeTaxPercent, WorldMarketSystem.BaseDutyPercent, 0, 0,
                econ.ActiveEmbargoes, econ.ActiveTradePacts,
                new List<IndexEntry>(), new List<NewsEntry>(), new List<CompanyEntry>(), new List<AgreementEntry>(), new List<QuotaEntry>(), new List<QuotaRecordEntry>(),
                new VoyageEntry(0, 0, 0, WorldMarketSystem.UrgentCost, 0, false), new List<OrgEntry>(), BuildParties(console));
        }

        var comp = market.Comp;
        var budget = _budget.GetBudget();

        var indices = new List<IndexEntry>();
        foreach (var category in _proto.EnumeratePrototypes<WorldMarketCategoryPrototype>().OrderBy(c => Loc.GetString(c.Name)))
        {
            comp.Index.TryGetValue(category.ID, out var value);
            comp.LastChange.TryGetValue(category.ID, out var change);
            var history = comp.History.TryGetValue(category.ID, out var past) ? new List<float>(past) : new List<float>();
            indices.Add(new IndexEntry(category.ID, Loc.GetString(category.Name), category.Color, value, change, history));
        }

        var news = comp.News.Select(item => new NewsEntry(item.Text, (int) (now - item.At).TotalSeconds)).ToList();

        var companies = new List<CompanyEntry>();
        foreach (var contract in _proto.EnumeratePrototypes<TradeContractPrototype>().OrderBy(c => Loc.GetString(c.Company)).ThenBy(c => Loc.GetString(c.Category)))
        {
            var embargo = comp.Embargo.TryGetValue(contract.Company.Id, out var until) && until > now ? (int) Math.Ceiling((until - now).TotalSeconds) : 0;
            var agreement = comp.Agreements.FirstOrDefault(a => a.Until > now && _proto.TryIndex<TradeAgreementPrototype>(a.Id, out var p) && p.Company.Id == contract.Company.Id);
            var agreementName = agreement != null && _proto.TryIndex<TradeAgreementPrototype>(agreement.Id, out var named) ? Loc.GetString(named.Name) : null;

            companies.Add(new CompanyEntry(
                Loc.GetString(contract.Company),
                Loc.GetString(contract.Category),
                (int) Math.Round(_market.SellFactor(contract) * 100f),
                (int) Math.Round(_market.BuyFactor(contract) * 100f),
                _market.DutyPercent(contract.Company.Id),
                embargo,
                agreementName));
        }

        var agreements = new List<AgreementEntry>();
        foreach (var proto in _proto.EnumeratePrototypes<TradeAgreementPrototype>().OrderBy(p => Loc.GetString(p.Name)))
        {
            var signed = comp.Agreements.FirstOrDefault(a => a.Id == proto.ID && a.Until > now);
            agreements.Add(new AgreementEntry(
                proto.ID,
                Loc.GetString(proto.Name),
                Loc.GetString(proto.Description),
                Loc.GetString(proto.Company),
                proto.Cost,
                proto.DurationSeconds,
                proto.DutyCut,
                proto.SellBonus,
                signed != null,
                signed != null ? (int) Math.Ceiling((signed.Until - now).TotalSeconds) : 0,
                budget >= proto.Cost));
        }

        var quotas = new List<QuotaEntry>();
        foreach (var quota in comp.Quotas)
        {
            if (!_proto.TryIndex<MetropolisQuotaPrototype>(quota.Id, out var proto))
                continue;

            quotas.Add(new QuotaEntry(
                proto.ID,
                Loc.GetString(proto.Text, ("amount", proto.Amount)),
                proto.Amount,
                Math.Min(quota.Progress, proto.Amount),
                Math.Max(0, (int) Math.Ceiling((quota.Deadline - now).TotalSeconds)),
                proto.Reward,
                proto.Fine));
        }

        var log = new List<QuotaRecordEntry>();
        foreach (var record in comp.QuotaLog)
        {
            if (_proto.TryIndex<MetropolisQuotaPrototype>(record.Id, out var proto))
                log.Add(new QuotaRecordEntry(Loc.GetString(proto.Text, ("amount", proto.Amount)), record.Done, (int) (now - record.At).TotalSeconds));
        }

        var nextVoyage = _market.NextVoyageAt() ?? now;
        var left = Math.Max(0, (int) Math.Ceiling((nextVoyage - now).TotalSeconds));
        var cooldown = Math.Max(0, (int) Math.Ceiling((comp.UrgentReadyAt - now).TotalSeconds));
        var delayed = comp.DelayUntil > now ? (int) Math.Ceiling((comp.DelayUntil - now).TotalSeconds) : 0;
        var voyage = new VoyageEntry(
            left,
            (int) WorldMarketSystem.VoyageEvery.TotalSeconds,
            delayed,
            WorldMarketSystem.UrgentCost,
            cooldown,
            cooldown == 0 && budget >= WorldMarketSystem.UrgentCost && left > WorldMarketSystem.UrgentLead.TotalSeconds + 15);

        var orgs = new List<OrgEntry>();
        foreach (var org in _proto.EnumeratePrototypes<UnderworldOrgPrototype>().OrderBy(o => Loc.GetString(o.Name)))
        {
            comp.Underworld.TryGetValue(org.ID, out var state);
            state ??= new UnderworldState();

            var wares = org.Wares.Select(ware => new WareEntry(
                ware.Id,
                Loc.GetString(ware.Name),
                _market.WarePrice(ware),
                ware.Heat,
                comp.Incoming.Count(i => i.Org == org.ID && i.Ware == ware.Id))).ToList();

            var orders = org.Orders.Select(order =>
            {
                var taken = state.Orders.FirstOrDefault(t => t.Id == order.Id);
                return new OrderEntry(
                    order.Id,
                    Loc.GetString(order.Text, ("amount", order.Amount)),
                    order.Amount,
                    order.PricePerUnit,
                    order.Heat,
                    order.Bonus,
                    taken != null,
                    taken?.Progress ?? 0,
                    taken != null ? Math.Max(0, (int) Math.Ceiling((taken.Deadline - now).TotalSeconds)) : order.DurationSeconds);
            }).ToList();

            orgs.Add(new OrgEntry(org.ID, Loc.GetString(org.Name), Loc.GetString(org.Description), org.Color, state.Standing, (int) Math.Round(state.Heat), wares, orders));
        }

        var inspection = comp.InspectionUntil > now ? (int) Math.Ceiling((comp.InspectionUntil - now).TotalSeconds) : 0;

        return new ColonyConsoleState(
            budget, console.SalesTaxPercent, console.IncomeTaxPercent, console.TradeTaxPercent,
            _market.DutyPercent(string.Empty), comp.Standing, inspection,
            econ.ActiveEmbargoes, econ.ActiveTradePacts,
            indices, news, companies, agreements, quotas, log, voyage, orgs, BuildParties(console));
    }

    private List<PartyEntry> BuildParties(AdminConsoleComponent console)
    {
        var parties = new List<PartyEntry>();
        foreach (var (id, cost) in console.CallableParties)
        {
            if (_proto.TryIndex<ThirdPartyPrototype>(id, out var proto) && _auRound.IsThirdPartyAllowedForCurrentContext(proto))
                parties.Add(new PartyEntry(id, proto.DisplayName ?? proto.ID, (int) cost, console.CalledParties.Contains(id)));
        }

        return parties;
    }
}
