using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Dropship.Rappel;

[RegisterComponent, NetworkedComponent]
public sealed partial class EEXRappelSystemComponent : Component
{
    [DataField]
    public EntProtoId GroundEndpointPrototype = "CMUEEXRappelGroundEndpoint";

    public EntityUid? GroundEndpoint;

    public EntityUid? Dropship;
}
