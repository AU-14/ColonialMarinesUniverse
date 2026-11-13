using Content.Shared.FixedPoint;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.Yautja.BadBlood;

public enum BadBloodShipState : byte
{
    None,
    Inbound,
    Arrived,
}

/// <summary>
/// One reinforcement purchase: seats on the Lodge spawner that fill, then spawn together.
/// </summary>
public sealed class BadBloodSquadCall
{
    public int Seats;

    /// <summary>
    /// Players holding a seat, in the order they won it.
    /// </summary>
    public readonly List<NetUserId> Claimed = new();

    public TimeSpan OpenedAt;

    /// <summary>
    /// The squad spawns with whoever holds a seat at this time. Unclaimed seats are refunded.
    /// </summary>
    public TimeSpan SpawnAt;

    /// <summary>
    /// What one seat cost, for the refund. Zero for free seats.
    /// </summary>
    public FixedPoint4 SeatCost;

    public bool ReserveOnly;

    public EntityUid? Buyer;
}

/// <summary>
/// A running ascension vote.
/// </summary>
public sealed class BadBloodAscensionVote
{
    public TimeSpan StartedAt;
    public TimeSpan EndsAt;

    /// <summary>
    /// Earliest candidate first. The order is the last tie-break.
    /// </summary>
    public readonly List<EntityUid> Candidates = new();

    /// <summary>
    /// Voter to candidate.
    /// </summary>
    public readonly Dictionary<EntityUid, EntityUid> Votes = new();

    /// <summary>
    /// Founding grace: the vote runs from a bracer prompt because no Lodge has stood yet.
    /// </summary>
    public bool FromBracer;
}

/// <summary>
/// The Bad Blood clan's state for the round: Superiority Points, ledgers, the Leader, the Lodge, Totems and the
/// clan ship. Lives on the <c>CMUBadBloodCampRule</c> game rule entity. Everything here outlives the Lodge.
/// Write through <c>BadBloodCampRuleSystem</c>. The fields are public so its partial files and tests can reach them.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodCampRuleComponent : Component
{
    // ---- Superiority Points ----

    /// <summary>
    /// Spendable SP.
    /// </summary>
    [DataField]
    public FixedPoint4 Sp;

    /// <summary>
    /// SP ever earned, as counted towards armory tiers. Totem income counts at <see cref="TotemTierCredit"/>.
    /// </summary>
    [DataField]
    public FixedPoint4 LifetimeSp;

    [DataField]
    public FixedPoint4 TierTwoLifetimeSp = 12;

    [DataField]
    public FixedPoint4 TierThreeLifetimeSp = 30;

    [DataField]
    public float TotemTierCredit = 0.5f;

    [DataField]
    public FixedPoint4 OfferingSp = 1;

    [DataField]
    public int MaxOfferingsPerBody = 2;

    [DataField]
    public FixedPoint4 CorpseCookSp = 3;

    [DataField]
    public FixedPoint4 MeatCookSp = FixedPoint4.New(0.5);

    /// <summary>
    /// The most SP one body gives through the spit, whole or in pieces.
    /// </summary>
    [DataField]
    public FixedPoint4 CookSpCapPerBody = 3;

    [DataField]
    public FixedPoint4 SquadOfTwoCost = 12;

    [DataField]
    public FixedPoint4 SquadOfThreeCost = 16;

    [DataField]
    public FixedPoint4 HoundCost = 6;

    [DataField]
    public FixedPoint4 TotemCost = 3;

    /// <summary>
    /// Trophies offered so far, by the body they were cut from.
    /// </summary>
    [ViewVariables]
    public readonly Dictionary<EntityUid, int> OfferedPerBody = new();

    /// <summary>
    /// Spit SP credited so far, by body.
    /// </summary>
    [ViewVariables]
    public readonly Dictionary<EntityUid, FixedPoint4> CookSpPerBody = new();

    // ---- the clan ----

    [ViewVariables]
    public TimeSpan StartedAt;

    /// <summary>
    /// Living Bad Bloods when the rule started (L in the design).
    /// </summary>
    [DataField]
    public int LandingParty;

    /// <summary>
    /// Seats the threat vote held for the clan. Raises the living cap when it is above the landing party + bonus.
    /// </summary>
    [DataField]
    public int HeldVoteSeats;

    [DataField]
    public int LivingCapBonus = 2;

    /// <summary>
    /// Bad Bloods + hounds never exceed this share of the player count.
    /// </summary>
    [DataField]
    public float ThreatBudgetFraction = 0.25f;

    [DataField]
    public int HoundsPerMaster = 3;

    /// <summary>
    /// Players whose Bad Blood died this round, with the time of death. Squad and hound seats favour them.
    /// </summary>
    [ViewVariables]
    public readonly Dictionary<NetUserId, TimeSpan> DeadBadBloods = new();

    /// <summary>
    /// How long a dead Bad Blood keeps seat priority over ordinary ghosts once a seat opens.
    /// </summary>
    [DataField]
    public TimeSpan DeadPriorityWindow = TimeSpan.FromSeconds(30);

    // ---- the Leader ----

    [ViewVariables]
    public EntityUid? Leader;

    /// <summary>
    /// When the Leader was first seen dead, deleted, evacuated or disconnected. Null while a Leader is fine.
    /// </summary>
    [ViewVariables]
    public TimeSpan? LeaderGoneAt;

    /// <summary>
    /// Since when the living Leader has had no player attached. Null while one is.
    /// </summary>
    [ViewVariables]
    public TimeSpan? LeaderDisconnectedAt;

    /// <summary>
    /// A disconnected Leader counts as gone after this long.
    /// </summary>
    [DataField]
    public TimeSpan LeaderSsdTimeout = TimeSpan.FromMinutes(5);

    [ViewVariables]
    public bool LeaderEverAssigned;

    // ---- the Lodge ----

    [ViewVariables]
    public EntityUid? Camp;

    [ViewVariables]
    public bool CampPlaced;

    /// <summary>
    /// Pitch Camp is refused before this time. Set when a Lodge is destroyed.
    /// </summary>
    [ViewVariables]
    public TimeSpan? NextCampAt;

    [DataField]
    public TimeSpan CampRebuildCooldown = TimeSpan.FromMinutes(5);

    /// <summary>
    /// A Lodge kit exists and has not been deployed yet.
    /// </summary>
    [ViewVariables]
    public bool KitOut;

    [ViewVariables]
    public EntityUid? Kit;

    [ViewVariables]
    public bool FirstCampPitched;

    /// <summary>
    /// Before any Lodge has stood, a dead Leader does not end the round for this long after the rule starts.
    /// </summary>
    [DataField]
    public TimeSpan FoundingGrace = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The Lodge may not be pitched this close to a dropship landing zone, in tiles.
    /// </summary>
    [DataField]
    public float CampLandingZoneDistance = 25f;

    // ---- the ascension vote ----

    [ViewVariables]
    public BadBloodAscensionVote? ActiveVote;

    [DataField]
    public TimeSpan VoteDuration = TimeSpan.FromSeconds(60);

    [DataField]
    public TimeSpan VotePerCandidate = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan VoteMaxDuration = TimeSpan.FromSeconds(120);

    // ---- the Blood Reserve and squads ----

    /// <summary>
    /// Voters who got no body and chose to wait for a seat, first come first served.
    /// </summary>
    [ViewVariables]
    public readonly List<NetUserId> ReserveQueue = new();

    [DataField]
    public TimeSpan ReserveChoiceTime = TimeSpan.FromSeconds(20);

    [ViewVariables]
    public readonly List<BadBloodSquadCall> SquadCalls = new();

    [DataField]
    public TimeSpan SeatRaffleTime = TimeSpan.FromSeconds(15);

    [DataField]
    public TimeSpan SquadSpawnTimeout = TimeSpan.FromSeconds(60);

    [ViewVariables]
    public bool FoundingSquadOpened;

    [ViewVariables]
    public bool LastStandSquadOpened;

    // ---- the end ----

    /// <summary>
    /// Clan Wipe: the round ends at this time unless a Bad Blood is back by then.
    /// </summary>
    [ViewVariables]
    public TimeSpan? WipeCollapseAt;

    [DataField]
    public TimeSpan ClanWipeDelay = TimeSpan.FromSeconds(120);

    // ---- Clan Totems ----

    [ViewVariables]
    public readonly List<EntityUid> Totems = new();

    [DataField]
    public int TotemLimit = 3;

    [DataField]
    public int TotemLimitHighPop = 4;

    [DataField]
    public int TotemHighPopPlayers = 80;

    /// <summary>
    /// Totems the clan may place for nothing. The first Lodge grants one.
    /// </summary>
    [DataField]
    public int FreeTotems;

    [DataField]
    public int RipeTotemsForCall = 3;

    /// <summary>
    /// Since when enough ripe Totems have stood together. Null while they do not.
    /// </summary>
    [ViewVariables]
    public TimeSpan? RipeHoldStartedAt;

    /// <summary>
    /// How long <see cref="RipeTotemsForCall"/> ripe Totems have to stand together, without a break, to call the ship.
    /// </summary>
    [DataField]
    public TimeSpan RipeHoldForCall = TimeSpan.FromMinutes(10);

    // ---- the clan ship ----

    [ViewVariables]
    public BadBloodShipState ShipState;

    [ViewVariables]
    public TimeSpan? ShipArrivesAt;

    [ViewVariables]
    public TimeSpan? NextSummonAt;

    [ViewVariables]
    public bool TierFourUnlocked;

    /// <summary>
    /// The ship cannot be called before this long after the clan landed. Holding the ripe Totems is the only thing
    /// that calls it.
    /// </summary>
    [DataField]
    public TimeSpan ShipEarliestCall = TimeSpan.FromMinutes(45);

    [DataField]
    public TimeSpan ShipInboundTime = TimeSpan.FromMinutes(20);

    [DataField]
    public TimeSpan SummonInterval = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Extra bracer regen while the ship is inbound, as a fraction.
    /// </summary>
    [DataField]
    public float ShipRegenBonus = 0.25f;

    /// <summary>
    /// How long the Lodge has to stand after the ship arrives for the clan to depart and win.
    /// </summary>
    [DataField]
    public TimeSpan DepartureTime = TimeSpan.FromMinutes(20);

    /// <summary>
    /// What is left of <see cref="DepartureTime"/>. Only counts down while a Lodge stands. Null before arrival.
    /// </summary>
    [ViewVariables]
    public TimeSpan? DepartureRemaining;

    /// <summary>
    /// Minute marks of the departure countdown already announced.
    /// </summary>
    [ViewVariables]
    public readonly HashSet<int> DepartureAnnounced = new();
}
