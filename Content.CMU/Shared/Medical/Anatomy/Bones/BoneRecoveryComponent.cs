namespace Content.Shared.CMU14.Medical.Anatomy.Bones;

/// <summary>
/// Server-owned recovery work exists only while structural integrity is missing.
/// A fracture clearing does not finish this work. The deadline follows the part's
/// pause clock; suspension additionally preserves time through patient stasis,
/// patient-only pause and detachment.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(SharedBoneSystem))]
public sealed partial class BoneRecoveryComponent : Component
{
    [AutoPausedField]
    public TimeSpan DueAt;

    public TimeSpan Remaining;
    public bool Suspended;
}
