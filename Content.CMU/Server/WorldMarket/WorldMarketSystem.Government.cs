// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Shared.CMU14.WorldMarket;

namespace Content.Server.CMU14.WorldMarket;

// A city of the map of the round, as the government of the colony sees it: what it is called, what it can do, and the trouble that it is in (nothing if none) and how long it goes on.
public sealed record WorldMarketCityInfo(string Id, string Name, List<CityCapability> Capabilities, string CrisisName, int CrisisLeftSeconds);

public sealed partial class WorldMarketSystem
{
    #region The government of the colony

    // What the government of the colony (see Server/Government) asks of the table of the world: which factions have an ambassador on the line, a way to give them points of influence and to shake
    // their trust in their mother faction, and the cities of the map of the round and the troubles that they are in.

    // The factions whose ambassadors have been on the line this round.
    public List<string> KnownFactions()
    {
        return TryMarket(out var market) ? market.Comp.LastSeen.Keys.OrderBy(f => f).ToList() : new List<string>();
    }

    public void GrantPoints(string faction, int points)
    {
        if (points != 0 && TryMarket(out var market))
            market.Comp.MotherPoints[faction] = Math.Max(0, market.Comp.MotherPoints.GetValueOrDefault(faction) + points);
    }

    public void ShiftTrust(string faction, int amount)
    {
        if (amount != 0 && TryMarket(out var market))
            AddTrust(market, faction, amount);
    }

    // The cities of the map of the round.
    public List<WorldMarketCityInfo> Cities()
    {
        var result = new List<WorldMarketCityInfo>();
        if (!TryMarket(out var market))
            return result;

        var now = _timing.CurTime;
        foreach (var city in ActiveCities(market))
        {
            var crisis = string.Empty;
            var left = 0;
            if (market.Comp.CityCrises.TryGetValue(city.ID, out var active) && active.EndsAt > now)
            {
                crisis = CrisisName(active.Id);
                left = (int) Math.Ceiling((active.EndsAt - now).TotalSeconds);
            }

            result.Add(new WorldMarketCityInfo(city.ID, Loc.GetString(city.Name), city.Capabilities.ToList(), crisis, left));
        }

        return result;
    }

    // A trouble of a city goes sooner: the part of what is left of it that is cut off.
    public void ShortenCrisis(string cityId, float fraction)
    {
        if (!TryMarket(out var market) || !market.Comp.CityCrises.TryGetValue(cityId, out var crisis))
            return;

        var now = _timing.CurTime;
        if (crisis.EndsAt <= now)
            return;

        crisis.EndsAt = now + (crisis.EndsAt - now) * (1.0 - Math.Clamp(fraction, 0f, 1f));
    }

    #endregion
}
