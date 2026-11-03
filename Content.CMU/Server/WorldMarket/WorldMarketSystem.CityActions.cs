// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Shared.CMU14.WorldMarket;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.WorldMarket;

// What the ambassadors do for the cities of the map, and what happens to the cities. An action (see WorldCityActionPrototype) is paid by the console and may be done at once or built over a time;
// what stands brings money to the budget of the faction, keeps troubles away from the city and gives favour when it is ready. Now and then a city falls into trouble (see
// WorldCrisisPrototype): the ones that have a well, a hospital or a store keep it off, the ones that come in time end it and win the favour of the city, and if nobody does anything every
// faction loses some of its favour there.
public sealed partial class WorldMarketSystem
{
    // How much more seldom the troubles of the cities (and the hazards of the map) come than they did: all the times between them are this many times longer.
    public const int RareFactor = 3;

    private static readonly TimeSpan FirstCityCrisis = TimeSpan.FromMinutes(3 * RareFactor);

    // The share of what the standing things were meant to bring in that they do bring: the incomes of the ambassadors are cut hard.
    public const float ProjectIncomeFactor = 0.3f;

    private static bool Has(List<ProtoId<WorldCrisisPrototype>> list, string id)
    {
        foreach (var entry in list)
        {
            if (entry.Id == id)
                return true;
        }

        return false;
    }

    private string CityName(string cityId)
    {
        return _proto.TryIndex<WorldCityPrototype>(cityId, out var city) ? Loc.GetString(city.Name) : cityId;
    }

    // Raises (or lowers) the favour of a faction in a city, within nothing and the most.
    public void AddCityInfluence(Entity<WorldMarketComponent> market, string faction, string cityId, int amount)
    {
        if (!market.Comp.CityInfluence.TryGetValue(faction, out var ofFaction))
            market.Comp.CityInfluence[faction] = ofFaction = new Dictionary<string, int>();

        var before = ofFaction.GetValueOrDefault(cityId);
        var after = Math.Clamp(before + amount, 0, MaxInfluence);
        ofFaction[cityId] = after;

        if (after > before)
        {
            // There is one scale of favour in a city for all the ambassadors: what one wins above the hundred that there are is taken from the others.
            ShareCity(market, faction, cityId);
            PayMotherSteps(market, faction, cityId, before, after);
        }
    }

    // The favour of all the factions in a city adds up to MaxInfluence at most: when the faction that has just won some takes the sum over it, the others lose the difference in
    // proportion to what each of them has (so thirty for one and seventy for another stay one scale, not two).
    private void ShareCity(Entity<WorldMarketComponent> market, string winner, string cityId)
    {
        var influence = market.Comp.CityInfluence;
        var total = influence.Values.Sum(f => f.GetValueOrDefault(cityId));
        var excess = total - MaxInfluence;
        if (excess <= 0)
            return;

        var others = influence.Where(pair => pair.Key != winner && pair.Value.GetValueOrDefault(cityId) > 0).Select(pair => pair.Key).ToList();
        var pool = others.Sum(f => influence[f][cityId]);
        if (pool <= 0)
            return;

        excess = Math.Min(excess, pool);
        var taken = 0;
        foreach (var other in others)
        {
            var cut = (int) (excess * (long) influence[other][cityId] / pool);
            influence[other][cityId] -= cut;
            taken += cut;
        }

        // What the division left over is taken one point at a time from whoever has the most.
        for (var left = excess - taken; left > 0; left--)
        {
            var biggest = others.OrderByDescending(f => influence[f][cityId]).First();
            if (influence[biggest][cityId] <= 0)
                break;

            influence[biggest][cityId]--;
        }
    }

    // The share of each faction in the one scale of a city (those that have any).
    public List<BidEntry> SharesOf(Entity<WorldMarketComponent> market, string cityId)
    {
        return market.Comp.CityInfluence
            .Select(pair => new BidEntry(pair.Key, pair.Value.GetValueOrDefault(cityId)))
            .Where(entry => entry.Total > 0)
            .OrderByDescending(entry => entry.Total)
            .ToList();
    }

    // How many great buildings (those that bring points) a faction has now, and how many it may have.
    public const int MaxPointBuildings = 3;

    private int PointBuildings(Entity<WorldMarketComponent> market, string faction)
    {
        var now = _timing.CurTime;

        // Those that stand, and those that are still on the road to the city (a caravan carries them).
        var standing = market.Comp.CityProjects.Count(p => p.Faction == faction && p.ExpiresAt > now
                                                           && _proto.TryIndex<WorldCityActionPrototype>(p.Action, out var action) && action.PointEverySeconds > 0);
        var coming = market.Comp.Caravans.Count(c => c.Faction == faction && !c.Gift
                                                     && _proto.TryIndex<WorldCityActionPrototype>(c.Action, out var action) && action.PointEverySeconds > 0);

        return standing + coming;
    }

    private bool HasProject(Entity<WorldMarketComponent> market, string faction, string cityId, string actionId, TimeSpan now)
    {
        foreach (var project in market.Comp.CityProjects)
        {
            if (project.Faction == faction && project.City == cityId && project.Action == actionId && project.ExpiresAt > now)
                return true;
        }

        return false;
    }

    // A deposit (a mine, a well, a plantation) makes what is built on it bring in twice as much.
    private float DepositFactor(string cityId)
    {
        return _proto.TryIndex<WorldCityPrototype>(cityId, out var place) && place.Capabilities.Contains(CityCapability.Deposit) ? 2f : 1f;
    }

    // Can the action be done in this place at all: some buildings are for the cities only, some for the other objects of the map only.
    private bool FitsPlace(string cityId, WorldCityActionPrototype action)
    {
        if (!_proto.TryIndex<WorldCityPrototype>(cityId, out var place))
            return false;

        return action.Placement switch
        {
            WorldPlacement.City => !place.Site,
            WorldPlacement.Site => place.Site,
            _ => true,
        };
    }

    // Why the action cannot be done now (in words), or nothing if it can.
    public string? ActionBlocked(Entity<WorldMarketComponent> market, string faction, string cityId, WorldCityActionPrototype action, float budget)
    {
        var now = _timing.CurTime;

        if (!FitsPlace(cityId, action))
            return Loc.GetString(action.Placement == WorldPlacement.City ? "rumc-map-only-cities" : "rumc-map-only-sites");

        // A faction has one of the buildings that are only one in the world.
        if (action.Unique && HasUnique(market, faction, action.ID, now))
            return Loc.GetString("rumc-map-unique");

        var influence = InfluenceOf(market, faction, cityId);
        if (influence < action.MinInfluence)
            return Loc.GetString("rumc-map-need-influence", ("value", action.MinInfluence));

        if (action.OnlyInCrisis && !(market.Comp.CityCrises.TryGetValue(cityId, out var crisis) && Has(action.Resolves, crisis.Id)))
            return Loc.GetString("rumc-map-no-crisis");

        if (!action.Stackable && HasProject(market, faction, cityId, action.ID, now))
            return Loc.GetString("rumc-map-already");

        // A faction may have only a few of the great buildings that bring points.
        if (action.PointEverySeconds > 0 && PointBuildings(market, faction) >= MaxPointBuildings)
            return Loc.GetString("rumc-map-point-limit", ("value", MaxPointBuildings));

        if (action.PointCost > 0 && PointsOf(faction) < action.PointCost)
            return Loc.GetString("rumc-mother-need-points", ("value", action.PointCost));

        if (budget < CityPrice(market, faction, cityId, action.Cost, out _, out _))
            return Loc.GetString("rumc-map-need-budget");

        // What goes by the roads needs an open way from the city that it goes from.
        if (!action.OnlyInCrisis && action.PointCost == 0 && !RouteOk(market, faction, cityId))
            return Loc.GetString("rumc-map-no-route");

        return null;
    }

    // Does the action: the cost is paid (the caller takes it from the budget; it is given), and the work goes to the city by the roads as a caravan, seen on the map, which a threat on the way
    // may take. What is done against a trouble that is on goes at once, and so does what there is no city to send from. Gives the cost, or nothing if the action cannot be done.
    public int? DoCityAction(string faction, string cityId, string actionId, float budget)
    {
        if (!TryMarket(out var market)
            || !_proto.TryIndex<WorldCityActionPrototype>(actionId, out var action)
            || action.Joint
            || !_proto.HasIndex<WorldCityPrototype>(cityId)
            || ActionBlocked(market, faction, cityId, action, budget) != null)
        {
            return null;
        }

        // In the region of interest of another faction it costs more (and the extra goes to the owner), and less for the winner of the bargain for the city.
        var price = CityPrice(market, faction, cityId, action.Cost, out var zoneOwner, out var surcharge);
        if (zoneOwner != null && surcharge > 0)
            PayFaction(market, zoneOwner, surcharge);

        // What is bought for the points is paid to the mother faction, which puts it up at once: no caravan.
        if (action.PointCost > 0 && !TrySpendPoints(faction, action.PointCost))
            return null;

        var origin = action.OnlyInCrisis || action.PointCost > 0 ? null : CaravanOrigin(market, faction, cityId);
        var path = origin != null ? FindPath(market, origin, cityId) : null;

        if (path != null)
            StartCaravan(market, faction, path, false, action.ID);
        else
            ApplyCityAction(market, faction, cityId, action);

        return price;
    }

    // The work comes into force in the city (at once, or when the caravan has come).
    private void ApplyCityAction(Entity<WorldMarketComponent> market, string faction, string cityId, WorldCityActionPrototype action)
    {
        var now = _timing.CurTime;
        var gain = action.Influence;

        // Against a trouble that is on: it ends, and the sooner the more of the favour.
        if (market.Comp.CityCrises.TryGetValue(cityId, out var crisis) && Has(action.Resolves, crisis.Id))
        {
            var total = Math.Max(1.0, (crisis.EndsAt - crisis.StartedAt).TotalSeconds);
            var left = Math.Max(0.0, (crisis.EndsAt - now).TotalSeconds);
            gain = (int) Math.Round(action.Influence * (1.0 + left / total));

            market.Comp.CityCrises.Remove(cityId);
            StatsOf(market, faction).Crises++;
        }

        StatsOf(market, faction).Projects++;

        // What stands (or is built, or brings in, or keeps off) is a project; what is done at once is done.
        var standing = action.BuildSeconds > 0 || action.EffectSeconds > 0 || action.IncomePerMinute != 0f || action.ReturnPercent > 0
                       || action.Protects.Count > 0 || action.Absorbs.Count > 0 || action.RepairsRoads;

        if (!standing)
        {
            AddCityInfluence(market, faction, cityId, gain);
            return;
        }

        var ready = now + TimeSpan.FromSeconds(action.BuildSeconds);
        var project = new CityProject
        {
            Faction = faction,
            City = cityId,
            Action = action.ID,
            ReadyAt = ready,
            ExpiresAt = action.EffectSeconds > 0 ? ready + TimeSpan.FromSeconds(action.EffectSeconds) : TimeSpan.MaxValue,
            Gain = gain,
            Charges = action.Charges,
            Paid = action.Cost,
        };

        if (action.BuildSeconds <= 0)
        {
            project.Rewarded = true;
            AddCityInfluence(market, faction, cityId, gain);
        }

        market.Comp.CityProjects.Add(project);
    }

    private string CrisisName(string id)
    {
        return _proto.TryIndex<WorldCrisisPrototype>(id, out var crisis) ? Loc.GetString(crisis.Name) : id;
    }

    // The money that the standing things of a faction have brought in since it was asked last (below nothing if they have cost more).
    public float ClaimIncome(string faction)
    {
        if (!TryMarket(out var market))
            return 0f;

        // A faction that has a console is known to the mother faction (it gives it tasks and sends it money).
        market.Comp.KnownFactions.Add(faction);
        return market.Comp.FactionIncome.Remove(faction, out var income) ? income : 0f;
    }

    // Is something that keeps the trouble away standing in the city (it is ready, and has not ended)?
    private CityProject? Protector(Entity<WorldMarketComponent> market, string cityId, string crisisId, TimeSpan now, bool absorbing)
    {
        foreach (var project in market.Comp.CityProjects)
        {
            if (project.City != cityId || !project.Rewarded || project.ExpiresAt <= now || project.FrozenUntil > now || !_proto.TryIndex<WorldCityActionPrototype>(project.Action, out var action))
                continue;

            // A joint work protects only while both partners keep their favour in the city.
            if (!JointWorks(market, project))
                continue;

            if (absorbing ? project.Charges > 0 && Has(action.Absorbs, crisisId) : Has(action.Protects, crisisId))
                return project;
        }

        return null;
    }

    // Once a second: what was ordered is built and ends, the money comes in, the troubles come and go.
    private void UpdateCities(Entity<WorldMarketComponent> market, TimeSpan now)
    {
        var comp = market.Comp;
        comp.TaxBreaks.RemoveAll(taxBreak => taxBreak.Until <= now);
        comp.Zones.RemoveAll(zone => zone.Until <= now);

        for (var i = comp.CityProjects.Count - 1; i >= 0; i--)
        {
            var project = comp.CityProjects[i];
            if (!_proto.TryIndex<WorldCityActionPrototype>(project.Action, out var action))
            {
                comp.CityProjects.RemoveAt(i);
                continue;
            }

            if (!project.Rewarded && now >= project.ReadyAt)
            {
                project.Rewarded = true;
                AddCityInfluence(market, project.Faction, project.City, project.Gain);
            }

            if (project.Rewarded && project.ExpiresAt > now && project.FrozenUntil <= now && action.IncomePerMinute != 0f && JointWorks(market, project))
            {
                // What it brings in is less in a bad season, under a hazard, and when all the roads to the city are shut; what it costs is not.
                var income = action.IncomePerMinute / 60f;
                if (income > 0f)
                    income *= IncomeFactorOf(project.City) * ProjectIncomeFactor * DepositFactor(project.City) * (project.Partner.Length > 0 ? JointIncomeFactor : 1f);

                PayFaction(market, project.Faction, income);

                if (income > 0f)
                    StatsOf(market, project.Faction).Earned += income;
            }

            // A great building brings a point of influence with the mother faction every so often while it stands.
            if (action.PointEverySeconds > 0 && project.Rewarded && project.ExpiresAt > now && JointWorks(market, project))
            {
                if (project.NextPoint == TimeSpan.Zero)
                {
                    project.NextPoint = now + TimeSpan.FromSeconds(action.PointEverySeconds);
                }
                else if (now >= project.NextPoint)
                {
                    AddPoints(market, project.Faction, action.PointsPerPeriod);
                    project.NextPoint += TimeSpan.FromSeconds(action.PointEverySeconds);
                }
            }

            var spent = action.Charges > 0 && project.Rewarded && project.Charges <= 0;
            if (project.ExpiresAt > now && !spent)
                continue;

            // It ended, or its last charge is spent.
            if (action.ReturnPercent > 0 && project.Rewarded)
            {
                var back = project.Paid * action.ReturnPercent / 100f;
                comp.FactionIncome[project.Faction] = comp.FactionIncome.GetValueOrDefault(project.Faction) + back;
            }

            comp.CityProjects.RemoveAt(i);
        }

        // The troubles that are over.
        foreach (var (cityId, crisis) in comp.CityCrises.Where(pair => pair.Value.EndsAt <= now).ToList())
        {
            comp.CityCrises.Remove(cityId);

            var penalty = _proto.TryIndex<WorldCrisisPrototype>(crisis.Id, out var proto) ? proto.Penalty : 5;
            foreach (var faction in comp.CityInfluence.Keys.ToList())
            {
                if (InfluenceOf(market, faction, cityId) > 0)
                    AddCityInfluence(market, faction, cityId, -penalty);
            }

        }

        if (comp.NextCityCrisis == TimeSpan.Zero)
            comp.NextCityCrisis = now + FirstCityCrisis;

        if (now < comp.NextCityCrisis)
            return;

        comp.NextCityCrisis = now + TimeSpan.FromSeconds(_random.Next(90 * RareFactor, 181 * RareFactor));
        NewCityCrisis(market, now);
    }

    // A city falls into trouble, unless something keeps it off.
    private void NewCityCrisis(Entity<WorldMarketComponent> market, TimeSpan now)
    {
        var cities = ActiveCities(market).Where(c => !market.Comp.CityCrises.ContainsKey(c.ID)).ToList();
        var crises = _proto.EnumeratePrototypes<WorldCrisisPrototype>().ToList();
        if (cities.Count == 0 || crises.Count == 0)
            return;

        var city = _random.Pick(cities);

        // One of the troubles, by how often it happens.
        var season = SeasonOf(market);
        var roll = _random.NextFloat() * crises.Sum(c => c.Weight * SeasonCrisisWeight(season, c.ID));
        var crisis = crises[^1];
        foreach (var candidate in crises)
        {
            roll -= candidate.Weight * SeasonCrisisWeight(season, candidate.ID);
            if (roll > 0f)
                continue;

            crisis = candidate;
            break;
        }


        if (Protector(market, city.ID, crisis.ID, now, false) is { } guard)
        {
            AddCityInfluence(market, guard.Faction, city.ID, 3);
            StatsOf(market, guard.Faction).Crises++;
            return;
        }

        if (Protector(market, city.ID, crisis.ID, now, true) is { } store && _proto.TryIndex<WorldCityActionPrototype>(store.Action, out var storeAction))
        {
            store.Charges--;
            StatsOf(market, store.Faction).Crises++;
            AddCityInfluence(market, store.Faction, city.ID, storeAction.AbsorbInfluence);
            return;
        }

        market.Comp.CityCrises[city.ID] = new CityCrisis
        {
            Id = crisis.ID,
            StartedAt = now,
            EndsAt = now + TimeSpan.FromSeconds(crisis.DurationSeconds),
        };

    }

    // What the console of a faction is told about a city: its trouble, what the faction has going there, what it can order.
    private (string Name, string Description, int Seconds, bool Repaired, List<ProjectEntry> Projects, List<ActionEntry> Actions) CityStatus(
        Entity<WorldMarketComponent> market, string faction, string cityId, float budget)
    {
        var now = _timing.CurTime;

        var name = string.Empty;
        var description = string.Empty;
        var seconds = 0;
        if (market.Comp.CityCrises.TryGetValue(cityId, out var crisis) && _proto.TryIndex<WorldCrisisPrototype>(crisis.Id, out var crisisProto))
        {
            name = Loc.GetString(crisisProto.Name);
            description = Loc.GetString(crisisProto.Description);
            seconds = Math.Max(0, (int) Math.Ceiling((crisis.EndsAt - now).TotalSeconds));
        }

        var repaired = false;
        var projects = new List<ProjectEntry>();
        foreach (var project in market.Comp.CityProjects)
        {
            if (project.City != cityId || !_proto.TryIndex<WorldCityActionPrototype>(project.Action, out var action))
                continue;

            if (project.Rewarded && action.RepairsRoads && project.ExpiresAt > now)
                repaired = true;

            if (project.Faction != faction)
                continue;

            var ready = Math.Max(0, (int) Math.Ceiling((project.ReadyAt - now).TotalSeconds));
            var left = project.ExpiresAt == TimeSpan.MaxValue ? -1 : Math.Max(0, (int) Math.Ceiling((project.ExpiresAt - now).TotalSeconds));
            projects.Add(new ProjectEntry(Loc.GetString(action.Name), ready, left, project.Charges));
        }

        var actions = new List<ActionEntry>();
        foreach (var action in _proto.EnumeratePrototypes<WorldCityActionPrototype>().Where(a => !a.Joint && FitsPlace(cityId, a)).OrderBy(a => a.MinInfluence).ThenBy(a => a.Order).ThenBy(a => a.Cost))
        {
            actions.Add(new ActionEntry(action.ID, Loc.GetString(action.Name), Loc.GetString(action.Description), CityPrice(market, faction, cityId, action.Cost, out _, out _), action.Influence,
                ActionBlocked(market, faction, cityId, action, budget) ?? string.Empty, action.PointCost));
        }

        return (name, description, seconds, repaired, projects, actions);
    }
}
