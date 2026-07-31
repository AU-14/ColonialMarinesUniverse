using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.NPC.Prototypes;
using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Threats.Mobs.CLF;

/// <summary>
///     Marks an entity as a CLF member. Used for showing CLF team identifiers
///     that only other CLF members can see (similar to how zombies identify each other).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CLFMemberComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<NpcFactionPrototype> Faction = "CLF";

    [DataField, AutoNetworkedField]
    public EntProtoId<IFFFactionComponent> IFF = "FactionCLF";

    // Networked so a faction-driven icon swap (InsurgencyFactionApplySystem) actually reaches clients;
    // without state sync the client kept rendering the default CLF icon.
    [DataField, AutoNetworkedField]
    public ProtoId<FactionIconPrototype> StatusIcon = "CLFFaction";
}
