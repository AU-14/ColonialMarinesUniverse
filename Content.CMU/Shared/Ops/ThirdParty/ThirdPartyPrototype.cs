using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.Threats;

[Prototype]
public sealed partial class ThirdPartyPrototype : IPrototype, IInheritingPrototype
{
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<ThirdPartyPrototype>))]
    public string[]? Parents { get; private set; }

    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; private set; }
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
    [DataField("entrymethod")]
    public string? EntryMethod { get; private set; }

    [DataField]
    public ResPath dropshippath { get; private set; } = new("/Maps/CMU14/Shuttles/black_ert.yml");

    [DataField]
    public List<string> BlacklistedGamemodes { get; private set; } = new();

    [DataField]
    public List<string> WhitelistedGamemodes { get; private set; } = new();

    [DataField]
    public int weight { get; private set; } = 1;

    [DataField("maxplayers")]
    public int MaxPlayers { get; private set; } = 100;

    /// <summary>
    ///     If true, ghosts may call this party in through the ghost role menu once enough players are dead.
    ///     Meant for minor civilian drop-ins, not heavy forces.
    /// </summary>
    [DataField]
    public bool GhostsCallable { get; private set; }

    [DataField("minplayers")]
    public int MinPlayers { get; private set; }

    [DataField]
    public int GhostsNeeded { get; private set; } = 10;

    [DataField]
    public List<string> BlacklistedPlatoons { get; private set; } = new();

    [DataField]
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

    [DataField("announcearrival")]
    public string? AnnounceArrival { get; private set; } = "A responding force has made their entrance into the conflict zone.";

    /// <summary>
    ///     Announced when a scheduled party goes ready and starts gathering ghost volunteers,
    ///     before it deploys. Worded as responders en route so a delayed landing reads as intended.
    /// </summary>
    [DataField("announceinbound")]
    public string? AnnounceInbound { get; private set; } = "Long range arrays detect an unidentified force moving to answer the distress call. Arrival expected shortly.";

    [IdDataField]
    public string ID { get; private set; } = default!;
}
