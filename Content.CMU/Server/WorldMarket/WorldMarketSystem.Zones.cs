// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Shared.CMU14.WorldMarket;

namespace Content.Server.CMU14.WorldMarket;

// The regions of interest. A faction marks a region of the map (a circle round a place) as its interest for a time. Everything that another faction does for a city in it costs more, and the
// extra goes to the owner. A faction holds a few of them; the same one marked again is kept longer.
public sealed partial class WorldMarketSystem
{
    public const float ZoneRadius = 15f;
    public const int ZoneSeconds = 1200;
    public const int MaxZonesPerFaction = 2;

    // The extra that others pay in a region, in percent of the price.
    public const int ZoneSurchargePercent = 25;

    // The points that a region costs.
    public int ZoneCost(string faction)
    {
        return Math.Max(1, (int) Math.Round(PriceFactor(TrustOf(faction)), MidpointRounding.AwayFromZero));
    }

    // The extra for a price in somebody's region (nothing if the city is not in one).
    public static int Surcharge(int price, string? owner)
    {
        return owner == null ? 0 : (int) Math.Ceiling(price * ZoneSurchargePercent / 100f);
    }

    // Whose region the city is in, if it is in one of another faction than the one that acts.
    private string? ZoneOwnerOf(Entity<WorldMarketComponent> market, string cityId, string faction)
    {
        if (market.Comp.Zones.Count == 0)
            return null;

        EnsureTerrain(market);
        if (!_places.TryGetValue(cityId, out var place))
            return null;

        var city = WorldMapTerrain.ToVector(place.Latitude, place.Longitude);
        var now = _timing.CurTime;

        foreach (var zone in market.Comp.Zones)
        {
            if (zone.Faction != faction && zone.Until > now && !InPact(market, zone.Faction, faction)
                && WorldMapTerrain.AngleBetween(city, WorldMapTerrain.ToVector(zone.Latitude, zone.Longitude)) <= ZoneRadius)
            {
                return zone.Faction;
            }
        }

        return null;
    }

    // The faction marks a region round a place. Gives whether it was done.
    public bool ClaimZone(string faction, float latitude, float longitude)
    {
        if (!TryMarket(out var market) || !float.IsFinite(latitude) || !float.IsFinite(longitude) || Math.Abs(latitude) > 90f || Math.Abs(longitude) > 180f)
            return false;

        var now = _timing.CurTime;
        market.Comp.Zones.RemoveAll(z => z.Until <= now);

        var place = WorldMapTerrain.ToVector(latitude, longitude);
        InterestZone? own = null;
        foreach (var zone in market.Comp.Zones)
        {
            if (WorldMapTerrain.AngleBetween(place, WorldMapTerrain.ToVector(zone.Latitude, zone.Longitude)) >= ZoneRadius * 0.75f)
                continue;

            // Another's region is not taken; one's own is kept longer.
            if (zone.Faction != faction)
                return false;

            own = zone;
        }

        if (own == null && market.Comp.Zones.Count(z => z.Faction == faction) >= MaxZonesPerFaction)
            return false;

        if (!TrySpendPoints(faction, ZoneCost(faction)))
            return false;

        if (own != null)
        {
            own.Until = now + TimeSpan.FromSeconds(ZoneSeconds);
        }
        else
        {
            market.Comp.Zones.Add(new InterestZone
            {
                Faction = faction,
                Latitude = latitude,
                Longitude = longitude,
                Until = now + TimeSpan.FromSeconds(ZoneSeconds),
            });
        }

        return true;
    }

    // The regions of all the factions, for the globe.
    public List<ZoneEntry> BuildZones(string faction)
    {
        var zones = new List<ZoneEntry>();
        if (!TryMarket(out var market))
            return zones;

        var now = _timing.CurTime;
        foreach (var zone in market.Comp.Zones.Where(z => z.Until > now))
            zones.Add(new ZoneEntry(zone.Faction, zone.Latitude, zone.Longitude, ZoneRadius, (int) Math.Ceiling((zone.Until - now).TotalSeconds), zone.Faction == faction));

        return zones;
    }
}
