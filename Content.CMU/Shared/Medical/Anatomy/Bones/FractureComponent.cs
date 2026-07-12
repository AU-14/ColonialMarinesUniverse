using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Anatomy.Bones;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedFractureSystem), typeof(SharedBoneSystem))]
public sealed partial class FractureComponent : Component
{
    [DataField, AutoNetworkedField]
    public FractureSeverity Severity = FractureSeverity.Hairline;

    /// <summary>
    ///     <see cref="AutoPausedField"/> so the value stays meaningful across round pauses.
    /// </summary>
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan AppearedAt;

    [DataField, AutoNetworkedField]
    public bool IsBleeding;

    /// <summary>
    ///     Distinguishes chest and groin trauma, which share the torso body part
    ///     but threaten different internal organs when the patient moves.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TargetBodyZone? SourceZone;

    [DataField, AutoPausedField]
    public TimeSpan NextMovementComplication;
}
