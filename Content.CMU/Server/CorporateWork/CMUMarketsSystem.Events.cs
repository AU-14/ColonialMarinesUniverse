using System.Linq;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Mind;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared.CMU14.CorporateWork;
using Content.Shared.Ghost.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Random;

namespace Content.Server.CMU14.CorporateWork;

/// <summary>
///     What happens in the round moves the stock of the companies in it. Profit lifts a company,
///     a mass-casualty event or a big setback (a withdrawal, a lost research licence) tanks it, and
///     a rival in the same line of business that isn't in the round moves in to fill the gap.
///     Companies not in the round only follow the market and the newswire.
/// </summary>
public sealed partial class CMUMarketsSystem
{
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private RMCUnrevivableSystem _unrevivable = default!;

    /// <summary>
    ///     Stock change per $100 of profit, up to <see cref="MaxIncomeShock"/> a minute.
    /// </summary>
    public const float IncomeShockPer100 = 0.004f;
    public const float MaxIncomeShock = 0.06f;

    /// <summary>
    ///     Profit in one minute that makes the newswire.
    /// </summary>
    public const int EarningsNews = 1000;

    /// <summary>
    ///     The first casualties among a company's people make the newswire and knock this off its stock.
    /// </summary>
    public const float FirstCasualtyShock = -0.10f;

    /// <summary>
    ///     A mass-casualty event: more than this share of a company's people dead for good
    ///     (unrevivable, ghosted out, or no body left).
    /// </summary>
    public const float MassCasualtyPermanent = 0.3f;

    /// <summary>
    ///     Too few people for a share to mean anything.
    /// </summary>
    public const int MassCasualtyMinStaff = 3;

    public const float MassCasualtyShock = -0.25f;

    private readonly HashSet<string> _firstDeathReported = new();
    private readonly HashSet<string> _massCasualtyReported = new();

    /// <summary>
    ///     Unclaimed third-party bodies that died, and the company they worked for. Without a mind
    ///     nothing else remembers them once they're gibbed.
    /// </summary>
    private readonly Dictionary<EntityUid, string> _deadUnclaimed = new();

    /// <summary>
    ///     Per round force, unclaimed bodies that died and were then destroyed.
    /// </summary>
    private readonly Dictionary<string, int> _destroyedUnclaimed = new();
    private float _lastCorporateBudget;

    /// <summary>
    ///     Corporate money that went into or came out of the markets since the last candle, so
    ///     trading doesn't count as profit.
    /// </summary>
    private float _corporateMarketFlow;

    /// <summary>
    ///     How much a rival gains moving in after a company's setback.
    /// </summary>
    public const float GapFill = 0.12f;

    private void InitializeEvents()
    {
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<MobStateComponent, EntityTerminatingEvent>(OnMobTerminating);
    }

    /// <summary>
    ///     A company's first death makes the newswire and drops its stock. After that, deaths only
    ///     move the stock once enough are permanent to make a mass-casualty event, checked every candle.
    /// </summary>
    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (!_started)
            return;

        // Revived: no longer a casualty if it's destroyed later.
        if (args.OldMobState == MobState.Dead && args.NewMobState != MobState.Dead)
            _deadUnclaimed.Remove(args.Target);

        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead)
            return;

        if (!HasMind(args.Target) && TryComp<GhostRoleComponent>(args.Target, out var role) && UnclaimedForce(role) is { } unclaimedForce)
            _deadUnclaimed[args.Target] = unclaimedForce;

        if (GetForce(args.Target) is not { } force || StockFor(force) is not { } stock || !_firstDeathReported.Add(stock.Commodity.ID))
            return;

        var now = _timing.CurTime;
        AddNews(now, now, Loc.GetString("cmu-markets-news-casualty",
            ("symbol", stock.Commodity.Symbol),
            ("company", Loc.GetString(stock.Commodity.Name))), CMUMarketKind.Stock);
        Shock(stock.Commodity.ID, FirstCasualtyShock);
    }

    /// <summary>
    ///     A dead unclaimed body that's destroyed (gibbed, blown up) is still a casualty, and now a
    ///     permanent one. Bodies deleted alive, like unclaimed deployments that expire, aren't.
    /// </summary>
    private void OnMobTerminating(Entity<MobStateComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!_started || !_deadUnclaimed.Remove(ent, out var force))
            return;

        _destroyedUnclaimed[force] = _destroyedUnclaimed.GetValueOrDefault(force) + 1;
    }

    /// <summary>
    ///     Called at every candle close.
    /// </summary>
    internal void UpdateRoundEvents()
    {
        CheckCorporateProfit();
        CheckMassCasualties();
    }

    /// <summary>
    ///     Corporate money that came in but isn't earnings (debts called in on a withdrawal), so
    ///     the stock doesn't read it as profit.
    /// </summary>
    public void IgnoreCorporateIncome(float amount)
    {
        _corporateMarketFlow += amount;
    }

    private void CheckCorporateProfit()
    {
        var budget = _corporate.GetCorporateBudget();
        var profit = budget - _lastCorporateBudget - _corporateMarketFlow;
        _lastCorporateBudget = budget;
        _corporateMarketFlow = 0;

        // Spending isn't a loss to the market, only money coming in counts.
        if (profit <= 0)
            return;

        ReportIncome(CorporateForce, profit);
        if (profit < EarningsNews || StockFor(CorporateForce) is not { } stock)
            return;

        var now = _timing.CurTime;
        AddNews(now, now, Loc.GetString("cmu-markets-news-earnings",
            ("symbol", stock.Commodity.Symbol),
            ("company", Loc.GetString(stock.Commodity.Name))), CMUMarketKind.Stock);
    }

    private void CheckMassCasualties()
    {
        foreach (var market in _markets.Values)
        {
            if (market.Commodity.RoundForces.Count == 0 || _massCasualtyReported.Contains(market.Commodity.ID))
                continue;

            var (staff, _, permanent) = CountCasualties(market);
            if (staff < MassCasualtyMinStaff || permanent <= staff * MassCasualtyPermanent)
                continue;

            _massCasualtyReported.Add(market.Commodity.ID);
            ReportSetback(market.Commodity.RoundForces[0], MassCasualtyShock,
                market.Commodity.Sectors.FirstOrDefault() ?? string.Empty, "cmu-markets-setback-casualties");
        }
    }

    /// <summary>
    ///     Everyone who has worked for this company this round, how many are dead, and how many of
    ///     those can't be brought back (unrevivable, or no body left).
    /// </summary>
    public (int Staff, int Dead, int Permanent) CountCasualties(string commodity)
    {
        return _markets.TryGetValue(commodity, out var market) ? CountCasualties(market) : (0, 0, 0);
    }

    private (int Staff, int Dead, int Permanent) CountCasualties(Market company)
    {
        int staff = 0, dead = 0, permanent = 0;
        var minds = EntityQueryEnumerator<MindComponent>();
        while (minds.MoveNext(out var mindId, out var mind))
        {
            if (!_jobs.MindTryGetJob(mindId, out var job) || job.RoundForce is not { } force || !company.Commodity.RoundForces.Contains(force))
                continue;

            staff++;
            // A mind that's moved to its ghost has left its body for good.
            if (mind.OwnedEntity is not { } body || TerminatingOrDeleted(body) || HasComp<GhostComponent>(body))
            {
                dead++;
                permanent++;
            }
            else if (_mobState.IsDead(body))
            {
                dead++;
                if (_unrevivable.IsUnrevivable(body))
                    permanent++;
            }
        }

        foreach (var force in company.Commodity.RoundForces)
        {
            var destroyed = _destroyedUnclaimed.GetValueOrDefault(force);
            staff += destroyed;
            dead += destroyed;
            permanent += destroyed;
        }

        // Third-party bodies nobody has taken yet have no mind, only the job on their ghost role.
        var unclaimed = EntityQueryEnumerator<GhostRoleComponent>();
        while (unclaimed.MoveNext(out var body, out var role))
        {
            if (HasMind(body) || UnclaimedForce(role) is not { } force || !company.Commodity.RoundForces.Contains(force))
                continue;

            staff++;
            if (!_mobState.IsDead(body))
                continue;

            dead++;
            if (_unrevivable.IsUnrevivable(body))
                permanent++;
        }

        return (staff, dead, permanent);
    }

    /// <summary>
    ///     A company in the round earned <paramref name="amount"/>. Its stock rises, and rivals in
    ///     its sectors dip a little.
    /// </summary>
    public void ReportIncome(string force, float amount)
    {
        if (!_started || amount <= 0 || StockFor(force) is not { } stock)
            return;

        var change = MathF.Min(MaxIncomeShock, amount / 100 * IncomeShockPer100);
        Shock(stock.Commodity.ID, change);
        foreach (var rival in Rivals(stock))
            Shock(rival.Commodity.ID, -change / 4);
    }

    /// <summary>
    ///     A company in the round took a hit in <paramref name="sector"/>. Its stock drops by
    ///     <paramref name="change"/> and a rival in that sector not in the round fills the gap.
    ///     Both make the newswire.
    /// </summary>
    public void ReportSetback(string force, float change, string sector, LocId reason)
    {
        EnsureMarkets();
        if (StockFor(force) is not { } stock)
            return;

        Shock(stock.Commodity.ID, change);
        var now = _timing.CurTime;
        AddNews(now, now, Loc.GetString("cmu-markets-news-setback",
            ("symbol", stock.Commodity.Symbol),
            ("company", Loc.GetString(stock.Commodity.Name)),
            ("reason", Loc.GetString(reason))), CMUMarketKind.Stock);

        var rivals = Rivals(stock, sector).Where(r => !IsInRound(r)).ToList();
        if (rivals.Count == 0)
            return;

        var rival = _random.Pick(rivals);
        Shock(rival.Commodity.ID, GapFill);
        AddNews(now, now, Loc.GetString("cmu-markets-news-gap",
            ("symbol", rival.Commodity.Symbol),
            ("company", Loc.GetString(rival.Commodity.Name)),
            ("rival", Loc.GetString(stock.Commodity.Name))), CMUMarketKind.Stock);
        UpdateAllUi();
    }

    private Market? StockFor(string force)
    {
        EnsureMarkets();
        return _markets.Values.FirstOrDefault(m => m.Commodity.Kind == CMUMarketKind.Stock && m.Commodity.RoundForces.Contains(force));
    }

    private IEnumerable<Market> Rivals(Market company, string? sector = null)
    {
        return _markets.Values.Where(m =>
            m != company &&
            m.Commodity.Kind == CMUMarketKind.Stock &&
            (sector == null ? m.Commodity.Sectors.Intersect(company.Commodity.Sectors).Any() : m.Commodity.Sectors.Contains(sector)));
    }

    private string? GetForce(EntityUid uid)
    {
        if (_mind.TryGetMind(uid, out var mindId, out _))
            return _jobs.MindTryGetJob(mindId, out var job) ? job.RoundForce : null;

        return TryComp<GhostRoleComponent>(uid, out var role) ? UnclaimedForce(role) : null;
    }

    /// <summary>
    ///     Whether a body is spoken for: it has a mind, or it isn't a mob at all (a ghost-role spawner).
    /// </summary>
    private bool HasMind(EntityUid uid)
    {
        if (!HasComp<MobStateComponent>(uid))
            return true;

        return TryComp<MindContainerComponent>(uid, out var container) && container.HasMind;
    }

    /// <summary>
    ///     The company an unclaimed ghost-role body works for, from the job it will be given.
    /// </summary>
    private string? UnclaimedForce(GhostRoleComponent role)
    {
        return role.JobProto is { } jobId && _prototypes.TryIndex<JobPrototype>(jobId, out var job) ? job.RoundForce : null;
    }

    /// <summary>
    ///     Whether anyone alive in the round works for this company.
    /// </summary>
    public bool IsInRound(string commodity)
    {
        return _markets.TryGetValue(commodity, out var market) && IsInRound(market);
    }

    private bool IsInRound(Market company)
    {
        if (company.Commodity.RoundForces.Count == 0)
            return false;

        var minds = EntityQueryEnumerator<MindComponent>();
        while (minds.MoveNext(out var mindId, out var mind))
        {
            if (mind.OwnedEntity is not { } body || _mobState.IsDead(body) || HasComp<GhostComponent>(body))
                continue;

            if (_jobs.MindTryGetJob(mindId, out var job) && job.RoundForce is { } force && company.Commodity.RoundForces.Contains(force))
                return true;
        }

        var unclaimed = EntityQueryEnumerator<GhostRoleComponent>();
        while (unclaimed.MoveNext(out var body, out var role))
        {
            if (!HasMind(body) && !_mobState.IsDead(body) &&
                UnclaimedForce(role) is { } force && company.Commodity.RoundForces.Contains(force))
            {
                return true;
            }
        }

        return false;
    }
}
