// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.WorldMarket;

// Key of the window of the world map, which the console of the ambassador opens (its own window, as the call of the third parties is).
[Serializable, NetSerializable]
public enum AmbassadorWorldMapUi : byte
{
    Key,
}

// A city of the map for the globe: where it lies, what the faction stands with it, what it can do and what a gift to it costs; what is wrong with it (a crisis), what the faction has
// built in it, and what the faction can do for it.
[Serializable, NetSerializable]
public sealed class CityEntry(
    string id,
    string name,
    string description,
    float latitude,
    float longitude,
    Color color,
    int influence,
    List<string> capabilities,
    int giftCost,
    bool canAfford,
    string crisisName = "",
    string crisisDescription = "",
    int crisisSeconds = 0,
    bool roadsRepaired = false,
    List<ProjectEntry>? projects = null,
    List<ActionEntry>? actions = null,
    string zoneOwner = "",
    CityExtras? extras = null,
    bool site = false)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly string Description = description;
    public readonly float Latitude = latitude;
    public readonly float Longitude = longitude;
    public readonly Color Color = color;
    public readonly int Influence = influence;
    public readonly List<string> Capabilities = capabilities;
    public readonly int GiftCost = giftCost;
    public readonly bool CanAfford = canAfford;

    // The trouble that the city is in (nothing in the name if it is not), and for how long yet.
    public readonly string CrisisName = crisisName;
    public readonly string CrisisDescription = crisisDescription;
    public readonly int CrisisSeconds = crisisSeconds;

    // Somebody has mended the roads of the city (the globe draws them whole).
    public readonly bool RoadsRepaired = roadsRepaired;

    // What the faction has built or paid for in the city, and what it can order there.
    public readonly List<ProjectEntry> Projects = projects ?? new();
    public readonly List<ActionEntry> Actions = actions ?? new();

    // The faction whose region of interest the city is in (nothing if none other than the one of the console): everything done there costs more, and the owner gets the extra.
    public readonly string ZoneOwner = zoneOwner;

    // The bargain, the patron, the good thing that is going on, the secret strikes, the joint works.
    public readonly CityExtras Extras = extras ?? new CityExtras();

    // Not a city but another object of the map (a mine, a fort, ruins...).
    public readonly bool Site = site;
}

// A bid in a bargain for a city.
[Serializable, NetSerializable]
public sealed class BidEntry(string faction, int total)
{
    public readonly string Faction = faction;
    public readonly int Total = total;
}

// A secret strike that the faction can make against another in a city: whom, what it costs, how likely it is to succeed, whether it is the answer to a harm, and why it cannot be done now.
[Serializable, NetSerializable]
public sealed class StrikeEntry(string target, int cost, int chance, bool answer, string blocked)
{
    public readonly string Target = target;
    public readonly int Cost = cost;
    public readonly int Chance = chance;
    public readonly bool Answer = answer;
    public readonly string Blocked = blocked;
}

// What is going on in a city besides the troubles: the good thing that is happening (and what has been paid to support it), the bargain, the faction that won it, the strikes that can be made,
// the joint works that can be offered.
[Serializable, NetSerializable]
public sealed class CityExtras
{
    public string EventName = string.Empty;
    public string EventDescription = string.Empty;
    public int EventSeconds;
    public int EventFunding;
    public int EventGoal;
    public int EventMine;
    public int EventSpoilers;
    public int EventSpoilCost;

    public int BidSeconds;
    public List<BidEntry> Bids = new();

    public string Patron = string.Empty;
    public int PatronSeconds;

    public List<StrikeEntry> Strikes = new();
    public List<ActionEntry> JointActions = new();

    // The one scale of favour in the city: how much of the hundred each faction has (BidEntry is a faction and a number).
    public List<BidEntry> Shares = new();

    // A report of intelligence can be bought here (nothing in IntelCost: it cannot), and why not.
    public int IntelCost;
    public string IntelBlocked = string.Empty;

    // The headquarters of the intelligence: the faction has one (the rest is filled only then). The scouts (what they cost, why they cannot be sent, how long till they are back, what they
    // found out), the secret harm (discredit, sabotage, provocation) with its cost and why it cannot be done.
    public bool Hq;
    public int ScoutCost;
    public string ScoutBlocked = string.Empty;
    public int ScoutSeconds;
    public List<string> ScoutReport = new();
    public int ProvokeCost;
    public string ProvokeBlocked = string.Empty;
    public List<StrikeEntry> Smears = new();
    public List<StrikeEntry> Sabotages = new();

    // More secret harm: buying the local leaders over, bribing an official to hold a building up, poisoning the wells where the target is guarded, and catching the scouts of another that are on
    // their way here.
    public List<StrikeEntry> Buyouts = new();
    public List<StrikeEntry> Freezes = new();
    public List<StrikeEntry> Poisons = new();
    public List<StrikeEntry> Intercepts = new();
}

// The things of the whole map: the season, the hazards, the other factions and what the faction stands in with each, the joint works that are offered to it.
[Serializable, NetSerializable]
public sealed class MapExtras
{
    public int Season;
    public int SeasonSeconds;
    public List<HazardEntry> Hazards = new();
    public List<FactionEntry> Factions = new();
    public List<JointOfferEntry> JointOffers = new();
    public List<CaravanEntry> Caravans = new();
    public List<DealEntry> Deals = new();
    public List<DossierEntry> Dossiers = new();
    public int IntelSeconds;
    public int IntelLevel;

    // The headquarters of the intelligence: the faction has one, what the counter-intelligence costs, how long it has to run, and why it cannot be turned on.
    public bool Hq;
    public int CounterCost;
    public int CounterSeconds;
    public string CounterBlocked = string.Empty;

    // Against a faction: leak what it did in secret, put a spy in its headquarters, complain of it to the mother faction (the cost is in points), spoil its main goals.
    public List<StrikeEntry> Leaks = new();
    public List<StrikeEntry> Spies = new();
    public List<StrikeEntry> Complaints = new();
    public List<StrikeEntry> TaskSabotages = new();

    // The false reports for the scouts of the others (what it costs, how long it runs, why it cannot be turned on), what the spy sees, and the inspection that hangs over the faction.
    public int DisinfoCost;
    public int DisinfoSeconds;
    public string DisinfoBlocked = string.Empty;
    public int SpySeconds;
    public List<string> SpyLines = new();
    public bool Inspected;
    public int PayOffCost;
    public string PayOffBlocked = string.Empty;
}

// A caravan on its way: whose, what it carries, where to, where it is now, whether it has an escort, whether it is of the faction of the console, how long yet, what an escort costs.
[Serializable, NetSerializable]
public sealed class CaravanEntry(int id, string faction, string label, string target, float latitude, float longitude, bool escorted, bool mine, int etaSeconds, int escortCost, string blocked)
{
    public readonly int Id = id;
    public readonly string Faction = faction;
    public readonly string Label = label;
    public readonly string Target = target;
    public readonly float Latitude = latitude;
    public readonly float Longitude = longitude;
    public readonly bool Escorted = escorted;
    public readonly bool Mine = mine;
    public readonly int EtaSeconds = etaSeconds;
    public readonly int EscortCost = escortCost;
    public readonly string Blocked = blocked;
}

// A deal that is offered by or to the faction of the console: what is given and what is asked (in words), whether it is offered to this faction, and why it cannot be agreed to now.
[Serializable, NetSerializable]
public sealed class DealEntry(string id, string from, string to, string give, string want, bool incoming, string blocked)
{
    public readonly string Id = id;
    public readonly string From = from;
    public readonly string To = to;
    public readonly string Give = give;
    public readonly string Want = want;
    public readonly bool Incoming = incoming;
    public readonly string Blocked = blocked;
}

// What a report of intelligence says of another faction (minus one, or nothing in the text, where the tier of the report does not reach).
[Serializable, NetSerializable]
public sealed class DossierEntry(string faction, bool online, int trust, int points, int cities, string influence, string projects, bool pact)
{
    public readonly string Faction = faction;
    public readonly bool Online = online;
    public readonly int Trust = trust;
    public readonly int Points = points;
    public readonly int Cities = cities;
    public readonly string Influence = influence;
    public readonly string Projects = projects;
    public readonly bool Pact = pact;
}

// A hazard of the map as the console shows it: what it is, where, for how long, whether it is weather, what a company costs against it, and why it cannot be hired now.
[Serializable, NetSerializable]
public sealed class HazardEntry(int id, string name, string description, float latitude, float longitude, float radius, int secondsLeft, bool weather, int removeCost, string blocked)
{
    public readonly int Id = id;
    public readonly string Name = name;
    public readonly string Description = description;
    public readonly float Latitude = latitude;
    public readonly float Longitude = longitude;
    public readonly float Radius = radius;
    public readonly int SecondsLeft = secondsLeft;
    public readonly bool Weather = weather;
    public readonly int RemoveCost = removeCost;
    public readonly string Blocked = blocked;
}

// Another faction: whether there is a pact with it, whether one is offered (by this faction, to this faction), and whether this faction has the right to answer a harm of it.
[Serializable, NetSerializable]
public sealed class FactionEntry(string name, bool pact, bool offerSent, bool offerReceived, bool answer)
{
    public readonly string Name = name;
    public readonly bool Pact = pact;
    public readonly bool OfferSent = offerSent;
    public readonly bool OfferReceived = offerReceived;
    public readonly bool Answer = answer;
}

// A joint work that another faction offers to this one: what, where, who, and what the share costs.
[Serializable, NetSerializable]
public sealed class JointOfferEntry(string id, string from, string city, string action, int cost, string blocked)
{
    public readonly string Id = id;
    public readonly string From = from;
    public readonly string City = city;
    public readonly string Action = action;
    public readonly int Cost = cost;
    public readonly string Blocked = blocked;
}

// What the console asks the table to do with the other factions and the cities (see MapCommandKind).
[Serializable, NetSerializable]
public enum MapCommandKind : byte
{
    PactPropose,
    PactAccept,
    PactBreak,
    Strike,
    Bid,
    JointPropose,
    JointAnswer,
    EventSupport,
    EventSpoil,
    HazardRemove,
    Escort,
    DealPropose,
    DealAnswer,
    Intel,

    // The intelligence (needs the headquarters): scouts into a place, discredit, sabotage, provocation, counter-intelligence.
    Scouts,
    Smear,
    Sabotage,
    Provoke,
    CounterIntel,

    // More of it: buy the leaders over, bribe an official, poison the wells, leak a secret harm, false reports, catch the scouts, a spy in a headquarters, a complaint to the mother faction
    // (and paying off the inspection that it brings), spoiling the main goals.
    Buyout,
    Freeze,
    Poison,
    Leak,
    Disinfo,
    Intercept,
    Spy,
    Complain,
    PayOff,
    SabotageTasks,
}

[Serializable, NetSerializable]
public sealed class MapCommandMessage(MapCommandKind kind, string a = "", string b = "", string c = "", int n = 0) : BoundUserInterfaceMessage
{
    public readonly MapCommandKind Kind = kind;
    public readonly string A = a;
    public readonly string B = b;
    public readonly string C = c;
    public readonly int N = n;
}

// A road of the map: between two cities of the round, of the kind that the land between them makes it (a road or a trail on land, a sea route over water).
[Serializable, NetSerializable]
public sealed class RoadEntry(string id, string from, string to, string name, string description, WorldRoadKind kind, string blocked = "")
{
    public readonly string Id = id;
    public readonly string From = from;
    public readonly string To = to;
    public readonly string Name = name;
    public readonly string Description = description;
    public readonly WorldRoadKind Kind = kind;

    // Why the road is closed now (a storm, a threat), nothing if it is open.
    public readonly string Blocked = blocked;
}

// A region of interest on the map: a circle of Radius degrees round a place, whose it is, for how long yet, and whether it is the faction of the console.
[Serializable, NetSerializable]
public sealed class ZoneEntry(string owner, float latitude, float longitude, float radius, int secondsLeft, bool mine)
{
    public readonly string Owner = owner;
    public readonly float Latitude = latitude;
    public readonly float Longitude = longitude;
    public readonly float Radius = radius;
    public readonly int SecondsLeft = secondsLeft;
    public readonly bool Mine = mine;
}

// Something that the faction has done in a city that is still going on: being built (ReadySeconds), standing (LeftSeconds, or minus one if for good), with what is left of its charges.
[Serializable, NetSerializable]
public sealed class ProjectEntry(string name, int readySeconds, int leftSeconds, int charges)
{
    public readonly string Name = name;
    public readonly int ReadySeconds = readySeconds;
    public readonly int LeftSeconds = leftSeconds;
    public readonly int Charges = charges;
}

// What the faction can do for a city: the name, what it is, what it costs, what it gives, and why it cannot be done now (nothing if it can).
[Serializable, NetSerializable]
public sealed class ActionEntry(string id, string name, string description, int cost, int influence, string blocked, int pointCost = 0)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly string Description = description;
    public readonly int Cost = cost;
    public readonly int Influence = influence;
    public readonly string Blocked = blocked;

    // The points of influence that it costs besides the money.
    public readonly int PointCost = pointCost;
}

// What the window of the map shows: the budget of the console, the number that the land is made from, the cities (put on the land) with the favour of the faction in each, the
// roads, the regions of interest and the things of the whole map. (The biomes are in the prototypes, so the client has them.)
[Serializable, NetSerializable]
public sealed class AmbassadorWorldMapState(float budget, int points, string factionName, int mapSeed, List<CityEntry> cities, List<RoadEntry> roads, List<ZoneEntry> zones, int zoneCost, MapExtras extras) : BoundUserInterfaceState
{
    public readonly float Budget = budget;

    // The points of influence with the mother faction (the second scale of the ambassador).
    public readonly int Points = points;
    public readonly string FactionName = factionName;

    // What the land is made from (see WorldMapTerrain): the cities are already put on it.
    public readonly int MapSeed = mapSeed;
    public readonly List<CityEntry> Cities = cities;
    public readonly List<RoadEntry> Roads = roads;

    // The regions of interest of all the factions, and what a new one costs in points for this faction.
    public readonly List<ZoneEntry> Zones = zones;
    public readonly int ZoneCost = zoneCost;

    // The season, the hazards, the other factions.
    public readonly MapExtras Extras = extras;
}

// The faction marks a region of the map (round this place) as its interest.
[Serializable, NetSerializable]
public sealed class ZoneClaimMessage(float latitude, float longitude) : BoundUserInterfaceMessage
{
    public readonly float Latitude = latitude;
    public readonly float Longitude = longitude;
}

// A gift to a city, paid from the budget of the console.
[Serializable, NetSerializable]
public sealed class GiftCityMessage(string city) : BoundUserInterfaceMessage
{
    public readonly string City = city;
}

// Something done for a city (see WorldCityActionPrototype), paid from the budget of the console.
[Serializable, NetSerializable]
public sealed class CityActionMessage(string city, string action) : BoundUserInterfaceMessage
{
    public readonly string City = city;
    public readonly string Action = action;
}
