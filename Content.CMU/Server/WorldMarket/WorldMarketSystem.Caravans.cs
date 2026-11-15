// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.WorldMarket;

namespace Content.Server.CMU14.WorldMarket;

// Gifts and the works that are ordered for a city go to it by the roads, as a caravan that is seen on the map. It sets out from the city where the faction has the most favour (a faction that
// has none yet, or an order against a trouble that is on, is not delayed) and goes by the nearest roads that are open. A threat on the way takes a caravan that has no escort; a storm holds it
// until it passes. The ambassadors cannot take each other's caravans: only the hazards of the map do. An escort can be paid for while the caravan is on the way. What the caravan carries comes
// into force when it arrives.
public sealed partial class WorldMarketSystem
{
    // How far a caravan goes in a second, in degrees of the ball; the favour that a city must be won to in order to be a place that a caravan sets out from; what an escort costs.
    private const float CaravanSpeed = 0.6f;
    private const int CaravanOriginInfluence = 12;
    public const int EscortCost = 300;

    // The city that a caravan to the target sets out from: where the faction has the most favour (at least CaravanOriginInfluence), not the target itself. Nothing if there is none.
    private string? CaravanOrigin(Entity<WorldMarketComponent> market, string faction, string target)
    {
        string? best = null;
        var most = CaravanOriginInfluence - 1;

        foreach (var city in ActiveCities(market))
        {
            if (city.ID == target)
                continue;

            var influence = InfluenceOf(market, faction, city.ID);
            if (influence <= most)
                continue;

            most = influence;
            best = city.ID;
        }

        return best;
    }

    // The nearest way from one city to another by the roads that are open (the cities in order), or nothing if there is none.
    private List<string>? FindPath(Entity<WorldMarketComponent> market, string from, string to)
    {
        EnsureTerrain(market);

        var distance = new Dictionary<string, float> { [from] = 0f };
        var previous = new Dictionary<string, string>();
        var done = new HashSet<string>();

        while (true)
        {
            string? current = null;
            var best = float.MaxValue;
            foreach (var (id, value) in distance)
            {
                if (!done.Contains(id) && value < best)
                {
                    best = value;
                    current = id;
                }
            }

            if (current == null)
                return null;

            if (current == to)
                break;

            done.Add(current);

            for (var i = 0; i < _roadLinks.Count; i++)
            {
                if (i < _blockedBy.Length && !string.IsNullOrEmpty(_blockedBy[i]))
                    continue;

                var link = _roadLinks[i];
                var other = link.From == current ? link.To : link.To == current ? link.From : null;
                if (other == null || done.Contains(other) || !_places.ContainsKey(current) || !_places.ContainsKey(other))
                    continue;

                var length = best + WorldMapTerrain.AngleBetween(PlaceVector(current), PlaceVector(other));
                if (distance.TryGetValue(other, out var known) && known <= length)
                    continue;

                distance[other] = length;
                previous[other] = current;
            }
        }

        var path = new List<string> { to };
        while (path[0] != from)
            path.Insert(0, previous[path[0]]);

        return path;
    }

    private Vector3 PlaceVector(string cityId)
    {
        var place = _places[cityId];
        return WorldMapTerrain.ToVector(place.Latitude, place.Longitude);
    }

    // Can something be sent to the city? Yes if there is no city to send it from, or there is an open way from one.
    private bool RouteOk(Entity<WorldMarketComponent> market, string faction, string target)
    {
        return CaravanOrigin(market, faction, target) is not { } origin || FindPath(market, origin, target) != null;
    }

    private string CaravanLabel(Caravan caravan)
    {
        if (caravan.Gift)
            return Loc.GetString("rumc-map-caravan-gift");

        return _proto.TryIndex<WorldCityActionPrototype>(caravan.Action, out var action) ? Loc.GetString(action.Name) : caravan.Action;
    }

    private void StartCaravan(Entity<WorldMarketComponent> market, string faction, List<string> path, bool gift, string action)
    {
        var start = _places[path[0]];
        var caravan = new Caravan
        {
            Id = ++market.Comp.NextCaravanId,
            Faction = faction,
            Gift = gift,
            Action = action,
            Path = path,
            Latitude = start.Latitude,
            Longitude = start.Longitude,
        };

        market.Comp.Caravans.Add(caravan);
    }

    private float SegmentLength(Caravan caravan)
    {
        return WorldMapTerrain.AngleBetween(PlaceVector(caravan.Path[caravan.Segment]), PlaceVector(caravan.Path[caravan.Segment + 1]));
    }

    // Once a second: the caravans go, are taken by a threat or held by a storm, and arrive.
    private void UpdateCaravans(Entity<WorldMarketComponent> market, TimeSpan now)
    {
        var comp = market.Comp;
        if (comp.Caravans.Count == 0)
            return;

        EnsureTerrain(market);

        for (var i = comp.Caravans.Count - 1; i >= 0; i--)
        {
            var caravan = comp.Caravans[i];
            if (caravan.Path.Count < 2 || caravan.Path.Any(id => !_places.ContainsKey(id)))
            {
                comp.Caravans.RemoveAt(i);
                continue;
            }

            // What is on the road: a threat takes a caravan that has no escort, a storm holds it.
            var here = WorldMapTerrain.ToVector(caravan.Latitude, caravan.Longitude);
            var held = false;
            string? takenBy = null;

            foreach (var hazard in comp.Hazards)
            {
                if (!_proto.TryIndex<WorldHazardPrototype>(hazard.Proto, out var proto)
                    || WorldMapTerrain.AngleBetween(here, WorldMapTerrain.ToVector(hazard.Latitude, hazard.Longitude)) > proto.Radius)
                {
                    continue;
                }

                if (proto.Weather)
                    held = true;
                else if (!caravan.Escorted)
                    takenBy = Loc.GetString(proto.Name);
            }

            if (takenBy != null)
            {
                comp.Caravans.RemoveAt(i);
                continue;
            }

            if (held)
                continue;

            caravan.Progress += CaravanSpeed;
            var length = SegmentLength(caravan);
            var arrived = false;

            while (caravan.Progress >= length)
            {
                caravan.Progress -= length;
                caravan.Segment++;

                if (caravan.Segment >= caravan.Path.Count - 1)
                {
                    arrived = true;
                    break;
                }

                length = SegmentLength(caravan);
            }

            if (arrived)
            {
                comp.Caravans.RemoveAt(i);
                DeliverCaravan(market, caravan);
                continue;
            }

            // Where it is now: along the great circle from one city of the way to the next.
            var a = PlaceVector(caravan.Path[caravan.Segment]);
            var b = PlaceVector(caravan.Path[caravan.Segment + 1]);
            var angle = MathF.Acos(Math.Clamp(Vector3.Dot(a, b), -1f, 1f));
            var t = length > 0.0001f ? caravan.Progress / length : 0f;
            var sin = MathF.Sin(angle);
            var point = sin < 0.0001f ? a : (MathF.Sin((1f - t) * angle) * a + MathF.Sin(t * angle) * b) / sin;
            (caravan.Latitude, caravan.Longitude) = ToLatLon(point);
        }
    }

    private void DeliverCaravan(Entity<WorldMarketComponent> market, Caravan caravan)
    {
        var target = caravan.Path[^1];

        if (caravan.Gift)
        {
            AddCityInfluence(market, caravan.Faction, target, GiftInfluence);
            return;
        }

        if (!_proto.TryIndex<WorldCityActionPrototype>(caravan.Action, out var action))
            return;

        ApplyCityAction(market, caravan.Faction, target, action);
    }

    // An escort is paid for a caravan on its way: it passes the threats. Gives the cost, or nothing.
    private int? CaravanEscort(Entity<WorldMarketComponent> market, string faction, string idText, float budget)
    {
        if (!int.TryParse(idText, out var id))
            return null;

        var caravan = market.Comp.Caravans.FirstOrDefault(c => c.Id == id && c.Faction == faction);
        if (caravan == null || caravan.Escorted || budget < EscortCost)
            return null;

        caravan.Escorted = true;
        return EscortCost;
    }

    // The caravans for the console of a faction: whose, what, where, how long yet, and whether an escort can be paid for.
    private List<CaravanEntry> BuildCaravanEntries(Entity<WorldMarketComponent> market, string faction, float budget)
    {
        var entries = new List<CaravanEntry>();
        foreach (var caravan in market.Comp.Caravans)
        {
            if (caravan.Path.Count < 2 || caravan.Path.Any(id => !_places.ContainsKey(id)))
                continue;

            // What is left of the way, in degrees, and so in seconds.
            var left = SegmentLength(caravan) - caravan.Progress;
            for (var i = caravan.Segment + 1; i < caravan.Path.Count - 1; i++)
                left += WorldMapTerrain.AngleBetween(PlaceVector(caravan.Path[i]), PlaceVector(caravan.Path[i + 1]));

            var mine = caravan.Faction == faction;
            var blocked = mine && !caravan.Escorted && budget < EscortCost ? Loc.GetString("rumc-map-need-budget") : string.Empty;

            entries.Add(new CaravanEntry(caravan.Id, caravan.Faction, CaravanLabel(caravan), CityName(caravan.Path[^1]), caravan.Latitude, caravan.Longitude,
                caravan.Escorted, mine, (int) Math.Ceiling(Math.Max(0f, left) / CaravanSpeed), EscortCost, blocked));
        }

        return entries;
    }
}
