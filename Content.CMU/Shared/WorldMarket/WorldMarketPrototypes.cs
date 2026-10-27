// SPDX-License-Identifier: AGPL-3.0-only
using Content.Shared.CMU14.Threats;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.WorldMarket;

// A price index of the world outside the colony (metals, fuel, gems, weapons). It wanders by itself and is shaken by the events of the world; the companies' prices of the goods that
// belong to it follow it. 100 is the usual price.
[Prototype("worldMarketCategory")]
public sealed partial class WorldMarketCategoryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public Color Color = Color.White;

    [DataField]
    public float Start = 100f;

    // How far the index wanders in a tick, in percent of itself, and how much of the way back to the usual price it comes in a tick.
    [DataField]
    public float Volatility = 1.4f;

    [DataField]
    public float Pull = 0.06f;

    [DataField]
    public float Min = 45f;

    [DataField]
    public float Max = 210f;
}

// A city of the world map: the globe of the console shows it where it lies. What it can do is its capabilities, and its favour is won with gifts (see WorldMarketSystem.Cities).
[Prototype("worldCity")]
public sealed partial class WorldCityPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    // A port stands on a coast, any other city inland. Where exactly is decided by the map of the round (see WorldMapTerrain.PlaceCities): it is made at random.
    [DataField]
    public bool Coastal;

    [DataField]
    public Color Color = Color.White;

    [DataField]
    public List<CityCapability> Capabilities = new();

    // What a gift costs the colony; every gift gives the same favour.
    [DataField]
    public int GiftCost = 400;

    // Not a city but another object of the map (a mine, a fort, ruins, a relay...): it has the favour of the factions and can be built on like a city, a few of them are taken every round
    // besides the cities, and they are joined to the nearest city by a trail.
    [DataField]
    public bool Site;
}

// What a city can do for the colony. Each has a text of its own (rumc-city-capability-<name>); for now only the trade hub does something (it takes a part off the duty). The farm cities sell food and seeds, the rich cities sell and dearly buy luxuries (see colonyWorldGood).
[Serializable]
public enum CityCapability : byte
{
    TradeHub,
    ArmsMarket,
    Intelligence,
    BlackMarket,
    Agrarian,
    Wealthy,

    // A deposit (a mine, a well, a plantation): what the factions build there brings in twice as much.
    Deposit,
}

// A biome of the world map: what a cell of the globe is (the sea, the ice, the forest...), with its colour and its description, which the console shows when a cell is picked.
[Prototype("worldBiome")]
public sealed partial class WorldBiomePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    [DataField]
    public Color Color = Color.Gray;
}

// What a road of the map is (the roads are made of the places of the cities of the round: see WorldMarketSystem.Cities).
[Serializable]
public enum WorldRoadKind : byte
{
    Road,
    Trail,
    SeaRoute,
}

// A trouble that a city of the map can fall into (an epidemic, a fire...). It lasts for a time; what is done against it is the actions (see WorldCityActionPrototype), and if nobody does
// anything the favour of every faction in the city falls when it ends.
[Prototype("worldCityCrisis")]
public sealed partial class WorldCrisisPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    [DataField]
    public int DurationSeconds = 240;

    // How often it happens, against the others.
    [DataField]
    public float Weight = 1f;

    // What every faction loses of its favour in the city if the crisis ends with nothing done.
    [DataField]
    public int Penalty = 5;
}

// Something that an ambassador can do for a city of the map for the favour of it: dig wells, mend roads, keep stores, put a hospital up, feed a fund... Some of it is done at once, some is
// built over a time, some stands for a time or for good and brings in money or keeps a trouble away. It is paid from the budget of the console.
[Prototype("worldCityAction")]
public sealed partial class WorldCityActionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    // The order in the list of the console.
    [DataField]
    public int Order;

    [DataField]
    public int Cost = 300;

    // The favour that it gives (when it is built, if it takes time). If it ends a crisis, the sooner the more of it.
    [DataField]
    public int Influence = 5;

    // The favour of the faction in the city that it takes to do it.
    [DataField]
    public int MinInfluence;

    // How long it takes to be built, and how long it stands after that (nothing: for good).
    [DataField]
    public int BuildSeconds;

    [DataField]
    public int EffectSeconds;

    // What it brings in (or costs, if it is below nothing) to the budget of the faction while it stands.
    [DataField]
    public float IncomePerMinute;

    // What of the cost comes back, in percent, when it ends (a fund).
    [DataField]
    public int ReturnPercent;

    // The troubles that it keeps away from the city while it stands.
    [DataField]
    public List<ProtoId<WorldCrisisPrototype>> Protects = new();

    // The troubles that it takes on itself: a charge of it is spent, the trouble does not come, and the faction gets AbsorbInfluence.
    [DataField]
    public List<ProtoId<WorldCrisisPrototype>> Absorbs = new();

    [DataField]
    public int Charges;

    [DataField]
    public int AbsorbInfluence = 12;

    // The troubles that it ends when it is ordered (and it can be ordered against them at any other time too, unless OnlyInCrisis).
    [DataField]
    public List<ProtoId<WorldCrisisPrototype>> Resolves = new();

    [DataField]
    public bool OnlyInCrisis;

    // It can be ordered again while it stands (a fund).
    [DataField]
    public bool Stackable;

    // The roads of the city are drawn whole on the globe while it stands.
    [DataField]
    public bool RepairsRoads;

    // The points of influence with the mother faction that it costs (besides the money; a building bought for the reputation is not sent by the roads, it is put up at once).
    [DataField]
    public int PointCost;

    // A great building that brings the faction PointsPerPeriod points of influence with the mother faction every PointEverySeconds while it stands (nothing: no points).
    [DataField]
    public int PointEverySeconds;

    [DataField]
    public int PointsPerPeriod = 1;

    // A big work of two factions: the one that offers it pays Cost, the other pays Cost too, and then both have it (the favour, the income) while both keep Influence 20 in the city.
    [DataField]
    public bool Joint;

    // Where it can be built: in the cities, in the other objects of the map (the sites), or in both.
    [DataField]
    public WorldPlacement Placement = WorldPlacement.Any;

    // What the building lets its faction do while it stands (nothing for most).
    [DataField]
    public WorldBuildingFeature Feature = WorldBuildingFeature.None;

    // A faction may have only one of it at a time (in the whole world).
    [DataField]
    public bool Unique;
}

// Where a building can be put up.
[Serializable]
public enum WorldPlacement : byte
{
    Any,
    City,
    Site,
}

// What a building gives to its faction. The headquarters of the intelligence lets it send scouts and do harm to the others in secret (see WorldMarketSystem.Espionage).
[Serializable]
public enum WorldBuildingFeature : byte
{
    None,
    IntelHq,
}

// Something that an ambassador can order of the mother faction for the points of influence that it has with it (see WorldMarketSystem.Mother): a private military company of a faction (as many
// as there is influence for), crates of food or of medicine, workers, a holiday from a tax. The points are earned in the cities of the map and can be changed for money at a set rate.
[Prototype("worldMotherOffer")]
public sealed partial class WorldMotherOfferPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    [DataField(required: true)]
    public MotherOfferGroup Group;

    // The faction that it comes from, for a company (the name of it, for the list).
    [DataField]
    public string Faction = string.Empty;

    // The ambassador faction whose company it is (by the name that the console has: UPP, United Americas, ...). Its own faction orders it at the price; a faction that has a pact with the home
    // faction borrows it for twice the price; any other cannot. A company with no home (a corporation, a cell) is for everyone.
    [DataField]
    public string Home = string.Empty;

    // The points of influence that it costs.
    [DataField]
    public int Cost = 1;

    // The order in the list.
    [DataField]
    public int Order;

    // A company or a party of workers: what is called in (as the ambassador calls a third party).
    [DataField]
    public ProtoId<ThirdPartyPrototype>? Party;

    // A party that is the same for all but belongs to whoever calls it: the one for each ambassador faction (by the name that the console has). Used instead of Party where it has the faction.
    [DataField]
    public Dictionary<string, ProtoId<ThirdPartyPrototype>> Parties = new();

    // Food or medicine: what is put down at the console, and how many of them.
    [DataField]
    public EntProtoId? Crate;

    [DataField]
    public int Amount = 1;

    // A holiday from a tax: which tax, by how many points of percent it is lower, and for how long.
    [DataField]
    public WorldTaxKind Tax;

    [DataField]
    public float TaxPoints = 5f;

    [DataField]
    public int DurationSeconds = 600;

    // A transport: how long it takes to come, and whether it takes people out of a city in trouble (then it is sent to a city, not to a console).
    [DataField]
    public int DelaySeconds = 60;

    [DataField]
    public bool Evacuation;
}

// What an offer of the mother faction is.
[Serializable]
public enum MotherOfferGroup : byte
{
    Pmc,
    Food,
    Medicine,
    Workers,
    Tax,
    Transport,
    Support,
}

// The taxes that can be lowered for a time.
[Serializable]
public enum WorldTaxKind : byte
{
    Sales,
    Income,
    Trade,
    Duty,
}

// What the mother faction asks of an ambassador: a report (often, short, and it must be sent by hand) or a mission (seldom, longer, done by itself when it is done). It is a list of
// requirements (nothing in a number: not asked) that are counted from the time it was given, and a reward. If the time runs out the trust of the mother faction falls.
[Prototype("worldMotherTask")]
public sealed partial class WorldMotherTaskPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    // A report is sent by the ambassador when it is ready; a mission is done when its requirements are met.
    [DataField]
    public bool Report;

    [DataField]
    public int DurationSeconds = 150;

    // The cities in which the favour of the faction is at least CityInfluence.
    [DataField]
    public int Cities;

    [DataField]
    public int CityInfluence = 25;

    // What the standing things in the cities have brought in, in money.
    [DataField]
    public int Earn;

    // How many things have been ordered for the cities, how many troubles have been ended or kept off, how many parties of workers have been ordered.
    [DataField]
    public int Projects;

    [DataField]
    public int Crises;

    [DataField]
    public int Workers;

    [DataField]
    public int RewardPoints = 1;

    [DataField]
    public int RewardMoney;

    // A crate of rare things that is put down at the console.
    [DataField]
    public EntProtoId? RewardCrate;

    // The trust of the mother faction that it gives and that is lost if it is not done.
    [DataField]
    public int TrustGain = 6;

    [DataField]
    public int TrustLoss = 8;
}

// A good thing that can happen in a city of the map (a holiday, a fair, a visit of a delegation). The factions pay to support it, and when it ends the favour that it gives (Reward) is shared
// between them by what each has paid, as much of it as the money reaches the Goal; another faction may spoil it, and then it gives less.
[Prototype("worldCityEvent")]
public sealed partial class WorldCityEventPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    [DataField]
    public int DurationSeconds = 300;

    [DataField]
    public float Weight = 1f;

    // The favour that it gives, all of it, to share.
    [DataField]
    public int Reward = 18;

    // The money that has to be put in for all of it.
    [DataField]
    public int Goal = 800;
}

// A hazard on the map: a storm (weather: it cannot be sent away), or a threat (a raid, a plague, a fire: a private military company can be hired against it). It is a circle round a place for a
// time. It closes the roads that pass through it, and the work in the cities inside it brings in less.
[Prototype("worldHazard")]
public sealed partial class WorldHazardPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    // Weather comes on the sea routes and cannot be sent away.
    [DataField]
    public bool Weather;

    [DataField]
    public int DurationSeconds = 240;

    // The radius, in degrees.
    [DataField]
    public float Radius = 10f;

    [DataField]
    public float Weight = 1f;

    // Which roads it closes.
    [DataField]
    public bool ClosesSea;

    [DataField]
    public bool ClosesLand;

    // What the work in the cities inside it brings in, of what it should (one: all of it).
    [DataField]
    public float IncomeFactor = 1f;

    // What it costs to hire a company against it (nothing: it cannot be sent away), and the favour that the one who did it gets in the nearest city.
    [DataField]
    public int RemoveCost;

    [DataField]
    public int RemoveInfluence = 8;
}
