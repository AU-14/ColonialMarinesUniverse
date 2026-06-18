using System.Collections.Generic;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Human.Surgery;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, UnsavedComponent]
[Access(typeof(HumanSurgerySystem))]
public sealed partial class ActiveHumanSurgeryOperationComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<SurgeryOperationState> Operations = new();
}
