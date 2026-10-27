// SPDX-License-Identifier: AGPL-3.0-only
namespace Content.Shared.CMU14.WorldMarket;

// The world outside the colony as one table, kept by the server for the round: the indices and their past, the news, what is going on (embargoes, delays), the agreements that are signed,
// the quotas of the mother company, the voyages, and what the organizations outside the law have sold and asked. It lives on an entity of its own that the first console of the colony
// administration makes.
[RegisterComponent]
public sealed partial class WorldMarketComponent : Component
{
    // The indices by category, and the last values of each for the graph.
    public readonly Dictionary<string, float> Index = new();
    public readonly Dictionary<string, List<float>> History = new();
    public readonly Dictionary<string, float> LastChange = new();

    public TimeSpan NextTick;

    // The number that the map of the round is made from (the same for the server and the clients, new every round). Nothing else is kept of the map: the land and the places of the cities
    // come of it.
    public int MapSeed;

    // The favour of each faction (the ambassador consoles of one faction share it) in each city of the map: by the name of the faction, then by the name of the city, 0 to 100. The map and
    // the cities are the same for all of them, the favour is not.
    public readonly Dictionary<string, Dictionary<string, int>> CityInfluence = new();

    // What the factions have ordered for the cities and that is still going on, the troubles that the cities are in now (by the name of the city),
    // what the standing things have brought in and the budget has not taken yet (by the name of the faction), and when the next trouble may come.
    public readonly List<CityProject> CityProjects = new();
    public readonly Dictionary<string, CityCrisis> CityCrises = new();
    public readonly Dictionary<string, float> FactionIncome = new();

    // The points of influence that each faction has with its mother faction (by the name of the faction), and which of the steps of favour in the cities have already been paid in points
    // ("faction|city|step"), so that each is paid once. The points are spent on orders, or changed for money.
    public readonly Dictionary<string, int> MotherPoints = new();
    public readonly HashSet<string> MotherSteps = new();

    // How many times each faction has ordered each fighting company ("faction|offer"): every order doubles the price of the next.
    public readonly Dictionary<string, int> OffersBought = new();

    // The intelligence: the scouts that the factions have sent ("faction|place"), and until when each faction has counter-intelligence on.
    public readonly Dictionary<string, ScoutReport> Scouts = new();
    public readonly Dictionary<string, TimeSpan> CounterUntil = new();

    // The secret harm that was done lately (to be leaked or spied on), until when each faction feeds false reports to the scouts of the others, the spies in the headquarters (by the faction
    // that put them), and the factions that the mother faction is going to inspect: the next payment to them is cut by half.
    public readonly List<HarmRecord> HarmLog = new();
    public readonly Dictionary<string, TimeSpan> DisinfoUntil = new();
    public readonly Dictionary<string, SpyOrder> Spies = new();
    public readonly HashSet<string> Inspected = new();

    // The holidays from taxes that are on.
    public readonly List<TaxBreak> TaxBreaks = new();

    // The factions that have an ambassador console (they are given tasks and money), the trust of the mother faction in each (0 to 100: it changes the prices of its services and the money
    // that it sends), what each has done in the cities, the tasks that are given now, crates that are to be put down at the consoles (by the name of the faction), the regions of interest,
    // and when the mother faction next asks for a report and sends money. (The missions have no time: each faction is always given a few main goals.)
    public readonly HashSet<string> KnownFactions = new();
    public readonly Dictionary<string, int> Trust = new();
    public readonly Dictionary<string, FactionStats> FactionStats = new();
    public readonly List<MotherTask> MotherTasks = new();
    public readonly Dictionary<string, List<string>> PendingCrates = new();
    public readonly List<InterestZone> Zones = new();
    public TimeSpan NextMotherReport;
    public TimeSpan NextStipend;

    // The main goals that each faction has done ("faction|task"): it is not set the same one again.
    public readonly HashSet<string> CompletedMissions = new();

    // The pacts and the offers of them, the rights to answer a harm, when each faction may strike in secret again, the bargains for the cities and who has won them, the joint works that are
    // offered, the good things that are going on in the cities, the hazards of the map, and when the next of each comes.
    public readonly List<Pact> Pacts = new();
    public readonly List<PactOffer> PactOffers = new();
    public readonly List<Retaliation> Retaliations = new();
    public readonly Dictionary<string, TimeSpan> StrikeReady = new();
    public readonly Dictionary<string, CityBid> Bids = new();
    public readonly Dictionary<string, CityPatron> Patrons = new();
    public readonly List<JointOffer> JointOffers = new();
    public readonly Dictionary<string, CityEvent> CityEvents = new();
    public readonly List<MapHazard> Hazards = new();

    // The caravans that are on the roads, the deals that are offered, the reports of intelligence that the factions have bought (until when, and how much they say), and when each faction was last
    // seen on the line.
    public readonly List<Caravan> Caravans = new();
    public int NextCaravanId;
    public readonly List<Deal> Deals = new();
    public int NextDealId;
    public readonly Dictionary<string, TimeSpan> IntelUntil = new();
    public readonly Dictionary<string, int> IntelLevel = new();
    public readonly Dictionary<string, TimeSpan> LastSeen = new();
    public int NextHazardId;
    public TimeSpan NextCityEvent;
    public TimeSpan NextHazard;
    public TimeSpan NextBidCheck;
    public TimeSpan NextCityCrisis;

    public bool Started;
}

