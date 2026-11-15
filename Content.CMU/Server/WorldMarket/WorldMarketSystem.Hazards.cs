// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.WorldMarket;
using Robust.Shared.Random;

namespace Content.Server.CMU14.WorldMarket;

// What happens on the map besides the troubles of the cities. The seasons go round (summer makes a drought likelier, winter takes a quarter off what the work brings in, and so on). Storms
// come on the sea routes and threats (raids, plagues, fires) on the roads and the lands: each is a circle that closes the roads through it and cuts what the work in the cities inside it brings
// in; a threat can be sent away for money (a private military company is hired), a storm cannot. Good things happen in the cities too (a holiday, a fair, a visit), and the factions pay to
// support them, or spoil them.
public sealed partial class WorldMarketSystem
{
    private const int SeasonSeconds = 720;
    private const int MaxHazards = 6;
    private const int MostHazardDegrees = 8;

    private static readonly TimeSpan FirstHazard = TimeSpan.FromMinutes(2 * RareFactor);
    private static readonly TimeSpan FirstCityEvent = TimeSpan.FromMinutes(2.5);

    // What it costs to spoil a good thing of a city, and how much of what it gives each spoiling takes (in percent).
    public const int EventSpoilCost = 300;
    private const int EventSpoilPercent = 30;

    private const int EventLeastSupport = 100;

    // What the work in the cities brings in now (by the name of the city): the season, the hazards, the roads that are shut. Found once a second.
    private readonly Dictionary<string, float> _incomeFactors = new();

    // The points along each road (filled when the roads are made), and why each is shut now (nothing if it is open).
    private readonly List<Vector3[]> _roadPoints = new();
    private string[] _blockedBy = Array.Empty<string>();

    private float IncomeFactorOf(string cityId) => _incomeFactors.GetValueOrDefault(cityId, 1f);

    private static Vector3[] SampleRoad(Vector3 a, Vector3 b)
    {
        var angle = MathF.Acos(Math.Clamp(Vector3.Dot(a, b), -1f, 1f));
        var steps = Math.Max(2, (int) MathF.Ceiling(angle / (3f * MathF.PI / 180f)));
        var sin = MathF.Sin(angle);
        var points = new Vector3[steps + 1];

        for (var i = 0; i <= steps; i++)
        {
            var t = i / (float) steps;
            points[i] = sin < 0.0001f ? a : (MathF.Sin((1f - t) * angle) * a + MathF.Sin(t * angle) * b) / sin;
        }

        return points;
    }

    private static (float Latitude, float Longitude) ToLatLon(Vector3 point)
    {
        const float degrees = 180f / MathF.PI;
        return (MathF.Asin(Math.Clamp(point.Y, -1f, 1f)) * degrees, MathF.Atan2(point.X, point.Z) * degrees);
    }

    #region Seasons

    // 0 spring, 1 summer, 2 autumn, 3 winter; it changes every twelve minutes, and the round does not always begin with spring.
    public int SeasonOf(Entity<WorldMarketComponent> market)
    {
        return ((int) (_timing.CurTime.TotalSeconds / SeasonSeconds) + (market.Comp.MapSeed & 3)) % 4;
    }

    private static float SeasonIncome(int season) => season == 3 ? 0.75f : 1f;

    // Floods in spring, droughts in summer, fires in autumn, plagues in winter.
    private static float SeasonCrisisWeight(int season, string crisisId)
    {
        return (season, crisisId) switch
        {
            (0, "RUMCCrisisFlood") => 2f,
            (1, "RUMCCrisisDrought") => 2.5f,
            (2, "RUMCCrisisFire") => 1.5f,
            (3, "RUMCCrisisEpidemic") => 1.5f,
            _ => 1f,
        };
    }

    #endregion

    #region Hazards

    private (WorldCityPrototype? City, float Angle) NearestCity(Entity<WorldMarketComponent> market, Vector3 point)
    {
        WorldCityPrototype? nearest = null;
        var best = float.MaxValue;

        foreach (var city in ActiveCities(market))
        {
            if (!_places.TryGetValue(city.ID, out var place))
                continue;

            var angle = WorldMapTerrain.AngleBetween(point, WorldMapTerrain.ToVector(place.Latitude, place.Longitude));
            if (angle >= best)
                continue;

            best = angle;
            nearest = city;
        }

        return (nearest, best);
    }

    private void SpawnHazard(Entity<WorldMarketComponent> market, TimeSpan now)
    {
        var comp = market.Comp;
        if (comp.Hazards.Count >= MaxHazards)
            return;

        EnsureTerrain(market);
        var protos = _proto.EnumeratePrototypes<WorldHazardPrototype>().ToList();
        if (protos.Count == 0 || _roadLinks.Count == 0)
            return;

        // One of them, by how often it happens (storms more in autumn).
        var autumn = SeasonOf(market) == 2;
        float WeightOf(WorldHazardPrototype p) => p.Weight * (p.Weather && autumn ? 2f : 1f);
        var roll = _random.NextFloat() * protos.Sum(WeightOf);
        var proto = protos[^1];
        foreach (var candidate in protos)
        {
            roll -= WeightOf(candidate);
            if (roll > 0f)
                continue;

            proto = candidate;
            break;
        }

        // Weather on a sea route; a threat mostly on a road, now and then on the land by a city.
        var roads = Enumerable.Range(0, _roadLinks.Count)
            .Where(i => proto.Weather ? _roadLinks[i].Kind == WorldRoadKind.SeaRoute : _roadLinks[i].Kind != WorldRoadKind.SeaRoute)
            .ToList();

        Vector3 place;
        if (roads.Count > 0 && (proto.Weather || _random.Prob(0.7f)))
        {
            var points = _roadPoints[_random.Pick(roads)];
            place = points[_random.Next(points.Length)];
        }
        else if (proto.Weather)
        {
            return;
        }
        else
        {
            var city = _random.Pick(ActiveCities(market));
            var (latitude, longitude) = _places.GetValueOrDefault(city.ID);
            place = WorldMapTerrain.ToVector(
                Math.Clamp(latitude + _random.Next(-MostHazardDegrees, MostHazardDegrees + 1), -80f, 80f),
                longitude + _random.Next(-MostHazardDegrees, MostHazardDegrees + 1));
        }

        var (lat, lon) = ToLatLon(place);
        comp.Hazards.Add(new MapHazard { Id = ++comp.NextHazardId, Proto = proto.ID, Latitude = lat, Longitude = lon, EndsAt = now + TimeSpan.FromSeconds(proto.DurationSeconds) });
    }

    // A company is hired against a threat: the money is paid, the threat is gone, and the favour of the nearest city goes to the one who paid. Gives the cost, or nothing.
    private int? HazardRemove(Entity<WorldMarketComponent> market, string faction, string idText, float budget)
    {
        if (!int.TryParse(idText, out var id))
            return null;

        var hazard = market.Comp.Hazards.FirstOrDefault(h => h.Id == id);
        if (hazard == null || !_proto.TryIndex<WorldHazardPrototype>(hazard.Proto, out var proto) || proto.RemoveCost <= 0 || budget < proto.RemoveCost)
            return null;

        market.Comp.Hazards.Remove(hazard);
        StatsOf(market, faction).Crises++;

        var (near, angle) = NearestCity(market, WorldMapTerrain.ToVector(hazard.Latitude, hazard.Longitude));
        if (near != null && angle <= proto.Radius + 15f)
            AddCityInfluence(market, faction, near.ID, proto.RemoveInfluence);

        return proto.RemoveCost;
    }

    private void UpdateHazards(Entity<WorldMarketComponent> market, TimeSpan now)
    {
        var comp = market.Comp;

        foreach (var hazard in comp.Hazards.Where(h => h.EndsAt <= now).ToList())
        {
            comp.Hazards.Remove(hazard);
        }

        if (comp.NextHazard == TimeSpan.Zero)
            comp.NextHazard = now + FirstHazard;

        if (now >= comp.NextHazard)
        {
            comp.NextHazard = now + TimeSpan.FromSeconds(_random.Next(120 * RareFactor, 201 * RareFactor));
            SpawnHazard(market, now);
        }
    }

    // Once a second: which roads are shut, and what the work in each city brings in (the season, the hazards over the city, all its roads shut).
    private void RecomputeHazardEffects(Entity<WorldMarketComponent> market)
    {
        EnsureTerrain(market);

        var cities = ActiveCities(market);
        var hazards = new List<(MapHazard Hazard, WorldHazardPrototype Proto, Vector3 Point)>();
        foreach (var hazard in market.Comp.Hazards)
        {
            if (_proto.TryIndex<WorldHazardPrototype>(hazard.Proto, out var proto))
                hazards.Add((hazard, proto, WorldMapTerrain.ToVector(hazard.Latitude, hazard.Longitude)));
        }

        var blocked = new string[_roadLinks.Count];
        for (var i = 0; i < _roadLinks.Count && i < _roadPoints.Count; i++)
        {
            var sea = _roadLinks[i].Kind == WorldRoadKind.SeaRoute;
            foreach (var (_, proto, point) in hazards)
            {
                if (!(sea ? proto.ClosesSea : proto.ClosesLand))
                    continue;

                if (_roadPoints[i].Any(p => WorldMapTerrain.AngleBetween(p, point) <= proto.Radius))
                {
                    blocked[i] = Loc.GetString(proto.Name);
                    break;
                }
            }
        }

        _blockedBy = blocked;

        var season = SeasonIncome(SeasonOf(market));
        _incomeFactors.Clear();
        foreach (var city in cities)
        {
            if (!_places.TryGetValue(city.ID, out var place))
                continue;

            var here = WorldMapTerrain.ToVector(place.Latitude, place.Longitude);
            var factor = season;
            foreach (var (_, proto, point) in hazards)
            {
                if (WorldMapTerrain.AngleBetween(here, point) <= proto.Radius)
                    factor *= proto.IncomeFactor;
            }

            // A city that all its roads are shut to has half of it less.
            var roads = Enumerable.Range(0, _roadLinks.Count).Where(i => _roadLinks[i].From == city.ID || _roadLinks[i].To == city.ID).ToList();
            if (roads.Count > 0 && roads.All(i => !string.IsNullOrEmpty(blocked[i])))
                factor *= 0.5f;

            _incomeFactors[city.ID] = factor;
        }
    }

    #endregion

    #region Good things in the cities

    private int? EventSupport(Entity<WorldMarketComponent> market, string faction, string cityId, int amount, float budget)
    {
        if (!market.Comp.CityEvents.TryGetValue(cityId, out var cityEvent) || cityEvent.EndsAt <= _timing.CurTime || amount < EventLeastSupport || amount > 5000 || budget < amount)
            return null;

        cityEvent.Funding[faction] = cityEvent.Funding.GetValueOrDefault(faction) + amount;
        return amount;
    }

    // Spoiling it takes away a part of what it gives, and the one who did it is not named.
    private int? EventSpoil(Entity<WorldMarketComponent> market, string cityId, float budget)
    {
        if (!market.Comp.CityEvents.TryGetValue(cityId, out var cityEvent) || cityEvent.EndsAt <= _timing.CurTime || budget < EventSpoilCost)
            return null;

        cityEvent.Spoilers++;
        return EventSpoilCost;
    }

    // It ends: what it gives is shared between those who supported it by how much each paid, as much of it as the money reached, less for every one who spoiled it.
    private void ResolveEvent(Entity<WorldMarketComponent> market, string cityId, CityEvent cityEvent)
    {
        if (!_proto.TryIndex<WorldCityEventPrototype>(cityEvent.Id, out var proto))
            return;

        var total = cityEvent.Funding.Values.Sum();

        if (total <= 0)
        {
            return;
        }

        var scale = Math.Clamp(total / (float) proto.Goal, 0.3f, 1.2f) * Math.Max(0.2f, 1f - EventSpoilPercent / 100f * cityEvent.Spoilers);
        var pool = proto.Reward * scale;

        foreach (var (faction, paid) in cityEvent.Funding)
            AddCityInfluence(market, faction, cityId, (int) Math.Round(pool * paid / total));

    }

    private void UpdateCityEvents(Entity<WorldMarketComponent> market, TimeSpan now)
    {
        var comp = market.Comp;

        foreach (var (cityId, cityEvent) in comp.CityEvents.Where(pair => pair.Value.EndsAt <= now).ToList())
        {
            comp.CityEvents.Remove(cityId);
            ResolveEvent(market, cityId, cityEvent);
        }

        if (comp.NextCityEvent == TimeSpan.Zero)
            comp.NextCityEvent = now + FirstCityEvent;

        if (now < comp.NextCityEvent)
            return;

        comp.NextCityEvent = now + TimeSpan.FromSeconds(_random.Next(150, 271));

        var cities = ActiveCities(market).Where(c => !comp.CityEvents.ContainsKey(c.ID) && !comp.CityCrises.ContainsKey(c.ID)).ToList();
        var events = _proto.EnumeratePrototypes<WorldCityEventPrototype>().ToList();
        if (cities.Count == 0 || events.Count == 0)
            return;

        var roll = _random.NextFloat() * events.Sum(e => e.Weight);
        var proto = events[^1];
        foreach (var candidate in events)
        {
            roll -= candidate.Weight;
            if (roll > 0f)
                continue;

            proto = candidate;
            break;
        }

        var city = _random.Pick(cities);
        comp.CityEvents[city.ID] = new CityEvent { Id = proto.ID, EndsAt = now + TimeSpan.FromSeconds(proto.DurationSeconds) };
    }

    #endregion

    // Once a second.
    private void UpdateWorld(Entity<WorldMarketComponent> market, TimeSpan now)
    {
        UpdateDiplomacy(market, now);
        UpdateCityEvents(market, now);
        UpdateHazards(market, now);
        RecomputeHazardEffects(market);
        UpdateTrade(market, now);
        UpdateEspionage(market, now);
        UpdateSabotage(market, now);
    }

    // The things of the whole map for the console of a faction: the season, the hazards, the other factions, the joint works that are offered to it.
    public MapExtras BuildMapExtras(string faction, float budget)
    {
        var market = EnsureMarket();
        var now = _timing.CurTime;
        var extras = new MapExtras
        {
            Season = SeasonOf(market),
            SeasonSeconds = SeasonSeconds - (int) (now.TotalSeconds % SeasonSeconds),
            Factions = BuildFactionEntries(market, faction),
            JointOffers = BuildJointOfferEntries(market, faction, budget),
            Caravans = BuildCaravanEntries(market, faction, budget),
            Deals = BuildDealEntries(market, faction, budget),
            Dossiers = BuildDossiers(market, faction),
            IntelSeconds = IntelSecondsLeft(faction),
            IntelLevel = market.Comp.IntelLevel.GetValueOrDefault(faction),
            Hq = HasHq(market, faction),
            CounterCost = CounterCost,
            CounterSeconds = CounterSecondsLeft(market, faction),
            CounterBlocked = CounterBlocked(market, faction, budget) ?? string.Empty,
        };

        FillSabotageMap(market, faction, budget, extras);

        foreach (var hazard in market.Comp.Hazards.Where(h => h.EndsAt > now))
        {
            if (!_proto.TryIndex<WorldHazardPrototype>(hazard.Proto, out var proto))
                continue;

            extras.Hazards.Add(new HazardEntry(hazard.Id, Loc.GetString(proto.Name), Loc.GetString(proto.Description), hazard.Latitude, hazard.Longitude, proto.Radius,
                (int) Math.Ceiling((hazard.EndsAt - now).TotalSeconds), proto.Weather, proto.RemoveCost,
                proto.RemoveCost > 0 && budget < proto.RemoveCost ? Loc.GetString("rumc-map-need-budget") : string.Empty));
        }

        return extras;
    }
}
