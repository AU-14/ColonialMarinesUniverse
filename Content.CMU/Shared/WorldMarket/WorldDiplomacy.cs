// SPDX-License-Identifier: AGPL-3.0-only
namespace Content.Shared.CMU14.WorldMarket;

// What the ambassadors do between themselves and with the cities, as the table of the round keeps it (see WorldMarketSystem.Diplomacy and WorldMarketSystem.Hazards).

// A pact between two factions: neither is surcharged in the regions of interest of the other, and neither may do harm to the other in the cities. A harm done breaks it, in view of all.
public sealed class Pact
{
    public string A = string.Empty;
    public string B = string.Empty;
}

// A pact that one faction has offered another, until a time.
public sealed class PactOffer
{
    public string From = string.Empty;
    public string To = string.Empty;
    public TimeSpan Until;
}

// The right of a faction (the victim) to answer another (the offender) that has done it harm: its secret strike against the offender is cheaper and likelier to succeed, until a time.
public sealed class Retaliation
{
    public string Victim = string.Empty;
    public string Offender = string.Empty;
    public TimeSpan Until;
}

// A bargain for a city, opened when two factions have almost the same favour in it: what each has bid (the bids are paid at once), until a time.
public sealed class CityBid
{
    public string City = string.Empty;
    public TimeSpan EndsAt;
    public readonly Dictionary<string, int> Totals = new();
}

// The faction that won the bargain for a city: its prices there are lower, until a time.
public sealed class CityPatron
{
    public string Faction = string.Empty;
    public TimeSpan Until;
}

// A big work that one faction has offered to do in a city together with another (see WorldCityActionPrototype.Joint): the proposer has paid its share, the other has until a time to pay its own.
public sealed class JointOffer
{
    public string Id = string.Empty;
    public string From = string.Empty;
    public string To = string.Empty;
    public string City = string.Empty;
    public string Action = string.Empty;
    public TimeSpan Until;
}

// A good thing that is going on in a city (a holiday, a fair, a visit): the factions pay to support it and share the favour that it gives; others may spoil it.
public sealed class CityEvent
{
    public string Id = string.Empty;
    public TimeSpan EndsAt;
    public int Spoilers;
    public readonly Dictionary<string, int> Funding = new();
}

// A hazard on the map: a storm, or a threat that a private military company can be hired against. A circle of a radius round a place, until a time.
public sealed class MapHazard
{
    public int Id;
    public string Proto = string.Empty;
    public float Latitude;
    public float Longitude;
    public TimeSpan EndsAt;
}

// A caravan that carries a gift or a work to a city: whose it is, what, the cities of the way and where it is on it (the segment, and how far along it, in degrees), whether it has an escort.
public sealed class Caravan
{
    public int Id;
    public string Faction = string.Empty;
    public bool Gift;
    public string Action = string.Empty;
    public List<string> Path = new();
    public int Segment;
    public float Progress;
    public bool Escorted;
    public float Latitude;
    public float Longitude;
}

// A deal that one faction has offered another: what it gives (a kind, M for money or P for points, and how much) for what it wants. What it gives is paid already and kept until the other agrees.
public sealed class Deal
{
    public string Id = string.Empty;
    public string From = string.Empty;
    public string To = string.Empty;
    public char GiveKind;
    public int GiveAmount;
    public char WantKind;
    public int WantAmount;
    public TimeSpan Until;
}
