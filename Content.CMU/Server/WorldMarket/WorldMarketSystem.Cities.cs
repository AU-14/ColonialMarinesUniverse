// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Shared.CMU14.WorldMarket;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.WorldMarket;

// The cities of the world map. They are not the same in every round: of the whole list of cities a few are taken at random, the land is made at random, and the cities are put on it at random
// (see WorldMapTerrain); the roads are made of where they stand. A faction wins the favour of a city with gifts and with what it does for it, and what the favour does is the capability of the
// city: for now only the trade hub has an effect (it takes a part off the duty), and the rest are for trying the map out.
public sealed partial class WorldMarketSystem
{
    public const int MaxInfluence = 100;
    public const int GiftInfluence = 12;

    // How many cities a round has: at least, and how many more there may be (the map is three times as big as it was, and so is what is on it), and how many of the other objects of the
    // map (the sites) there are besides.
    private const int FewestCities = 13;
    private const int MoreCities = 5;
    private const int FewestSites = 9;
    private const int MoreSites = 4;

    // The favour that takes one percent off the duty in a trade hub, and the most that all the hubs together take off.
    private const int HubInfluencePerPercent = 25;
    private const int HubMaxDutyCut = 5;

    // The favour of a faction in a city.
    public int InfluenceOf(Entity<WorldMarketComponent> market, string faction, string city)
    {
        return market.Comp.CityInfluence.GetValueOrDefault(faction)?.GetValueOrDefault(city) ?? 0;
    }

    // The favour that counts for the market as a whole: the greatest that any faction has in the city.
    private int LeadingInfluence(Entity<WorldMarketComponent> market, string city)
    {
        var leading = 0;
        foreach (var faction in market.Comp.CityInfluence.Values)
            leading = Math.Max(leading, faction.GetValueOrDefault(city));

        return leading;
    }

    // The duty that the trade hubs of the map take off: a percent for every twenty-five of favour, five at the most in all of them.
    private int CityDutyCut(Entity<WorldMarketComponent> market)
    {
        var cut = 0;
        foreach (var city in ActiveCities(market))
        {
            if (city.Capabilities.Contains(CityCapability.TradeHub))
                cut += LeadingInfluence(market, city.ID) / HubInfluencePerPercent;
        }

        return Math.Min(HubMaxDutyCut, cut);
    }

    // The cities of the round, with the land that they stand on, and the roads between them: kept until the number of the map is another.
    private List<WorldCityPrototype> _active = new();
    private int _activeSeed;
    private WorldMapTerrain? _terrain;
    private int _terrainSeed;
    private Dictionary<string, (float Latitude, float Longitude)> _places = new();
    private readonly List<(string From, string To, WorldRoadKind Kind)> _roadLinks = new();

    // The cities of the round: of all the cities that there are, a few at random (and always one that is a trade hub).
    private List<WorldCityPrototype> ActiveCities(Entity<WorldMarketComponent> market)
    {
        if (market.Comp.MapSeed == 0)
            market.Comp.MapSeed = _random.Next(1, int.MaxValue);

        var seed = market.Comp.MapSeed;
        if (_activeSeed == seed && _active.Count > 0)
            return _active;

        var all = _proto.EnumeratePrototypes<WorldCityPrototype>().OrderBy(c => c.ID, StringComparer.Ordinal).ToList();
        var pool = all.Where(c => !c.Site).ToList();
        var sitePool = all.Where(c => c.Site).ToList();
        var rng = new System.Random(seed);

        // Mixed, and the first ones taken.
        for (var i = pool.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        var count = Math.Min(pool.Count, FewestCities + rng.Next(MoreCities + 1));
        var chosen = pool.Take(count).ToList();

        if (chosen.Count > 0 && !chosen.Any(c => c.Capabilities.Contains(CityCapability.TradeHub)) && pool.Skip(count).FirstOrDefault(c => c.Capabilities.Contains(CityCapability.TradeHub)) is { } hub)
            chosen[^1] = hub;

        // The other objects of the map, taken the same way.
        for (var i = sitePool.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (sitePool[i], sitePool[j]) = (sitePool[j], sitePool[i]);
        }

        var sites = sitePool.Take(Math.Min(sitePool.Count, FewestSites + rng.Next(MoreSites + 1))).ToList();

        // The cities first (the roads are made of them), then the sites; each in the order of the names.
        _active = chosen.OrderBy(c => Loc.GetString(c.Name)).Concat(sites.OrderBy(c => Loc.GetString(c.Name))).ToList();
        _activeSeed = seed;
        _terrain = null;
        return _active;
    }

    // Makes the land, puts the cities of the round on it, and makes the roads between them (once for a number of the map).
    private void EnsureTerrain(Entity<WorldMarketComponent> market)
    {
        var cities = ActiveCities(market);
        var seed = market.Comp.MapSeed;
        if (_terrain != null && _terrainSeed == seed)
            return;

        _terrain = new WorldMapTerrain(seed);
        _terrainSeed = seed;
        _places = _terrain.PlaceCities(cities.Select(c => (c.ID, c.Coastal)).ToList());
        MakeRoadLinks(cities);
    }

    // The roads: every city is joined to the others (each to the nearest of those that are already joined), and a few more between the near ones. A road over land is a road (or a trail, for the
    // extra ones), one that goes over water is a sea route.
    private void MakeRoadLinks(List<WorldCityPrototype> cities)
    {
        _roadLinks.Clear();
        _roadPoints.Clear();
        if (_terrain == null || cities.Count < 2)
            return;

        var points = cities.Select(c => WorldMapTerrain.ToVector(_places[c.ID].Latitude, _places[c.ID].Longitude)).ToArray();
        var joined = new HashSet<int> { 0 };
        var pairs = new HashSet<(int, int)>();

        // The cities come first in the list, then the sites: the roads join the cities to one another, and each site is joined to the nearest city by a trail.
        var cityCount = cities.Count(c => !c.Site);

        void Link(int a, int b, WorldRoadKind intended)
        {
            pairs.Add((Math.Min(a, b), Math.Max(a, b)));
            _roadLinks.Add((cities[a].ID, cities[b].ID, _terrain!.KindOfRoad(intended, points[a], points[b])));
            _roadPoints.Add(SampleRoad(points[a], points[b]));
        }

        while (joined.Count < cityCount)
        {
            var bestA = -1;
            var bestB = -1;
            var best = float.MaxValue;

            foreach (var a in joined)
            {
                for (var b = 0; b < cityCount; b++)
                {
                    if (joined.Contains(b))
                        continue;

                    var distance = WorldMapTerrain.AngleBetween(points[a], points[b]);
                    if (distance >= best)
                        continue;

                    best = distance;
                    bestA = a;
                    bestB = b;
                }
            }

            joined.Add(bestB);
            Link(bestA, bestB, WorldRoadKind.Road);
        }

        // A few more, between the nearest of those that are not joined yet.
        var extra = new List<(float Distance, int A, int B)>();
        for (var a = 0; a < cityCount; a++)
        {
            for (var b = a + 1; b < cityCount; b++)
            {
                if (!pairs.Contains((a, b)))
                    extra.Add((WorldMapTerrain.AngleBetween(points[a], points[b]), a, b));
            }
        }

        foreach (var (_, a, b) in extra.OrderBy(e => e.Distance).Take(Math.Max(1, cityCount / 3)))
            Link(a, b, WorldRoadKind.Trail);

        // Every site has a trail to the city that is nearest to it.
        for (var site = cityCount; site < cities.Count && cityCount > 0; site++)
        {
            var nearest = 0;
            var nearestAngle = float.MaxValue;
            for (var city = 0; city < cityCount; city++)
            {
                var angle = WorldMapTerrain.AngleBetween(points[site], points[city]);
                if (angle >= nearestAngle)
                    continue;

                nearestAngle = angle;
                nearest = city;
            }

            Link(nearest, site, WorldRoadKind.Trail);
        }
    }

    // The cities of the map for a console, with the favour of its faction in each and whether the budget of the console is enough for a gift (the price is higher in the region of interest of
    // another faction), and the roads between them. The number that the land is made from goes with them: the client makes the same land of it.
    public List<CityEntry> BuildCities(string faction, float budget, out int seed, out List<RoadEntry> roads)
    {
        var market = EnsureMarket();
        EnsureTerrain(market);

        seed = market.Comp.MapSeed;

        var cities = new List<CityEntry>();
        foreach (var city in _active)
        {
            var influence = InfluenceOf(market, faction, city.ID);
            var capabilities = city.Capabilities.Select(c => Loc.GetString("rumc-city-capability-" + c.ToString().ToLowerInvariant())).ToList();
            var (latitude, longitude) = _places.GetValueOrDefault(city.ID);
            var status = CityStatus(market, faction, city.ID, budget);
            var owner = ZoneOwnerOf(market, city.ID, faction);
            var gift = CityPrice(market, faction, city.ID, city.GiftCost, out _, out _);

            cities.Add(new CityEntry(
                city.ID,
                Loc.GetString(city.Name),
                Loc.GetString(city.Description),
                latitude,
                longitude,
                city.Color,
                influence,
                capabilities,
                gift,
                budget >= gift && influence < MaxInfluence && RouteOk(market, faction, city.ID),
                status.Name,
                status.Description,
                status.Seconds,
                status.Repaired,
                status.Projects,
                status.Actions,
                owner ?? string.Empty,
                BuildCityExtras(market, faction, city.ID, budget),
                city.Site));
        }

        roads = new List<RoadEntry>();
        for (var index = 0; index < _roadLinks.Count; index++)
        {
            var (fromId, toId, kind) = _roadLinks[index];
            var blocked = index < _blockedBy.Length ? _blockedBy[index] ?? string.Empty : string.Empty;
            var fromName = cities.First(c => c.Id == fromId).Name;
            var toName = cities.First(c => c.Id == toId).Name;
            var descriptionKey = kind switch
            {
                WorldRoadKind.Road => "rumc-map-road-desc-road",
                WorldRoadKind.Trail => "rumc-map-road-desc-trail",
                _ => "rumc-map-road-desc-searoute",
            };

            roads.Add(new RoadEntry($"{fromId}>{toId}", fromId, toId,
                Loc.GetString("rumc-map-road-name", ("from", fromName), ("to", toName)),
                Loc.GetString(descriptionKey, ("from", fromName), ("to", toName)), kind, blocked));
        }

        return cities;
    }

    // A gift to a city: the cost is taken by the caller (the budget of the console that gives it), here the favour grows, up to the most. Gives how much the gift cost (with the extra of a
    // region of interest, which goes to its owner), or nothing if it cannot be given.
    public int? GiftCity(string faction, string cityId, float budget)
    {
        if (!_proto.TryIndex<WorldCityPrototype>(cityId, out var city) || !TryMarket(out var market))
            return null;

        var price = CityPrice(market, faction, cityId, city.GiftCost, out var owner, out var surcharge);
        if (budget < price)
            return null;

        var influence = InfluenceOf(market, faction, cityId);
        if (influence >= MaxInfluence)
            return null;

        if (!RouteOk(market, faction, cityId))
            return null;

        if (owner != null && surcharge > 0)
            PayFaction(market, owner, surcharge);

        // The gift goes by the roads as a caravan, unless there is no city to send it from.
        var origin = CaravanOrigin(market, faction, cityId);
        var path = origin != null ? FindPath(market, origin, cityId) : null;
        if (path != null)
            StartCaravan(market, faction, path, true, string.Empty);
        else
            AddCityInfluence(market, faction, cityId, GiftInfluence);

        return price;
    }
}
