using Content.Shared.Radio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._AU14.Radio;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RTORelayComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<RadioChannelPrototype>> BridgedChannels = new()
    {
        "radioGovforAlpha",
        "radioGovforBravo",
        "radioGovforCharlie",
    };

    [DataField, AutoNetworkedField]
    public bool Active = false;
}
