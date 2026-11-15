// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Shared.CMU14.WorldMarket;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.WorldMarket;

// The second scale of an ambassador: the points of influence with the mother faction. They are not the favour of one city but what the faction has earned with its mother in all of them: each
// step of favour in a city (a quarter of the way to the most, then a half, three quarters, all of it) is paid once, and the points are spent on orders of the mother faction (see
// WorldMotherOfferPrototype) or changed for money at a set rate (a subsidy).
public sealed partial class WorldMarketSystem
{
    // How much money one point of influence is changed for.
    public const int MoneyPerPoint = 1500;

    // The steps of favour in a city, and the points that each is paid with.
    private static readonly (int Step, int Points)[] MotherSteps = { (25, 1), (50, 1), (75, 2), (100, 2) };

    public int PointsOf(string faction)
    {
        return TryMarket(out var market) ? market.Comp.MotherPoints.GetValueOrDefault(faction) : 0;
    }

    private void AddPoints(Entity<WorldMarketComponent> market, string faction, int points)
    {
        market.Comp.MotherPoints[faction] = market.Comp.MotherPoints.GetValueOrDefault(faction) + points;
    }

    // The favour of a faction in a city has risen from before to after: each step that it has passed for the first time pays its points.
    private void PayMotherSteps(Entity<WorldMarketComponent> market, string faction, string cityId, int before, int after)
    {
        foreach (var (step, points) in MotherSteps)
        {
            if (before >= step || after < step || !market.Comp.MotherSteps.Add($"{faction}|{cityId}|{step}"))
                continue;

            AddPoints(market, faction, points);
        }
    }

    // The points are taken for an order, or not if there are not enough.
    public bool TrySpendPoints(string faction, int points)
    {
        if (!TryMarket(out var market) || market.Comp.MotherPoints.GetValueOrDefault(faction) < points)
            return false;

        market.Comp.MotherPoints[faction] -= points;
        return true;
    }

    // Points changed for money: how much money, or nothing if there are not as many points.
    public int? Subsidy(string faction, int points)
    {
        if (points <= 0 || !TrySpendPoints(faction, points))
            return null;


        return points * MoneyPerPoint;
    }

    // Why the offer cannot be ordered now (in words), or nothing if it can.
    public string? OfferBlocked(string faction, WorldMotherOfferPrototype offer)
    {
        // A company of another faction is closed, unless that faction has a pact with this one.
        if (PmcAccess(faction, offer) == PmcAccessKind.Closed)
            return Loc.GetString("rumc-mother-pmc-home-only", ("home", Loc.GetString(offer.Faction)));

        var cost = OfferCost(faction, offer);
        if (PointsOf(faction) < cost)
            return Loc.GetString("rumc-mother-need-points", ("value", cost));

        // One holiday of a tax at a time.
        if (offer.Group == MotherOfferGroup.Tax && TryMarket(out var market))
        {
            var now = _timing.CurTime;
            if (market.Comp.TaxBreaks.Any(b => b.Kind == offer.Tax && b.Until > now))
                return Loc.GetString("rumc-mother-tax-active");
        }

        return null;
    }

    public void ApplyTaxBreak(string faction, WorldMotherOfferPrototype offer)
    {
        if (!TryMarket(out var market))
            return;

        market.Comp.TaxBreaks.Add(new TaxBreak
        {
            Kind = offer.Tax,
            Points = offer.TaxPoints,
            Until = _timing.CurTime + TimeSpan.FromSeconds(offer.DurationSeconds),
            Faction = faction,
        });

    }

    // How many points of percent a tax is lower by now, because of the holidays that are on.
    public float TaxBreakPoints(WorldTaxKind kind)
    {
        if (!TryMarket(out var market))
            return 0f;

        var now = _timing.CurTime;
        var points = 0f;
        foreach (var taxBreak in market.Comp.TaxBreaks)
        {
            if (taxBreak.Kind == kind && taxBreak.Until > now)
                points += taxBreak.Points;
        }

        return points;
    }

    // What the faction can order, with the reasons that it cannot. A transport has places that it can be sent to: the points that it can be put down at (given by the console), or the cities
    // that are in trouble (for an evacuation).
    public List<MotherOfferEntry> BuildOffers(string faction, List<TargetEntry> dropPoints)
    {
        var offers = new List<MotherOfferEntry>();
        TryMarket(out var market);

        // The companies are in the order that the faction cares for: its own, those of its allies, the free ones, the closed ones; each home faction is together.
        foreach (var offer in _proto.EnumeratePrototypes<WorldMotherOfferPrototype>()
                     .OrderBy(o => o.Group)
                     .ThenBy(o => o.Group is MotherOfferGroup.Pmc or MotherOfferGroup.Support ? (int) PmcAccess(faction, o) : 0)
                     .ThenBy(o => o.Group is MotherOfferGroup.Pmc or MotherOfferGroup.Support ? o.Faction : string.Empty, StringComparer.Ordinal)
                     .ThenBy(o => o.Order)
                     .ThenBy(o => o.Cost))
        {
            var targets = new List<TargetEntry>();
            if (offer.Group == MotherOfferGroup.Transport)
            {
                if (!offer.Evacuation)
                    targets.AddRange(dropPoints);
                else if (market.Comp != null)
                    targets.AddRange(ActiveCities(market).Where(c => market.Comp.CityCrises.ContainsKey(c.ID)).Select(c => new TargetEntry(c.ID, Loc.GetString(c.Name))));
            }

            var blocked = OfferBlocked(faction, offer);
            if (blocked == null && offer.Group == MotherOfferGroup.Transport && targets.Count == 0)
                blocked = Loc.GetString(offer.Evacuation ? "rumc-mother-no-crisis-city" : "rumc-mother-no-drop-point");

            offers.Add(new MotherOfferEntry(offer.ID, offer.Group, FactionLabel(faction, offer),
                Loc.GetString(offer.Name), Loc.GetString(offer.Description), OfferCost(faction, offer), blocked ?? string.Empty, targets));
        }

        return offers;
    }

    // How a faction stands to a company: its own, an ally's (borrowed), nobody's (free), or another faction's (closed). A company with a home faction and nothing else is nobody's.
    public PmcAccessKind PmcAccess(string faction, WorldMotherOfferPrototype offer)
    {
        if (offer.Home.Length == 0)
            return PmcAccessKind.Free;

        if (offer.Home == faction)
            return PmcAccessKind.Own;

        return TryMarket(out var market) && InPact(market, faction, offer.Home) ? PmcAccessKind.Ally : PmcAccessKind.Closed;
    }

    // The heading that the list shows over a company: the home faction, and how it stands to the faction that looks.
    private string FactionLabel(string faction, WorldMotherOfferPrototype offer)
    {
        if (string.IsNullOrEmpty(offer.Faction))
            return string.Empty;

        var name = Loc.GetString(offer.Faction);
        return PmcAccess(faction, offer) switch
        {
            PmcAccessKind.Own => Loc.GetString("rumc-mother-pmc-own", ("faction", name)),
            PmcAccessKind.Ally => Loc.GetString("rumc-mother-pmc-ally", ("faction", name)),
            PmcAccessKind.Closed => Loc.GetString("rumc-mother-pmc-closed", ("faction", name)),
            _ => offer.Group == MotherOfferGroup.Pmc ? Loc.GetString("rumc-mother-pmc-free", ("faction", name)) : name,
        };
    }

    // An evacuation: people are taken out of a city that is in trouble, the trouble ends, and the sooner the more favour. The points are taken here. Gives whether it was done.
    public bool EvacuateCity(string faction, string cityId, int cost)
    {
        if (!TryMarket(out var market) || !market.Comp.CityCrises.TryGetValue(cityId, out var crisis) || !TrySpendPoints(faction, cost))
            return false;

        var total = Math.Max(1.0, (crisis.EndsAt - crisis.StartedAt).TotalSeconds);
        var left = Math.Max(0.0, (crisis.EndsAt - _timing.CurTime).TotalSeconds);

        market.Comp.CityCrises.Remove(cityId);
        StatsOf(market, faction).Crises++;
        AddCityInfluence(market, faction, cityId, (int) Math.Round(14.0 * (1.0 + left / total)));
        return true;
    }
}

// How a faction stands to a private military company (the order is the order of the list).
public enum PmcAccessKind : byte
{
    Own,
    Ally,
    Free,
    Closed,
}
