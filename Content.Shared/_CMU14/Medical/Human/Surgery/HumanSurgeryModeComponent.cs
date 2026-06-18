using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Human.Surgery;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true), UnsavedComponent]
[Access(typeof(HumanSurgeryModeSystem))]
public sealed partial class HumanSurgeryModeComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled;
}
