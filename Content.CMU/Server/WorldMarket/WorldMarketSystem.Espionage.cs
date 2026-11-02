// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Shared.CMU14.WorldMarket;
using Robust.Shared.Random;

namespace Content.Server.CMU14.WorldMarket;

// The intelligence of the ambassadors. A faction that has built the headquarters of the intelligence (one in the world, in a city or in another object of the map) can send scouts into a place
// to see what the others have there, and can do harm to the others in secret: discredit one in a city (it loses some of its share of the reputation), blow up one of its buildings, stir
// a trouble up in a city, and put counter-intelligence up against all of this. Everything is asked through MapCommand. The harm is done in secret: a failed attempt, or one that a faction
// with counter-intelligence on has caught, is known to the victim, who gets the right to answer; a harm to a faction that there is a pact with breaks the pact.
public sealed partial class WorldMarketSystem
{
    public const int ScoutCost = 250;
    public const int SmearCost = 450;
    public const int SabotageCost = 900;
    public const int ProvokeCost = 800;
    public const int CounterCost = 700;

    private static readonly TimeSpan ScoutTime = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan ScoutValid = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan CounterTime = TimeSpan.FromMinutes(6);

    // The favour that a faction must have in a place to stir a trouble up in it.
    private const int ProvokeLeastInfluence = 10;

    #region The headquarters

    // Does the faction have a headquarters of the intelligence that is built and stands?
    public bool HasHq(Entity<WorldMarketComponent> market, string faction)
    {
        var now = _timing.CurTime;
        return market.Comp.CityProjects.Any(project => project.Faction == faction && project.Rewarded && project.ExpiresAt > now
                                                       && _proto.TryIndex<WorldCityActionPrototype>(project.Action, out var action) && action.Feature == WorldBuildingFeature.IntelHq);
    }

    // Does the faction already have one of a building that it may have only one of (standing, being built, or on the road to the city)?
    private bool HasUnique(Entity<WorldMarketComponent> market, string faction, string actionId, TimeSpan now)
    {
        return market.Comp.CityProjects.Any(project => project.Faction == faction && project.Action == actionId && project.ExpiresAt > now)
               || market.Comp.Caravans.Any(caravan => caravan.Faction == faction && !caravan.Gift && caravan.Action == actionId);
    }

    private bool CounterOn(Entity<WorldMarketComponent> market, string faction)
    {
        return market.Comp.CounterUntil.TryGetValue(faction, out var until) && until > _timing.CurTime;
    }

    #endregion

    #region Scouts

    private static string ScoutKey(string faction, string cityId) => faction + "|" + cityId;

    // Have the scouts of the faction come back from the place, and is their report still good?
    private bool Scouted(Entity<WorldMarketComponent> market, string faction, string cityId)
    {
        var now = _timing.CurTime;
        return market.Comp.Scouts.TryGetValue(ScoutKey(faction, cityId), out var report) && report.ReadyAt <= now && report.Until > now;
    }

    private string? ScoutBlocked(Entity<WorldMarketComponent> market, string faction, string cityId, float budget)
    {
        if (!HasHq(market, faction))
            return Loc.GetString("rumc-map-hq-needed");

        if (market.Comp.Scouts.TryGetValue(ScoutKey(faction, cityId), out var report) && report.Until > _timing.CurTime)
            return Loc.GetString("rumc-map-scouts-busy");

        if (budget < ScoutCost)
            return Loc.GetString("rumc-map-need-budget");

        return null;
    }

    private int? Scouts(Entity<WorldMarketComponent> market, string faction, string cityId, float budget)
    {
        if (ActiveCities(market).All(c => c.ID != cityId) || ScoutBlocked(market, faction, cityId, budget) != null)
            return null;

        var now = _timing.CurTime;
        market.Comp.Scouts[ScoutKey(faction, cityId)] = new ScoutReport { ReadyAt = now + ScoutTime, Until = now + ScoutTime + ScoutValid };
        return ScoutCost;
    }

    // What the scouts have found out about the others in a place: what each has built there, and whether it is covered by counter-intelligence.
    private List<string> ScoutLines(Entity<WorldMarketComponent> market, string faction, string cityId)
    {
        var lines = new List<string>();
        var now = _timing.CurTime;

        foreach (var other in market.Comp.KnownFactions.Where(f => f != faction).OrderBy(f => f, StringComparer.Ordinal))
        {
            var influence = InfluenceOf(market, other, cityId);

            // A faction that feeds false reports to the scouts of the others gives them made-up numbers and buildings.
            if (DisinfoOn(market, other))
            {
                var (fakeInfluence, fakeBuildings) = FalseReport(other, cityId);
                lines.Add(Loc.GetString("rumc-map-scouts-line", ("faction", other), ("influence", fakeInfluence), ("buildings", fakeBuildings.Length > 0 ? fakeBuildings : Loc.GetString("rumc-map-scouts-nothing"))));
                continue;
            }

            var buildings = market.Comp.CityProjects
                .Where(p => p.Faction == other && p.City == cityId && p.ExpiresAt > now && _proto.HasIndex<WorldCityActionPrototype>(p.Action))
                .GroupBy(p => p.Action)
                .Select(g => Loc.GetString(_proto.Index<WorldCityActionPrototype>(g.Key).Name) + (g.Count() > 1 ? $" ×{g.Count()}" : string.Empty))
                .ToList();

            if (influence <= 0 && buildings.Count == 0)
                continue;

            var text = buildings.Count > 0 ? string.Join(", ", buildings) : Loc.GetString("rumc-map-scouts-nothing");
            lines.Add(Loc.GetString(CounterOn(market, other) ? "rumc-map-scouts-line-covered" : "rumc-map-scouts-line", ("faction", other), ("influence", influence), ("buildings", text)));
        }

        return lines;
    }

    #endregion

    #region Harm in secret

    // The chance that a harm succeeds, in percent: the scouts' report helps, the counter-intelligence of the target hinders, an answer to a harm is likelier.
    private int HarmChance(Entity<WorldMarketComponent> market, string faction, string cityId, string target, int baseChance)
    {
        var chance = baseChance;
        if (Scouted(market, faction, cityId))
            chance += 15;

        if (CounterOn(market, target))
            chance -= 25;

        if (HasRetaliation(market, faction, target))
            chance += 20;

        // The partners of a joint work in the city guard one another.
        chance -= JointGuard(market, target, cityId);

        return Math.Clamp(chance, 10, 95);
    }

    private string? HarmBlocked(Entity<WorldMarketComponent> market, string faction, string cityId, string target, int cost, float budget)
    {
        if (!HasHq(market, faction))
            return Loc.GetString("rumc-map-hq-needed");

        var now = _timing.CurTime;
        if (market.Comp.StrikeReady.TryGetValue(faction, out var ready) && ready > now)
            return Loc.GetString("rumc-map-strike-wait", ("time", (int) Math.Ceiling((ready - now).TotalSeconds)));

        if (budget < cost)
            return Loc.GetString("rumc-map-need-budget");

        if (InfluenceOf(market, target, cityId) <= 0)
            return Loc.GetString("rumc-map-strike-nothing");

        return null;
    }

    private bool HarmAllowed(Entity<WorldMarketComponent> market, string faction, string cityId, string target)
    {
        return target != faction && market.Comp.KnownFactions.Contains(target) && ActiveCities(market).Any(c => c.ID == cityId);
    }

    // The start of a harm: the cooldown, and the pact (a harm to a faction that there is a pact with breaks it, unless it is an answer to a harm).
    private void BeginHarm(Entity<WorldMarketComponent> market, string faction, string target)
    {
        var comp = market.Comp;
        comp.StrikeReady[faction] = _timing.CurTime + StrikeCooldown;

        if (HasRetaliation(market, faction, target))
            comp.Retaliations.RemoveAll(r => r.Victim == faction && r.Offender == target);
        else if (InPact(market, faction, target))
            Violate(market, faction, target);
    }

    // The harm was found out: the faction loses the trust of the mother faction, and the victim has the right to answer.
    private void Expose(Entity<WorldMarketComponent> market, string faction, string target, bool failed)
    {
        if (failed)
            AddTrust(market, faction, -8);

        market.Comp.Retaliations.RemoveAll(r => r.Victim == target && r.Offender == faction);
        market.Comp.Retaliations.Add(new Retaliation { Victim = target, Offender = faction, Until = _timing.CurTime + RetaliationTime });
    }

    // Discredit: the target loses some of its share of the reputation of the city. It always works unless the target has counter-intelligence on.
    private int? Smear(Entity<WorldMarketComponent> market, string faction, string cityId, string target, float budget)
    {
        if (!HarmAllowed(market, faction, cityId, target) || HarmBlocked(market, faction, cityId, target, SmearCost, budget) != null)
            return null;

        var chance = HarmChance(market, faction, cityId, target, 100);
        BeginHarm(market, faction, target);
        LogHarm(market, faction, target, cityId, "rumc-map-harm-smear");

        if (_random.Prob(chance / 100f))
        {
            AddCityInfluence(market, target, cityId, -_random.Next(6, 13));

            // A faction that is watching sees who did it.
            if (CounterOn(market, target))
                Expose(market, faction, target, false);
        }
        else
        {
            Expose(market, faction, target, true);
        }

        return SmearCost;
    }

    // Sabotage: one of the buildings of the target in the city is blown up (not the great ones).
    private List<CityProject> SabotageTargets(Entity<WorldMarketComponent> market, string cityId, string target)
    {
        var now = _timing.CurTime;
        return market.Comp.CityProjects
            .Where(p => p.Faction == target && p.City == cityId && p.ExpiresAt > now && _proto.TryIndex<WorldCityActionPrototype>(p.Action, out var action) && action.PointEverySeconds == 0)
            .ToList();
    }

    private string? SabotageBlocked(Entity<WorldMarketComponent> market, string faction, string cityId, string target, float budget)
    {
        if (HarmBlocked(market, faction, cityId, target, SabotageCost, budget) is { } blocked)
            return blocked;

        return SabotageTargets(market, cityId, target).Count == 0 ? Loc.GetString("rumc-map-sabotage-nothing") : null;
    }

    private int? Sabotage(Entity<WorldMarketComponent> market, string faction, string cityId, string target, float budget)
    {
        if (!HarmAllowed(market, faction, cityId, target) || SabotageBlocked(market, faction, cityId, target, budget) != null)
            return null;

        var chance = HarmChance(market, faction, cityId, target, 55);
        BeginHarm(market, faction, target);
        LogHarm(market, faction, target, cityId, "rumc-map-harm-sabotage");

        if (_random.Prob(chance / 100f))
        {
            market.Comp.CityProjects.Remove(_random.Pick(SabotageTargets(market, cityId, target)));
            AddCityInfluence(market, target, cityId, -4);

            if (CounterOn(market, target))
                Expose(market, faction, target, false);
        }
        else
        {
            Expose(market, faction, target, true);
        }

        return SabotageCost;
    }

    // Provocation: a trouble is stirred up in a city where the faction has a foothold. Nobody knows who did it; every faction in the city loses favour when it ends, unless someone ends it.
    private string? ProvokeBlocked(Entity<WorldMarketComponent> market, string faction, string cityId, float budget)
    {
        if (!HasHq(market, faction))
            return Loc.GetString("rumc-map-hq-needed");

        var now = _timing.CurTime;
        if (market.Comp.StrikeReady.TryGetValue(faction, out var ready) && ready > now)
            return Loc.GetString("rumc-map-strike-wait", ("time", (int) Math.Ceiling((ready - now).TotalSeconds)));

        if (market.Comp.CityCrises.ContainsKey(cityId))
            return Loc.GetString("rumc-map-provoke-already");

        if (InfluenceOf(market, faction, cityId) < ProvokeLeastInfluence)
            return Loc.GetString("rumc-map-need-influence", ("value", ProvokeLeastInfluence));

        if (budget < ProvokeCost)
            return Loc.GetString("rumc-map-need-budget");

        return null;
    }

    private int? Provoke(Entity<WorldMarketComponent> market, string faction, string cityId, float budget)
    {
        if (ActiveCities(market).All(c => c.ID != cityId) || ProvokeBlocked(market, faction, cityId, budget) != null)
            return null;

        var now = _timing.CurTime;
        market.Comp.StrikeReady[faction] = now + StrikeCooldown;
        LogHarm(market, faction, string.Empty, cityId, "rumc-map-harm-provoke");

        var crises = _proto.EnumeratePrototypes<WorldCrisisPrototype>().Where(c => Protector(market, cityId, c.ID, now, false) == null).ToList();
        if (crises.Count > 0)
        {
            var crisis = _random.Pick(crises);
            market.Comp.CityCrises[cityId] = new CityCrisis { Id = crisis.ID, StartedAt = now, EndsAt = now + TimeSpan.FromSeconds(crisis.DurationSeconds) };
        }

        // If the city is guarded against every trouble there is, the money is lost for nothing.
        return ProvokeCost;
    }

    #endregion

    #region Counter-intelligence

    private string? CounterBlocked(Entity<WorldMarketComponent> market, string faction, float budget)
    {
        if (!HasHq(market, faction))
            return Loc.GetString("rumc-map-hq-needed");

        if (CounterOn(market, faction))
            return Loc.GetString("rumc-map-counter-on");

        return budget < CounterCost ? Loc.GetString("rumc-map-need-budget") : null;
    }

    private int? Counter(Entity<WorldMarketComponent> market, string faction, float budget)
    {
        if (CounterBlocked(market, faction, budget) != null)
            return null;

        market.Comp.CounterUntil[faction] = _timing.CurTime + CounterTime;
        return CounterCost;
    }

    public int CounterSecondsLeft(Entity<WorldMarketComponent> market, string faction)
    {
        return market.Comp.CounterUntil.TryGetValue(faction, out var until) ? Math.Max(0, (int) Math.Ceiling((until - _timing.CurTime).TotalSeconds)) : 0;
    }

    #endregion

    #region What the console is told

    // The intelligence section of a place, for a faction that has the headquarters.
    private void FillEspionage(Entity<WorldMarketComponent> market, string faction, string cityId, float budget, CityExtras extras)
    {
        if (!HasHq(market, faction))
            return;

        var now = _timing.CurTime;
        extras.Hq = true;
        extras.ScoutCost = ScoutCost;
        extras.ScoutBlocked = ScoutBlocked(market, faction, cityId, budget) ?? string.Empty;

        if (market.Comp.Scouts.TryGetValue(ScoutKey(faction, cityId), out var report) && report.Until > now)
        {
            if (report.ReadyAt > now)
                extras.ScoutSeconds = (int) Math.Ceiling((report.ReadyAt - now).TotalSeconds);
            else
                extras.ScoutReport = ScoutLines(market, faction, cityId);
        }

        extras.ProvokeCost = ProvokeCost;
        extras.ProvokeBlocked = ProvokeBlocked(market, faction, cityId, budget) ?? string.Empty;

        foreach (var target in market.Comp.KnownFactions.Where(f => f != faction).OrderBy(f => f, StringComparer.Ordinal))
        {
            if (InfluenceOf(market, target, cityId) <= 0)
                continue;

            var answer = HasRetaliation(market, faction, target);
            extras.Smears.Add(new StrikeEntry(target, SmearCost, HarmChance(market, faction, cityId, target, 100), answer, HarmBlocked(market, faction, cityId, target, SmearCost, budget) ?? string.Empty));
            extras.Sabotages.Add(new StrikeEntry(target, SabotageCost, HarmChance(market, faction, cityId, target, 55), answer, SabotageBlocked(market, faction, cityId, target, budget) ?? string.Empty));
        }

        FillSabotage(market, faction, cityId, budget, extras);
    }

    #endregion

    // Once a second: reports that are old are dropped, counter-intelligence that has run out is dropped.
    private void UpdateEspionage(Entity<WorldMarketComponent> market, TimeSpan now)
    {
        var comp = market.Comp;
        foreach (var key in comp.Scouts.Where(pair => pair.Value.Until <= now).Select(pair => pair.Key).ToList())
            comp.Scouts.Remove(key);

        foreach (var faction in comp.CounterUntil.Where(pair => pair.Value <= now).Select(pair => pair.Key).ToList())
            comp.CounterUntil.Remove(faction);
    }
}
