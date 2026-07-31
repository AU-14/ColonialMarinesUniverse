using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.CraftIntoPipeBomb;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]

public sealed partial class CraftIntoPipeBombComponent : Component
{
    [DataField("Spawn"), AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public EntProtoId Spawn { get; set; } = "";

    [DataField("Time"), AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(2);

    [DataField("DestroyReq"), AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public bool DestroyReqAfterResult { get; set; } = false;

    [DataField("NeedBlowtorch"), AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public bool Blowtorch { get; set; } = false;

    [DataField("NeedLighter"), AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public bool Lighter { get; set; } = false;

    [DataField("NeedWires"), AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public bool Wires { get; set; } = false;
}
