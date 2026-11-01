using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.ThreeD;

/// <summary>Opt-in elevation profile for this grid; no global map-name or coordinate guesses.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMU3DElevationComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public ProtoId<CMU3DElevationPrototype> Profile;
}
