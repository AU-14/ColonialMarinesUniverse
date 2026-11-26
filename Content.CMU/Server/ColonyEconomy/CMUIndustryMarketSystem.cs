using System.Linq;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.ColonyEconomy;

// one thing an ASRS shipment exports: what it is (a stack, entity or liquid id) and its full ASRS price
public readonly record struct IndustryExport(string Key, double Value);

// diminishing returns on industry exports, per faction. selling piles up pressure on the product and on the faction's
// total, both bleed off with a half-life, and past a free allowance every credit of pressure lowers the price. the
// payout is the exact integral of the price curve, so splitting a load into many small ones doesn't pay more.
// only industry exports go through here; crate refunds and invoices never do
public sealed partial class CMUIndustryMarketSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IGameTiming _timing = default!;

    private sealed class FactionMarket
    {
        public readonly Dictionary<string, double> Items = new();
        public double Total;
        public TimeSpan Updated;
    }

    private readonly record struct Curve(double ItemFree, double ItemSlope, double TotalFree, double TotalSlope);

    private readonly Dictionary<string, FactionMarket> _markets = new();
    private readonly Dictionary<string, double> _pendingItems = new();

    private bool _enabled;
    private float _halfLife;
    private Curve _civil;
    private Curve _military;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _markets.Clear());

        Subs.CVar(_config, CCVars.CMUIndustryMarketEnabled, v => _enabled = v, true);
        Subs.CVar(_config, CCVars.CMUIndustryMarketHalfLife, v => _halfLife = v, true);
        Subs.CVar(_config, CCVars.CMUIndustryMarketItemFree, v => _civil = _civil with { ItemFree = v }, true);
        Subs.CVar(_config, CCVars.CMUIndustryMarketItemSlope, v => _civil = _civil with { ItemSlope = v }, true);
        Subs.CVar(_config, CCVars.CMUIndustryMarketTotalFree, v => _civil = _civil with { TotalFree = v }, true);
        Subs.CVar(_config, CCVars.CMUIndustryMarketTotalSlope, v => _civil = _civil with { TotalSlope = v }, true);
        Subs.CVar(_config, CCVars.CMUIndustryMarketMilitaryItemFree, v => _military = _military with { ItemFree = v }, true);
        Subs.CVar(_config, CCVars.CMUIndustryMarketMilitaryItemSlope, v => _military = _military with { ItemSlope = v }, true);
        Subs.CVar(_config, CCVars.CMUIndustryMarketMilitaryTotalFree, v => _military = _military with { TotalFree = v }, true);
        Subs.CVar(_config, CCVars.CMUIndustryMarketMilitaryTotalSlope, v => _military = _military with { TotalSlope = v }, true);
    }

    public static bool IsMilitary(string faction)
        => faction.Equals("govfor", StringComparison.OrdinalIgnoreCase)
           || faction.Equals("opfor", StringComparison.OrdinalIgnoreCase);

    // what these exports would pay this faction right now, without selling them
    public double Quote(string faction, IReadOnlyList<IndustryExport> exports) => Settle(faction, exports, false);

    // pays for the exports and counts them against the faction
    public double Sell(string faction, IReadOnlyList<IndustryExport> exports) => Settle(faction, exports, true);

    // fraction of full price the next credit of this product would fetch, 0-1
    public double CurrentMultiplier(string faction, string key)
    {
        if (!_enabled)
            return 1;

        var curve = GetCurve(faction);
        var market = GetMarket(faction);
        return Marginal(curve.ItemFree, curve.ItemSlope, market.Items.GetValueOrDefault(key))
               * Marginal(curve.TotalFree, curve.TotalSlope, market.Total);
    }

    // two steps, each along one line: the product's own curve turns full price into what the product is worth now,
    // then the faction's total curve takes its cut of that. a curve along one line pays the same however the sale is
    // cut up, so splitting a load or mixing products in any order can't pay more than selling it all at once
    private double Settle(string faction, IReadOnlyList<IndustryExport> exports, bool record)
    {
        var paid = 0.0;
        if (!_enabled)
        {
            foreach (var export in exports)
                paid += export.Value;
            return paid;
        }

        var curve = GetCurve(faction);
        var market = GetMarket(faction);
        // a quote has to see the earlier lines of the same load too, or one big load looks as cheap as its first unit
        _pendingItems.Clear();
        var total = market.Total;
        foreach (var export in exports)
        {
            if (!double.IsFinite(export.Value) || export.Value <= 0)
                continue;

            var item = _pendingItems.TryGetValue(export.Key, out var pending)
                ? pending
                : market.Items.GetValueOrDefault(export.Key);
            var worth = Integrate(curve.ItemFree, curve.ItemSlope, item, export.Value);
            paid += Integrate(curve.TotalFree, curve.TotalSlope, total, worth);
            _pendingItems[export.Key] = item + export.Value;
            total += worth;
        }

        if (record)
        {
            foreach (var (key, pressure) in _pendingItems)
                market.Items[key] = pressure;
            market.Total = total;
        }

        return paid;
    }

    private Curve GetCurve(string faction) => IsMilitary(faction) ? _military : _civil;

    private static double Marginal(double free, double slope, double pressure)
        => slope > 0 && pressure > free ? 1 / (1 + (pressure - free) / slope) : 1;

    // integral of Marginal(pressure + x) for x in [0, amount]: flat up to the free allowance, 1/(1 + y/slope) after it
    private static double Integrate(double free, double slope, double pressure, double amount)
    {
        if (amount <= 0)
            return 0;
        if (slope <= 0)
            return amount;

        var flat = Math.Clamp(free - pressure, 0, amount);
        var start = Math.Max(0, pressure - free);
        var over = amount - flat;
        return flat + slope * Math.Log((slope + start + over) / (slope + start));
    }

    // decay is applied when a faction is looked at, nothing ticks
    private FactionMarket GetMarket(string faction)
    {
        var now = _timing.CurTime;
        if (!_markets.TryGetValue(faction, out var market))
        {
            market = new FactionMarket { Updated = now };
            _markets[faction] = market;
            return market;
        }

        var minutes = (now - market.Updated).TotalMinutes;
        market.Updated = now;
        if (minutes <= 0)
            return market;

        var kept = _halfLife > 0 ? Math.Pow(0.5, minutes / _halfLife) : 0;
        market.Total *= kept;
        foreach (var key in market.Items.Keys.ToList())
        {
            var value = market.Items[key] * kept;
            if (value < 0.01)
                market.Items.Remove(key);
            else
                market.Items[key] = value;
        }

        return market;
    }
}
