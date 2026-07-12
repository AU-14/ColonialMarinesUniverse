using Content.Shared.Body.Part;

namespace Content.Server._CMU14.Medical.Treatment.Surgery;

[RegisterComponent]
public sealed partial class CMUSurgeryWindowOpenComponent : Component
{
    [DataField]
    public EntityUid Patient;

    [DataField]
    public BodyPartType TargetPartType;

    [DataField]
    public BodyPartSymmetry TargetSymmetry;

    [ViewVariables]
    public ulong ViewRevision;
}
