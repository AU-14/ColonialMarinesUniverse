using Content.Shared.Body.Part;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Surgery;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUSurgeryWindowOpenComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Patient;

    [DataField, AutoNetworkedField]
    public BodyPartType TargetPartType;

    [DataField, AutoNetworkedField]
    public BodyPartSymmetry TargetSymmetry;
}
