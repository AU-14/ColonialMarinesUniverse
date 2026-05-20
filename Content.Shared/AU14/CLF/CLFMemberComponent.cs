using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.NPC.Prototypes;
using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.AU14.CLF;

/// <summary>
/// Marks an entity as a CLF member. Used for showing CLF team identifiers
/// that only other CLF members can see (similar to how zombies identify each other).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CLFMemberComponent : Component
{
    [DataField]
    public ProtoId<FactionIconPrototype> StatusIcon { get; set; } = "CLFFaction";

    [DataField]
    public ProtoId<NpcFactionPrototype> Faction = "CLF";

    [DataField]
    public EntProtoId<IFFFactionComponent> IFF = "FactionCLF";
}

