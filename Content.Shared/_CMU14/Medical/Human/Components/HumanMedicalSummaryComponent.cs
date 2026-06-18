using Content.Shared._CMU14.Medical.Human.Data;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Human.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class HumanMedicalSummaryComponent : Component
{
    [DataField, AutoNetworkedField]
    public MedicalSummary Summary;
}
