using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._CMU14.Threats;

[Prototype]
public sealed partial class ThirdPartyPrototype : IPrototype
{
    /// <summary>
    ///     Player-facing display name for this third party (e.g., "UPP GROM Special Forces").
    ///     If not set, falls back to ID.
    /// </summary>
    [DataField("displayName")]
    public string? DisplayName { get; private set; }

    [DataField("blacklistedThreats")]
    public List<string> BlacklistedThreats { get; private set; } = new();

    [DataField("whitelistedThreats")]
    public List<string> WhitelistedThreats { get; private set; } = new();

    // The preferred field is the string 'entrymethod' (values: "ground", "shuttle", "parachute").
    [DataField("entrymethod", required: false)]
    public string? EntryMethod { get; private set; }

    [DataField("dropshippath", required: false)]
    public ResPath dropshippath { get; private set; } = new("/Maps/_CMU14/Shuttles/black_ert.yml");

    // used if enterbyshuttle is true

    [DataField("blacklistedgamemodes")]
    public List<string> BlacklistedGamemodes { get; private set; } = new();

    [DataField("whitelistedgamemodes")]
    public List<string> whitelistedgamemodes { get; private set; } = new();

    [DataField("weight", required: false)]
    public int weight { get; private set; } = 1;

    [DataField("maxplayers")]
    public int MaxPlayers { get; private set; } = 100;

    [DataField("minplayers")]
    public int MinPlayers { get; private set; }

// for rolling

    [DataField("GhostsNeeded")]
    public int GhostsNeeded { get; private set; } = 10;

    // used if this isn't a roundstart spawn

    [DataField("blacklistedPlatoons", required: false)]
    public List<string> BlacklistedPlatoons { get; private set; } = new();

    [DataField("WhitelistedPlatoons", required: false)]
    public List<string> WhitelistedPlatoons { get; private set; } = new();

    [DataField("roundstart", required: false)]
    public bool RoundStart { get; private set; }

    /// <summary>
    ///     Whether this party belongs to a faction included in the Distress Signal survivor announcement.
    /// </summary>
    [DataField]
    public bool AnnounceAsSurvivors { get; private set; }

    [DataField("partyspawn", required: true)]
    public ProtoId<PartySpawnPrototype> PartySpawn { get; private set; }

    [DataField("announcearrival", required: false)]
    public string? AnnounceArrival { get; private set; } = "A new force has entered the battlefield.";

    [IdDataField]
    public string ID { get; private set; } = default!;
}
