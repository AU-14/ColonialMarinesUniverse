using System;
using Content.Shared._CMU14.Medical.Human.Data;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Human.Surgery;

public enum SurgeryStepKind : byte
{
    OpenIncision = 0,
    RetractIncision = 1,
    DeepAccess = 2,
    RepairOrgan = 3,
    RepairFracture = 4,
    RepairInternalBleed = 5,
    RepairStump = 6,
    CloseIncision = 7,
    SutureWound = 8,
    RemoveForeignObject = 9,
    RemoveEschar = 10,
    MendBoneAccess = 11,
    CutEmbryoRoots = 12,
    RemoveEmbryo = 13,
    RepairEyes = 14,
    RepairBrainDamage = 15,
    SeverMuscles = 16,
    AmputateLimb = 17,
    PrepareIncision = 18,
    AttachBiologicalLimb = 19,
    FitProsthetic = 20,
    RemoveProsthetic = 21,
    ClampBleeders = 22,
    ApplyBoneGel = 23,
    SetBone = 24,
    SealBoneWithGel = 25,
    ApplyBoneGraft = 26,
    SetGraftedBone = 27,
    CancelAmputation = 28,
}

[Serializable, NetSerializable]
public enum SurgeryProcedureId : byte
{
    None = 0,
    SurgicalAccess,
    SutureWound,
    RemoveForeignObject,
    SealStump,
    RepairInternalBleeding,
    RemoveEschar,
    RepairOrgan,
    RepairFracture,
    CloseIncision,
    AlienEmbryoRemoval,
    EyeSurgery,
    BrainDamageSurgery,
    Amputation,
    ReattachLimb,
    FitProsthetic,
    RemoveProsthetic,
}

[Serializable, NetSerializable]
public enum SurgeryToolRole : byte
{
    None = 0,
    CutFlesh,
    InitialIncisionShortcut,
    ClampOrExtract,
    SetBone,
    SealBone,
    Retract,
    CloseIncision,
    RepairVessel,
    CutBone,
    RepairOrgan,
    TreatBurn,
    SutureWound,
    GraftBurn,
    Drill,
    AttachLimb,
    FitProsthetic,
    RemoveProsthetic,
}

[Serializable, NetSerializable]
public enum SurgeryPainRequirement : byte
{
    None = 0,
    Light,
    Medium,
    Heavy,
    Full,
}

[Serializable, NetSerializable]
public enum SurgeryToolQuality : byte
{
    Ideal = 0,
    Suboptimal,
    Substitute,
    BadSubstitute,
    Awful,
}

[Serializable, NetSerializable]
public enum SurgerySurfaceQuality : byte
{
    Ideal = 0,
    Adequate,
    Unsuited,
    Awful,
}

[Serializable, NetSerializable]
public readonly record struct SurgeryAttempt(
    BodyRegion Region,
    SurgeryStepKind Step,
    OrganSlot OrganSlot = OrganSlot.None,
    int InjuryId = 0,
    int BleedSourceId = 0,
    bool PatientAnesthetized = false,
    bool PatientPainkilled = false,
    SurgeryPainRequirement PainRequirement = SurgeryPainRequirement.Medium,
    SurgeryToolQuality ToolQuality = SurgeryToolQuality.Ideal,
    SurgerySurfaceQuality SurfaceQuality = SurgerySurfaceQuality.Ideal,
    int RequiredSurgerySkill = 1,
    bool LyingRequired = false,
    bool SelfOperable = false,
    TimeSpan BaseDelay = default,
    SurgeryProcedureId ProcedureId = SurgeryProcedureId.None,
    int StepIndex = 0,
    SurgeryToolRole ToolRole = SurgeryToolRole.None);

[DataDefinition, Serializable, NetSerializable]
public partial record struct SurgeryOperationState
{
    [DataField]
    public BodyRegion Region;

    [DataField]
    public SurgeryProcedureId ProcedureId;

    [DataField]
    public int StepIndex;

    [DataField]
    public bool Committed;

    public SurgeryOperationState(
        BodyRegion region,
        SurgeryProcedureId procedureId,
        int stepIndex,
        bool committed)
    {
        Region = region;
        ProcedureId = procedureId;
        StepIndex = stepIndex;
        Committed = committed;
    }
}

public readonly record struct SurgeryResult(
    bool Applied,
    MedicalDirtyFlags DirtyFlags,
    string FailureReason)
{
    public bool PainEventRequired { get; init; }
}
