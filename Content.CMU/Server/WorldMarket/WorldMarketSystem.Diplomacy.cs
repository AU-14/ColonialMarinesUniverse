// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Shared.CMU14.WorldMarket;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.WorldMarket;

// What the ambassadors do between themselves in the cities. Pacts (neither is surcharged in the regions of interest of the other, neither may do the other harm: a harm done breaks the pact in
// view of all and gives the victim the right to answer). Secret strikes against the favour another faction has in a city (they may fail, and then the one who made them is known and loses the
// trust of the mother faction). Bargains for a city, opened when two factions have almost the same favour in it. Joint works of two factions. Everything is asked through MapCommand.
public sealed partial class WorldMarketSystem
{
    private static readonly TimeSpan PactOfferTime = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan RetaliationTime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan StrikeCooldown = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan BidTime = TimeSpan.FromMinutes(6);
    private static readonly TimeSpan PatronTime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan JointOfferTime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan BidCheckEvery = TimeSpan.FromSeconds(30);

    // A secret strike costs this, half of it as an answer to a harm.
    public const int StrikeCost = 600;

    // The favour that two factions must both have in a city, and the most that they may differ by, for a bargain to open; the least that a bid is; what the winner gets and its discount.
    private const int BidMinInfluence = 20;
    private const int BidMaxGap = 10;
    private const int BidMinRaise = 100;
    private const int PatronInfluence = 15;
    private const int PatronDiscountPercent = 20;

    // The favour that both factions must keep in a city for a joint work there to work.
    private const int JointMinInfluence = 20;

    private int _nextOfferId;

    public int? MapCommand(string faction, MapCommandKind kind, string a, string b, string c, int n, float budget)
    {
        if (!TryMarket(out var market))
            return null;

        market.Comp.KnownFactions.Add(faction);

        switch (kind)
        {
            case MapCommandKind.PactPropose:
                return PactPropose(market, faction, a) ? 0 : null;
            case MapCommandKind.PactAccept:
                return PactAccept(market, faction, a) ? 0 : null;
            case MapCommandKind.PactBreak:
                return PactBreak(market, faction, a) ? 0 : null;
            case MapCommandKind.Strike:
                return Strike(market, faction, a, b, budget);
            case MapCommandKind.Bid:
                return Bid(market, faction, a, n, budget);
            case MapCommandKind.JointPropose:
                return JointPropose(market, faction, a, b, c, budget);
            case MapCommandKind.JointAnswer:
                return JointAnswer(market, faction, a, n != 0, budget);
            case MapCommandKind.EventSupport:
                return EventSupport(market, faction, a, n, budget);
            case MapCommandKind.EventSpoil:
                return EventSpoil(market, a, budget);
            case MapCommandKind.HazardRemove:
                return HazardRemove(market, faction, a, budget);
            case MapCommandKind.Escort:
                return CaravanEscort(market, faction, a, budget);
            case MapCommandKind.DealPropose:
                return DealPropose(market, faction, a, b, c, budget);
            case MapCommandKind.DealAnswer:
                return DealAnswer(market, faction, a, n != 0, budget);
            case MapCommandKind.Intel:
                return Intel(market, faction, a, budget);
            case MapCommandKind.Scouts:
                return Scouts(market, faction, a, budget);
            case MapCommandKind.Smear:
                return Smear(market, faction, a, b, budget);
            case MapCommandKind.Sabotage:
                return Sabotage(market, faction, a, b, budget);
            case MapCommandKind.Provoke:
                return Provoke(market, faction, a, budget);
            case MapCommandKind.CounterIntel:
                return Counter(market, faction, budget);
            case MapCommandKind.Buyout:
                return Buyout(market, faction, a, b, budget);
            case MapCommandKind.Freeze:
                return Freeze(market, faction, a, b, budget);
            case MapCommandKind.Poison:
                return Poison(market, faction, a, b, budget);
            case MapCommandKind.Leak:
                return Leak(market, faction, a, budget);
            case MapCommandKind.Disinfo:
                return Disinfo(market, faction, budget);
            case MapCommandKind.Intercept:
                return Intercept(market, faction, a, b, budget);
            case MapCommandKind.Spy:
                return Spy(market, faction, a, budget);
            case MapCommandKind.Complain:
                return Complain(market, faction, a);
            case MapCommandKind.PayOff:
                return PayOff(market, faction, budget);
            case MapCommandKind.SabotageTasks:
                return SabotageTasks(market, faction, a, budget);
            default:
                return null;
        }
    }

    // The price of a thing done for a city (or a gift): higher in the region of interest of another faction (the extra goes to the owner), lower for the winner of the bargain for the city.
    private int CityPrice(Entity<WorldMarketComponent> market, string faction, string cityId, int price, out string? owner, out int surcharge)
    {
        owner = ZoneOwnerOf(market, cityId, faction);
        surcharge = Surcharge(price, owner);

        var discount = 0;
        if (market.Comp.Patrons.TryGetValue(cityId, out var patron) && patron.Faction == faction && patron.Until > _timing.CurTime)
            discount = (int) Math.Ceiling(price * PatronDiscountPercent / 100f);

        return price <= 0 ? 0 : Math.Max(1, price + surcharge - discount);
    }

    #region Pacts

    public bool InPact(Entity<WorldMarketComponent> market, string x, string y)
    {
        return market.Comp.Pacts.Any(p => (p.A == x && p.B == y) || (p.A == y && p.B == x));
    }

    private bool PactPropose(Entity<WorldMarketComponent> market, string faction, string other)
    {
        var comp = market.Comp;
        var now = _timing.CurTime;
        if (other.Length == 0 || other == faction || !comp.KnownFactions.Contains(other) || InPact(market, faction, other))
            return false;

        if (comp.PactOffers.Any(o => o.From == faction && o.To == other && o.Until > now))
            return false;

        comp.PactOffers.Add(new PactOffer { From = faction, To = other, Until = now + PactOfferTime });
        return true;
    }

    private bool PactAccept(Entity<WorldMarketComponent> market, string faction, string other)
    {
        var comp = market.Comp;
        var now = _timing.CurTime;
        if (!comp.PactOffers.Any(o => o.From == other && o.To == faction && o.Until > now) || InPact(market, faction, other))
            return false;

        comp.PactOffers.RemoveAll(o => (o.From == other && o.To == faction) || (o.From == faction && o.To == other));
        comp.Pacts.Add(new Pact { A = other, B = faction });
        return true;
    }

    // The pact is broken by hand: it costs a little of the trust of the mother faction.
    private bool PactBreak(Entity<WorldMarketComponent> market, string faction, string other)
    {
        if (market.Comp.Pacts.RemoveAll(p => (p.A == faction && p.B == other) || (p.A == other && p.B == faction)) == 0)
            return false;

        AddTrust(market, faction, -3);
        return true;
    }

    // A harm is done to a faction that the offender has a pact with: the pact is broken in view of all, the offender loses trust, and the victim gets the right to answer.
    private void Violate(Entity<WorldMarketComponent> market, string offender, string victim)
    {
        var comp = market.Comp;
        comp.Pacts.RemoveAll(p => (p.A == offender && p.B == victim) || (p.A == victim && p.B == offender));
        AddTrust(market, offender, -10);

        comp.Retaliations.RemoveAll(r => r.Victim == victim && r.Offender == offender);
        comp.Retaliations.Add(new Retaliation { Victim = victim, Offender = offender, Until = _timing.CurTime + RetaliationTime });
    }

    private bool HasRetaliation(Entity<WorldMarketComponent> market, string victim, string offender)
    {
        var now = _timing.CurTime;
        return market.Comp.Retaliations.Any(r => r.Victim == victim && r.Offender == offender && r.Until > now);
    }

    #endregion

    #region Secret strikes

    // How likely a strike is to succeed, in percent: the more favour the striker has in the city and the less the target, the likelier; an answer to a harm is likelier.
    private int StrikeChance(Entity<WorldMarketComponent> market, string faction, string cityId, string target)
    {
        var chance = Math.Clamp(35 + InfluenceOf(market, faction, cityId) - InfluenceOf(market, target, cityId) - JointGuard(market, target, cityId), 10, 85);
        return HasRetaliation(market, faction, target) ? Math.Min(95, chance + 40) : chance;
    }

    // What the partners of a joint work gain from it besides the work itself: it brings in half as much again, the mother faction thinks the better of both when it is begun, and the two
    // guard one another in the city while it stands (a harm to either is less likely to succeed).
    public const float JointIncomeFactor = 1.5f;
    public const int JointTrustGain = 3;
    public const int JointGuardPercent = 20;

    // How much less likely a secret harm to the faction in the city is, because of the joint works that it has standing there.
    private int JointGuard(Entity<WorldMarketComponent> market, string faction, string cityId)
    {
        var now = _timing.CurTime;
        return market.Comp.CityProjects.Any(p => p.Faction == faction && p.City == cityId && p.Partner.Length > 0 && p.Rewarded && p.ExpiresAt > now && JointWorks(market, p))
            ? JointGuardPercent
            : 0;
    }

    private int StrikePrice(Entity<WorldMarketComponent> market, string faction, string target)
    {
        return HasRetaliation(market, faction, target) ? StrikeCost / 2 : StrikeCost;
    }

    private string? StrikeBlocked(Entity<WorldMarketComponent> market, string faction, string cityId, string target, float budget)
    {
        var now = _timing.CurTime;
        if (market.Comp.StrikeReady.TryGetValue(faction, out var ready) && ready > now)
            return Loc.GetString("rumc-map-strike-wait", ("time", (int) Math.Ceiling((ready - now).TotalSeconds)));

        if (InfluenceOf(market, target, cityId) <= 0)
            return Loc.GetString("rumc-map-strike-nothing");

        if (budget < StrikePrice(market, faction, target))
            return Loc.GetString("rumc-map-need-budget");

        return null;
    }

    private int? Strike(Entity<WorldMarketComponent> market, string faction, string cityId, string target, float budget)
    {
        var comp = market.Comp;
        if (target == faction || !comp.KnownFactions.Contains(target) || ActiveCities(market).All(c => c.ID != cityId) || StrikeBlocked(market, faction, cityId, target, budget) != null)
            return null;

        var now = _timing.CurTime;
        var price = StrikePrice(market, faction, target);
        var chance = StrikeChance(market, faction, cityId, target);
        var answering = HasRetaliation(market, faction, target);

        comp.StrikeReady[faction] = now + StrikeCooldown;
        LogHarm(market, faction, target, cityId, "rumc-map-harm-strike");

        // A strike against a faction that there is a pact with breaks the pact, even if it succeeds (unless it is the answer to a harm).
        if (answering)
            comp.Retaliations.RemoveAll(r => r.Victim == faction && r.Offender == target);
        else if (InPact(market, faction, target))
            Violate(market, faction, target);

        if (_random.Prob(chance / 100f))
        {
            AddCityInfluence(market, target, cityId, -_random.Next(8, 15));
        }
        else
        {
            AddTrust(market, faction, -8);
            comp.Retaliations.RemoveAll(r => r.Victim == target && r.Offender == faction);
            comp.Retaliations.Add(new Retaliation { Victim = target, Offender = faction, Until = now + RetaliationTime });
        }

        return price;
    }

    #endregion

    #region Bargains

    private int? Bid(Entity<WorldMarketComponent> market, string faction, string cityId, int amount, float budget)
    {
        var now = _timing.CurTime;
        if (!market.Comp.Bids.TryGetValue(cityId, out var bid) || bid.EndsAt <= now || amount < BidMinRaise || budget < amount)
            return null;

        if (InfluenceOf(market, faction, cityId) < 10)
            return null;

        bid.Totals[faction] = bid.Totals.GetValueOrDefault(faction) + amount;
        return amount;
    }

    private void UpdateBargains(Entity<WorldMarketComponent> market, TimeSpan now)
    {
        var comp = market.Comp;

        foreach (var cityId in comp.Bids.Where(pair => pair.Value.EndsAt <= now).Select(pair => pair.Key).ToList())
        {
            var bid = comp.Bids[cityId];
            comp.Bids.Remove(cityId);

            var best = bid.Totals.OrderByDescending(pair => pair.Value).ToList();
            if (best.Count == 0 || (best.Count > 1 && best[0].Value == best[1].Value))
            {
                continue;
            }

            var winner = best[0].Key;
            comp.Patrons[cityId] = new CityPatron { Faction = winner, Until = now + PatronTime };
            AddCityInfluence(market, winner, cityId, PatronInfluence);
        }

        foreach (var cityId in comp.Patrons.Where(pair => pair.Value.Until <= now).Select(pair => pair.Key).ToList())
            comp.Patrons.Remove(cityId);

        if (now < comp.NextBidCheck)
            return;

        comp.NextBidCheck = now + BidCheckEvery;

        // A city where two factions stand almost level opens a bargain.
        foreach (var city in ActiveCities(market))
        {
            if (comp.Bids.ContainsKey(city.ID) || comp.Patrons.ContainsKey(city.ID))
                continue;

            var top = comp.KnownFactions
                .Select(f => (Faction: f, Influence: InfluenceOf(market, f, city.ID)))
                .Where(x => x.Influence >= BidMinInfluence)
                .OrderByDescending(x => x.Influence)
                .Take(2)
                .ToList();

            if (top.Count < 2 || top[0].Influence - top[1].Influence > BidMaxGap)
                continue;

            comp.Bids[city.ID] = new CityBid { City = city.ID, EndsAt = now + BidTime };
        }
    }

    #endregion

    #region Joint works

    private string? JointBlocked(Entity<WorldMarketComponent> market, string faction, string cityId, WorldCityActionPrototype action, float budget)
    {
        if (!FitsPlace(cityId, action))
            return Loc.GetString(action.Placement == WorldPlacement.City ? "rumc-map-only-cities" : "rumc-map-only-sites");

        if (InfluenceOf(market, faction, cityId) < Math.Max(JointMinInfluence, action.MinInfluence))
            return Loc.GetString("rumc-map-need-influence", ("value", Math.Max(JointMinInfluence, action.MinInfluence)));

        if (budget < action.Cost)
            return Loc.GetString("rumc-map-need-budget");

        return null;
    }

    private int? JointPropose(Entity<WorldMarketComponent> market, string faction, string cityId, string actionId, string partner, float budget)
    {
        var comp = market.Comp;
        var now = _timing.CurTime;

        if (partner == faction || !comp.KnownFactions.Contains(partner) || ActiveCities(market).All(c => c.ID != cityId)
            || !_proto.TryIndex<WorldCityActionPrototype>(actionId, out var action) || !action.Joint
            || JointBlocked(market, faction, cityId, action, budget) != null
            || comp.JointOffers.Any(o => o.From == faction && o.To == partner && o.City == cityId && o.Action == actionId && o.Until > now))
        {
            return null;
        }

        comp.JointOffers.Add(new JointOffer
        {
            Id = "j" + ++_nextOfferId,
            From = faction,
            To = partner,
            City = cityId,
            Action = actionId,
            Until = now + JointOfferTime,
        });


        return action.Cost;
    }

    private int? JointAnswer(Entity<WorldMarketComponent> market, string faction, string offerId, bool accept, float budget)
    {
        var comp = market.Comp;
        var offer = comp.JointOffers.FirstOrDefault(o => o.Id == offerId && o.To == faction && o.Until > _timing.CurTime);
        if (offer == null || !_proto.TryIndex<WorldCityActionPrototype>(offer.Action, out var action))
            return null;

        if (!accept)
        {
            comp.JointOffers.Remove(offer);
            PayFaction(market, offer.From, action.Cost);
            return 0;
        }

        if (JointBlocked(market, faction, offer.City, action, budget) != null)
            return null;

        comp.JointOffers.Remove(offer);
        StartJointProject(market, offer.From, offer.To, offer.City, action);
        StartJointProject(market, offer.To, offer.From, offer.City, action);


        return action.Cost;
    }

    private void StartJointProject(Entity<WorldMarketComponent> market, string faction, string partner, string cityId, WorldCityActionPrototype action)
    {
        var now = _timing.CurTime;

        // Working together pleases the mother faction.
        AddTrust(market, faction, JointTrustGain);
        var ready = now + TimeSpan.FromSeconds(action.BuildSeconds);
        var project = new CityProject
        {
            Faction = faction,
            City = cityId,
            Action = action.ID,
            ReadyAt = ready,
            ExpiresAt = action.EffectSeconds > 0 ? ready + TimeSpan.FromSeconds(action.EffectSeconds) : TimeSpan.MaxValue,
            Gain = action.Influence,
            Charges = action.Charges,
            Paid = action.Cost,
            Partner = partner,
        };

        if (action.BuildSeconds <= 0)
        {
            project.Rewarded = true;
            AddCityInfluence(market, faction, cityId, action.Influence);
        }

        StatsOf(market, faction).Projects++;
        market.Comp.CityProjects.Add(project);
    }

    // A joint work works while both factions keep their favour in the city.
    private bool JointWorks(Entity<WorldMarketComponent> market, CityProject project)
    {
        return project.Partner.Length == 0
               || (InfluenceOf(market, project.Faction, project.City) >= JointMinInfluence && InfluenceOf(market, project.Partner, project.City) >= JointMinInfluence);
    }

    #endregion

    // Offers and bargains that are over.
    private void UpdateDiplomacy(Entity<WorldMarketComponent> market, TimeSpan now)
    {
        var comp = market.Comp;
        comp.PactOffers.RemoveAll(o => o.Until <= now);
        comp.Retaliations.RemoveAll(r => r.Until <= now);

        // A joint work that the other did not take up in time: the share that was paid comes back.
        for (var i = comp.JointOffers.Count - 1; i >= 0; i--)
        {
            var offer = comp.JointOffers[i];
            if (offer.Until > now)
                continue;

            if (_proto.TryIndex<WorldCityActionPrototype>(offer.Action, out var action))
                PayFaction(market, offer.From, action.Cost);

            comp.JointOffers.RemoveAt(i);
        }

        UpdateBargains(market, now);
    }

    #region What the console is told

    // The other factions, and what the faction stands in with each.
    private List<FactionEntry> BuildFactionEntries(Entity<WorldMarketComponent> market, string faction)
    {
        var now = _timing.CurTime;
        var comp = market.Comp;

        return comp.KnownFactions
            .Where(other => other != faction)
            .OrderBy(other => other, StringComparer.Ordinal)
            .Select(other => new FactionEntry(
                other,
                InPact(market, faction, other),
                comp.PactOffers.Any(o => o.From == faction && o.To == other && o.Until > now),
                comp.PactOffers.Any(o => o.From == other && o.To == faction && o.Until > now),
                HasRetaliation(market, faction, other)))
            .ToList();
    }

    private List<JointOfferEntry> BuildJointOfferEntries(Entity<WorldMarketComponent> market, string faction, float budget)
    {
        var entries = new List<JointOfferEntry>();
        var now = _timing.CurTime;

        foreach (var offer in market.Comp.JointOffers.Where(o => o.To == faction && o.Until > now))
        {
            if (!_proto.TryIndex<WorldCityActionPrototype>(offer.Action, out var action))
                continue;

            entries.Add(new JointOfferEntry(offer.Id, offer.From, CityName(offer.City), Loc.GetString(action.Name), action.Cost,
                JointBlocked(market, faction, offer.City, action, budget) ?? string.Empty));
        }

        return entries;
    }

    // What is going on in a city besides the troubles, for the console of a faction.
    private CityExtras BuildCityExtras(Entity<WorldMarketComponent> market, string faction, string cityId, float budget)
    {
        var comp = market.Comp;
        var now = _timing.CurTime;
        var extras = new CityExtras { Shares = SharesOf(market, cityId) };

        if (comp.CityEvents.TryGetValue(cityId, out var cityEvent) && _proto.TryIndex<WorldCityEventPrototype>(cityEvent.Id, out var eventProto))
        {
            extras.EventName = Loc.GetString(eventProto.Name);
            extras.EventDescription = Loc.GetString(eventProto.Description);
            extras.EventSeconds = Math.Max(0, (int) Math.Ceiling((cityEvent.EndsAt - now).TotalSeconds));
            extras.EventFunding = cityEvent.Funding.Values.Sum();
            extras.EventGoal = eventProto.Goal;
            extras.EventMine = cityEvent.Funding.GetValueOrDefault(faction);
            extras.EventSpoilers = cityEvent.Spoilers;
            extras.EventSpoilCost = EventSpoilCost;
        }

        if (comp.Bids.TryGetValue(cityId, out var bid) && bid.EndsAt > now)
        {
            extras.BidSeconds = Math.Max(0, (int) Math.Ceiling((bid.EndsAt - now).TotalSeconds));
            extras.Bids = bid.Totals.OrderByDescending(pair => pair.Value).Select(pair => new BidEntry(pair.Key, pair.Value)).ToList();
        }

        if (comp.Patrons.TryGetValue(cityId, out var patron) && patron.Until > now)
        {
            extras.Patron = patron.Faction;
            extras.PatronSeconds = Math.Max(0, (int) Math.Ceiling((patron.Until - now).TotalSeconds));
        }

        foreach (var target in comp.KnownFactions.Where(f => f != faction).OrderBy(f => f, StringComparer.Ordinal))
        {
            if (InfluenceOf(market, target, cityId) <= 0)
                continue;

            extras.Strikes.Add(new StrikeEntry(target, StrikePrice(market, faction, target), StrikeChance(market, faction, cityId, target),
                HasRetaliation(market, faction, target), StrikeBlocked(market, faction, cityId, target, budget) ?? string.Empty));
        }

        if (_proto.TryIndex<WorldCityPrototype>(cityId, out var cityProto) && cityProto.Capabilities.Contains(CityCapability.Intelligence))
        {
            extras.IntelCost = IntelCost;
            extras.IntelBlocked = IntelBlocked(market, faction, cityProto, budget) ?? string.Empty;
        }

        foreach (var action in _proto.EnumeratePrototypes<WorldCityActionPrototype>().Where(a => a.Joint && FitsPlace(cityId, a)).OrderBy(a => a.Order))
        {
            extras.JointActions.Add(new ActionEntry(action.ID, Loc.GetString(action.Name), Loc.GetString(action.Description), action.Cost, action.Influence,
                JointBlocked(market, faction, cityId, action, budget) ?? string.Empty));
        }

        FillEspionage(market, faction, cityId, budget, extras);

        return extras;
    }

    #endregion
}
