using Content.Shared.NPC.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.AllianceConsole;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AllianceConsoleComponent : Component
{
    /// <summary>
    /// The faction this console belongs to: govfor/opfor, or an exact npcFaction
    /// id for any other side ("AUWeYu").
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
    /// Factions whose status this console can set. Null = derived at MapInit from
    /// npcFaction protos flagged allianceTarget (prefer), plus every military side for
    /// non-army consoles, minus own faction.
    /// </summary>
    [DataField]
    public HashSet<string>? ControllableFactions;

    public static NpcFactionPrototype? ResolveFaction(IPrototypeManager prototypes, string faction)
    {
        return prototypes.TryIndex(faction, out NpcFactionPrototype? proto)
            ? proto
            : prototypes.TryIndex(faction.ToUpperInvariant(), out proto)
                ? proto
                : null;
    }
}

[Serializable, NetSerializable]
public enum AllianceStatus : byte
{
    Neutral = 0,
    Friendly = 1,
    Hostile = 2,
}
