// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Shared.CMU14.WorldMarket;

namespace Content.Server.CMU14.WorldMarket;

// Deals between the factions, and the reports of the cities of intelligence. A deal is money for points, or points for money, offered by one faction to another: what the offerer gives is paid
// at once, and it is kept until the other agrees (then each gets what the other gave) or says no, or the time is out (then it comes back). A city that has intelligence sells a report of the
// other factions, and the more favour the faction has in the cities of that kind, the more the report says; without one most of it is not seen.
public sealed partial class WorldMarketSystem
{
    private static readonly TimeSpan DealOfferTime = TimeSpan.FromMinutes(3);

    private const int DealMostMoney = 50000;
    private const int DealMostPoints = 50;

    // What a report costs, the favour that a faction must have in the city that sells it, how long a report is good, and how long after its last sign a faction is "on the line".
    public const int IntelCost = 500;
    private const int IntelLeastInfluence = 10;
    private const int IntelSeconds = 300;
    private const int OnlineSeconds = 90;

    #region Deals

    // "M6000" is six thousand in money, "P2" is two points.
    private static bool ParseSide(string text, out char kind, out int amount)
    {
        kind = '\0';
        amount = 0;
        if (text.Length < 2 || (text[0] != 'M' && text[0] != 'P') || !int.TryParse(text.AsSpan(1), out amount) || amount <= 0)
            return false;

        kind = text[0];
        return amount <= (kind == 'M' ? DealMostMoney : DealMostPoints);
    }

    private string SideText(char kind, int amount)
    {
        return kind == 'M' ? $"${amount:N0}" : Loc.GetString("rumc-mother-cost", ("points", amount));
    }

    private void Receive(Entity<WorldMarketComponent> market, string faction, char kind, int amount)
    {
        if (kind == 'M')
            PayFaction(market, faction, amount);
        else
            AddPoints(market, faction, amount);
    }

    // The faction offers a deal to another: what it gives is paid at once (the money comes from the budget, as the cost that is given back; the points are taken here).
    private int? DealPropose(Entity<WorldMarketComponent> market, string faction, string partner, string give, string want, float budget)
    {
        var comp = market.Comp;
        var now = _timing.CurTime;

        if (partner == faction || !comp.KnownFactions.Contains(partner) || !ParseSide(give, out var giveKind, out var giveAmount) || !ParseSide(want, out var wantKind, out var wantAmount)
            || giveKind == wantKind
            || comp.Deals.Any(d => d.From == faction && d.To == partner && d.GiveKind == giveKind && d.GiveAmount == giveAmount && d.WantAmount == wantAmount && d.Until > now))
        {
            return null;
        }

        var cost = 0;
        if (giveKind == 'M')
        {
            if (budget < giveAmount)
                return null;

            cost = giveAmount;
        }
        else if (!TrySpendPoints(faction, giveAmount))
        {
            return null;
        }

        comp.Deals.Add(new Deal
        {
            Id = "d" + ++comp.NextDealId,
            From = faction,
            To = partner,
            GiveKind = giveKind,
            GiveAmount = giveAmount,
            WantKind = wantKind,
            WantAmount = wantAmount,
            Until = now + DealOfferTime,
        });


        return cost;
    }

    // The other faction answers. If it agrees it pays what was asked (the money as the cost that is given back, the points are taken here), and each gets what the other gave.
    private int? DealAnswer(Entity<WorldMarketComponent> market, string faction, string dealId, bool accept, float budget)
    {
        var comp = market.Comp;
        var deal = comp.Deals.FirstOrDefault(d => d.Id == dealId && d.To == faction && d.Until > _timing.CurTime);
        if (deal == null)
            return null;

        if (!accept)
        {
            comp.Deals.Remove(deal);
            Receive(market, deal.From, deal.GiveKind, deal.GiveAmount);
            return 0;
        }

        var cost = 0;
        if (deal.WantKind == 'M')
        {
            if (budget < deal.WantAmount)
                return null;

            cost = deal.WantAmount;
        }
        else if (!TrySpendPoints(faction, deal.WantAmount))
        {
            return null;
        }

        comp.Deals.Remove(deal);
        Receive(market, deal.From, deal.WantKind, deal.WantAmount);
        Receive(market, faction, deal.GiveKind, deal.GiveAmount);


        return cost;
    }

    // Deals that were not taken up in time: what the offerer gave comes back.
    private void UpdateDeals(Entity<WorldMarketComponent> market, TimeSpan now)
    {
        var comp = market.Comp;
        for (var i = comp.Deals.Count - 1; i >= 0; i--)
        {
            var deal = comp.Deals[i];
            if (deal.Until > now)
                continue;

            Receive(market, deal.From, deal.GiveKind, deal.GiveAmount);
            comp.Deals.RemoveAt(i);
        }
    }

    private List<DealEntry> BuildDealEntries(Entity<WorldMarketComponent> market, string faction, float budget)
    {
        var now = _timing.CurTime;
        var entries = new List<DealEntry>();

        foreach (var deal in market.Comp.Deals.Where(d => d.Until > now && (d.To == faction || d.From == faction)))
        {
            var incoming = deal.To == faction;
            var blocked = string.Empty;
            if (incoming)
            {
                if (deal.WantKind == 'M' && budget < deal.WantAmount)
                    blocked = Loc.GetString("rumc-map-need-budget");
                else if (deal.WantKind == 'P' && PointsOf(faction) < deal.WantAmount)
                    blocked = Loc.GetString("rumc-mother-need-points", ("value", deal.WantAmount));
            }

            entries.Add(new DealEntry(deal.Id, deal.From, deal.To, SideText(deal.GiveKind, deal.GiveAmount), SideText(deal.WantKind, deal.WantAmount), incoming, blocked));
        }

        return entries;
    }

    #endregion

    #region Intelligence

    // The faction was seen on the line (its console was open).
    public void MarkOnline(string faction)
    {
        if (TryMarket(out var market))
            market.Comp.LastSeen[faction] = _timing.CurTime;
    }

    // How much the faction is told: by its favour in all the cities of intelligence together, one to three.
    private int IntelTier(Entity<WorldMarketComponent> market, string faction)
    {
        var sum = ActiveCities(market).Where(c => c.Capabilities.Contains(CityCapability.Intelligence)).Sum(c => InfluenceOf(market, faction, c.ID));
        return sum < 25 ? 1 : sum < 60 ? 2 : 3;
    }

    private string? IntelBlocked(Entity<WorldMarketComponent> market, string faction, WorldCityPrototype city, float budget)
    {
        if (!city.Capabilities.Contains(CityCapability.Intelligence))
            return Loc.GetString("rumc-map-intel-no");

        if (InfluenceOf(market, faction, city.ID) < IntelLeastInfluence)
            return Loc.GetString("rumc-map-need-influence", ("value", IntelLeastInfluence));

        if (budget < IntelCost)
            return Loc.GetString("rumc-map-need-budget");

        return null;
    }

    // A report is bought in a city of intelligence: it is good for a time, and says as much as the favour of the faction in such cities allows.
    private int? Intel(Entity<WorldMarketComponent> market, string faction, string cityId, float budget)
    {
        if (!_proto.TryIndex<WorldCityPrototype>(cityId, out var city) || ActiveCities(market).All(c => c.ID != cityId) || IntelBlocked(market, faction, city, budget) != null)
            return null;

        market.Comp.IntelUntil[faction] = _timing.CurTime + TimeSpan.FromSeconds(IntelSeconds);
        market.Comp.IntelLevel[faction] = IntelTier(market, faction);
        return IntelCost;
    }

    public int IntelSecondsLeft(string faction)
    {
        return TryMarket(out var market) && market.Comp.IntelUntil.TryGetValue(faction, out var until)
            ? Math.Max(0, (int) Math.Ceiling((until - _timing.CurTime).TotalSeconds))
            : 0;
    }

    // What the report says of each other faction: whether it is on the line, how much the mother faction trusts it (always); its points and the cities that it holds (from the second tier); where
    // its favour is and what it has going (the third).
    private List<DossierEntry> BuildDossiers(Entity<WorldMarketComponent> market, string faction)
    {
        var entries = new List<DossierEntry>();
        var now = _timing.CurTime;
        var comp = market.Comp;

        if (!comp.IntelUntil.TryGetValue(faction, out var until) || until <= now)
            return entries;

        var tier = comp.IntelLevel.GetValueOrDefault(faction, 1);

        foreach (var other in comp.KnownFactions.Where(f => f != faction).OrderBy(f => f, StringComparer.Ordinal))
        {
            var online = comp.LastSeen.TryGetValue(other, out var seen) && (now - seen).TotalSeconds <= OnlineSeconds;
            var points = tier >= 2 ? PointsOf(other) : -1;
            var held = tier >= 2 ? ControlledCities(market, other, 25) : -1;

            var influence = string.Empty;
            var projects = string.Empty;
            if (tier >= 3)
            {
                influence = string.Join(", ", ActiveCities(market)
                    .Select(c => (City: c, Influence: InfluenceOf(market, other, c.ID)))
                    .Where(x => x.Influence > 0)
                    .OrderByDescending(x => x.Influence)
                    .Take(6)
                    .Select(x => $"{Loc.GetString(x.City.Name)} {x.Influence}"));

                projects = string.Join(", ", comp.CityProjects
                    .Where(p => p.Faction == other && p.ExpiresAt > now && _proto.HasIndex<WorldCityActionPrototype>(p.Action))
                    .GroupBy(p => p.Action)
                    .Select(g => g.Count() > 1 ? $"{Loc.GetString(_proto.Index<WorldCityActionPrototype>(g.Key).Name)} ×{g.Count()}" : Loc.GetString(_proto.Index<WorldCityActionPrototype>(g.Key).Name)));
            }

            entries.Add(new DossierEntry(other, online, TrustOf(other), points, held, influence, projects, InPact(market, faction, other)));
        }

        return entries;
    }

    #endregion

    // Once a second.
    private void UpdateTrade(Entity<WorldMarketComponent> market, TimeSpan now)
    {
        UpdateDeals(market, now);
        UpdateCaravans(market, now);
    }
}
