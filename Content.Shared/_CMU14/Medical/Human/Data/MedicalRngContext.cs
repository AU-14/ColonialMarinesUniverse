namespace Content.Shared._CMU14.Medical.Human.Data;

public readonly record struct MedicalRngContext(
    float BoneRoll = 1f,
    float OrganRoll = 1f,
    float VascularRoll = 1f,
    float SurgeryRoll = 1f,
    float TreatmentRoll = 1f);
