using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.K9.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class K9HandlerComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> Dogs = new();

    [DataField]
    public EntProtoId TameActionId = "RMCActionK9Tame";

    [DataField, AutoNetworkedField]
    public EntityUid? TameAction;

    [DataField]
    public EntProtoId SicEmActionId = "RMCActionK9SicEm";

    [DataField, AutoNetworkedField]
    public EntityUid? SicEmAction;

    [DataField]
    public EntProtoId EvacuateActionId = "RMCActionK9Evacuate";

    [DataField, AutoNetworkedField]
    public EntityUid? EvacuateAction;

    [DataField]
    public EntProtoId GoodBoyActionId = "RMCActionK9GoodBoy";

    [DataField, AutoNetworkedField]
    public EntityUid? GoodBoyAction;
}
