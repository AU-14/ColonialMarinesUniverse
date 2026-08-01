using Content.Shared.NPC.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.AU14.AllianceConsole;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AllianceConsoleComponent : Component
{
    /// <summary>
    /// The military faction this console belongs to ("govfor" or "opfor").
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public string Faction = string.Empty;

    /// <summary>
    /// Current alliance statuses for each third-party faction.
    /// Only factions listed in <see cref="ControllableFactions"/> can be modified.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, AllianceStatus> FactionStatuses = new();

    /// <summary>
    /// The set of NPC faction IDs that can be configured via this console.
    /// </summary>
    [DataField]
    public HashSet<string> ControllableFactions = new()
    {
        "AUUpp",
        "AUWeYu",
        "CLF",
        "AUColonist",
        "AUBureau",
        "CMUProdigy",
        "CMUVAI",
        "CMUTWE",
        "CMUUSCMC",
        "NSPA",
        "AUUnitedAmericas",
    };

    public static readonly Dictionary<string, string> FactionDisplayNames = new()
    {
        { "AUUpp",             "UPP" },
        { "AUWeYu",            "Weyland-Yutani" },
        { "CLF",               "CLF" },
        { "AUColonist",        "Colonists" },
        { "AUBureau",          "Colonial Marshals" },
        { "CMUProdigy",        "Prodigy Corporation" },
        { "CMUVAI",            "VAI" },
        { "CMUTWE",            "Three World Empire" },
        { "CMUUSCMC",          "USCMC" },
        { "NSPA",              "NSPA" },
        { "AUUnitedAmericas",  "United Americas" },
    };
}

[Serializable, NetSerializable]
public enum AllianceStatus : byte
{
    Neutral = 0,
    Friendly = 1,
    Hostile = 2,
}
