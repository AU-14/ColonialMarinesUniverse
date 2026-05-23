using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.AU14;

/// <summary>
/// Marks an entity as a cultist, allowing them to access the Hivemind channel.
/// Also used for showing cultist team identifiers visible to other cultists and xenos.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CultistComponent : Component
{
    [DataField]
    public ProtoId<FactionIconPrototype> StatusIcon { get; set; } = "CultistFaction";
    [DataField, AutoNetworkedField]
    public EntityUid? Hive;
}

