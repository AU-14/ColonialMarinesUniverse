using Content.Shared.NPC.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Sentry;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedSentryTargetingSystem))]
public sealed partial class SentryTargetingComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<string> FriendlyFactions = new();

    [DataField, AutoNetworkedField]
    public HashSet<string> DeployedFriendlyFactions = new();

    [DataField, AutoNetworkedField]
    public string OriginalFaction = "UNMC";

    [DataField, AutoNetworkedField]
    public HashSet<string> TargetedFactions = new();

    [DataField, AutoNetworkedField]
    public HashSet<string> HumanoidAdded = new();

    /// <summary>
    /// NPC factions set as friendly by the alliance console.
    /// Entities in these factions are never treated as valid targets, regardless of IFF.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<NpcFactionPrototype>> AllianceFriendlyNpcFactions = new();
}
