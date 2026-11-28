using Content.Shared.CMU14.util;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.Threats;

[Prototype]
public sealed partial class ThreatPrototype : IPrototype
{
    /// <summary>
    ///     Shared vote cooldown identity for variants across game modes. Defaults to this prototype's ID.
    /// </summary>
    [DataField]
    public string? VoteCooldownGroup;

    /// <summary>
    ///     Whether this threat can be voted for in consecutive rounds.
    /// </summary>
    [DataField]
    public bool AllowConsecutiveVotes;

    [DataField]
    public List<string> BlacklistedPlatoons { get; private set; } = new();

    [DataField]
    public List<string> WhitelistedPlatoons { get; private set; } = new();

    [DataField("threatweight", required: false)]
    public int ThreatWeight { get; private set; } = 1;

    /// <summary>
    ///     List of game rule prototype IDs to add for this threat's win condition (e.g., "KillAllGovforRule",
    ///     "ThreatSurviveRule").
    /// </summary>
    [DataField("winconditions", required: false)]
    public List<string> WinConditions { get; private set; } = new();

    /// <summary>
    ///     Optional party to spawn at round start. Threats without one do not reserve or spawn player bodies.
    /// </summary>
    [DataField("roundstartspawns")]
    public ProtoId<PartySpawnPrototype>? RoundStartSpawn { get; private set; }

    /// <summary>
    ///     Whether extra member bodies are spawned beyond the partySpawn plan so every held
    ///     threat voter gets a seat. Elite threats (small designed counts) set this false or
    ///     popularity overrides the threat's intended scale.
    /// </summary>
    [DataField(required: false)]
    public bool SpawnExtraVoteMembers { get; private set; } = true;

    [DataField("possibleInserts")]
    public List<AuInsertPrototype> Inserts { get; private set; } = new();

    //   [DataField("govforratio")]
    //  public float GovForRatio { get; private set; } = 0.6f;

    /// <summary>
    ///     Maximum share of readied players assigned to player-controlled threat bodies.
    /// </summary>
    [DataField("threatratio")]
    public float ThreatRatio { get; private set; } = 0.25f;

    [DataField("thirdpartyratio")]
    public float ThirdPartyRatio { get; private set; } = 0.15f;

    // for roundstart

    [DataField]
    public List<string> BlacklistedGamemodes { get; private set; } = new();

    [DataField]
    public List<string> WhitelistedGamemodes { get; private set; } = new();

    [DataField("maxplayers")]
    public int MaxPlayers { get; private set; }

    [DataField("minplayers")]
    public int MinPlayers { get; private set; }

    [DataField("objectivewhitelist", required: false)]
    public List<string> ObjectiveWhitelist { get; private set; } = new();

    /// <summary>
    ///     Game rules started when the threat deploys, before <c>ThreatDeployedEvent</c> is raised.
    ///     Win conditions start separately (see <see cref="WinConditions"/> and <see cref="WinConditionsOnLanding"/>).
    /// </summary>
    [DataField("addgamerules", required: false)]
    public List<string> AddGameRules { get; private set; } = new();

    /// <summary>
    ///     How the threat's bodies enter the round. <see cref="ThreatEntryMethod.Shuttle"/> seats them aboard
    ///     <see cref="ShuttlePath"/>, which then flies to a landing zone.
    /// </summary>
    [DataField]
    public ThreatEntryMethod EntryMethod { get; private set; } = ThreatEntryMethod.Markers;

    /// <summary>The ship grid loaded for <see cref="ThreatEntryMethod.Shuttle"/> entry.</summary>
    [DataField]
    public ResPath? ShuttlePath { get; private set; }

    /// <summary>Which landing zones the threat ship may choose.</summary>
    [DataField]
    public ThreatLandingZonePolicy LandingZone { get; private set; } = ThreatLandingZonePolicy.ThirdParty;

    /// <summary>
    ///     Planet ID to landing-zone name. When the round's planet is listed and a free zone of that name is
    ///     allowed by <see cref="LandingZone"/>, the ship prefers it.
    /// </summary>
    [DataField]
    public Dictionary<string, string> LandingZoneByPlanet { get; private set; } = new();

    /// <summary>Launch startup time of the threat ship's automatic flight.</summary>
    [DataField]
    public TimeSpan ShuttleStartupTime { get; private set; } = TimeSpan.FromSeconds(4);

    /// <summary>Hyperspace travel time of the threat ship's automatic flight.</summary>
    [DataField]
    public TimeSpan ShuttleHyperspaceTime { get; private set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    ///     When true, win conditions start when the threat ship lands instead of at deployment.
    ///     Only meaningful with <see cref="ThreatEntryMethod.Shuttle"/>.
    /// </summary>
    [DataField]
    public bool WinConditionsOnLanding { get; private set; }

    /// <summary>
    ///     When true and a leader body is still unclaimed after held leaders are seated, the deployment
    ///     seats a member voter in it instead (preferring voters whose profile lists the threat-leader job).
    ///     A held leader is never displaced. The vote hold itself is unchanged.
    /// </summary>
    [DataField]
    public bool UngatedLeader { get; private set; }

    /// <summary>
    ///     When true the threat deploys as soon as its spawn delay ends, seating the held voters who are still
    ///     available, instead of waiting for more than sixty percent of its bodies to be signed up. It is kept
    ///     out of the interest window; leftover bodies become ghost roles as usual.
    /// </summary>
    [DataField]
    public bool SkipInterestGate { get; private set; }

    [DataField("winmessage", required: false)]
    public string? WinMessage { get; private set; }

    [DataField("maxthirdParties")]
    public int MaxThirdParties { get; private set; } = 7;

    [DataField("thirdpartyinterval", required: false)]

    public int ThirdPartyInterval { get; private set; } = 14000;

    [DataField("lorePrimer")]
    public ProtoId<LorePrimerPrototype>? LorePrimer { get; private set; }

    [DataField("hiveevolution")]
    public bool hiveevolution { get; private set; }

    // if xeno evo should send messages

    /// <summary>
    ///     Whether this threat fields xenofauna: non-humanoid threat life (xenos, apes, biomorphs, neomorphs,
    ///     wendigo). The Early Warning Joe's xenofauna directives accept on this flag before the threat's
    ///     bodies have spawned. Never set it on humanoid threats (cultists, tribals, Bad Blood).
    /// </summary>
    [DataField("xenofauna")]
    public bool Xenofauna { get; private set; }

    /// <summary>Whether the hive's burrowed larva pool stays active with this threat; xeno-lineage threats only.</summary>
    [DataField("burrowedLarvaEnabled")]
    public bool BurrowedLarvaEnabled { get; private set; }

    /// <summary>
    ///     Optional job scaling prototype for human job slots.
    ///     Used by ColonyFall and DistressSignal modes (Insurgency/FOF use Planet instead).
    /// </summary>
    [DataField("jobScaling", required: false)]
    public ProtoId<JobScalePrototype>? JobScaling { get; private set; }

    /// <summary>
    ///     Minimum seconds after round start before threat entities spawn and win conditions activate.
    /// </summary>
    [DataField("spawnDelayMin")]
    public int SpawnDelayMin { get; private set; } = 600;

    /// <summary>
    ///     Maximum seconds after round start before threat entities spawn and win conditions activate.
    /// </summary>
    [DataField("spawnDelayMax")]
    public int SpawnDelayMax { get; private set; } = 1200;

    [IdDataField]
    public string ID { get; private set; } = default!;
}
