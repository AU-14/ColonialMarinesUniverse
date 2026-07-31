using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Treatment.FieldCare;

[Serializable, NetSerializable]
public enum CMUFieldTreatmentFamily : byte
{
    Hemostatic = 0,
    Antiseptic,
    BurnGel,
    TissueSealant,
    TraumaFoam,
}

[Serializable, NetSerializable]
public enum CMUFieldTreatmentBaseKind : byte
{
    Gauze = 0,
    TraumaDressing,
}
