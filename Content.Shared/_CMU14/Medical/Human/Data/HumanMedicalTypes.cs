using System;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Human.Data;

[Serializable, NetSerializable]
public enum BodyRegion : byte
{
    None = 0,
    Head,
    Chest,
    Groin,
    LeftArm,
    RightArm,
    LeftHand,
    RightHand,
    LeftLeg,
    RightLeg,
    LeftFoot,
    RightFoot,
}

[Serializable, NetSerializable]
public enum InjuryKind : byte
{
    Cut = 0,
    Puncture,
    Bruise,
    Burn,
    InternalBleed,
    Stump,
    SurgicalIncision,
}

[Serializable, NetSerializable]
public enum InjuryStage : byte
{
    None = 0,
    Tiny,
    Small,
    Moderate,
    Large,
    Deep,
    Flesh,
    Gaping,
    GapingBig,
    Massive,
    Huge,
    Monumental,
    Severe,
    Carbonised,
    InternalBleed,
    Stump,
}

[Serializable, NetSerializable]
public enum OrganSlot : byte
{
    None = 0,
    Brain,
    Heart,
    LeftLung,
    RightLung,
    Liver,
    Kidneys,
    Stomach,
    Eyes,
    Ears,
}

[Serializable, NetSerializable]
public enum OrganDamageStatus : byte
{
    None = 0,
    LittleBruised,
    Bruised,
    Broken,
}

[Serializable, NetSerializable]
public enum BleedKind : byte
{
    External = 0,
    Internal,
    Stump,
}

[Flags, Serializable, NetSerializable]
public enum BleedFlags : byte
{
    None = 0,
    Arterial = 1 << 0,
    Surgical = 1 << 1,
}

[Serializable, NetSerializable]
public enum BleedSeverity : byte
{
    None = 0,
    Trace,
    Light,
    Moderate,
    Heavy,
    Critical,
}

[Serializable, NetSerializable]
public enum ForeignObjectKind : byte
{
    Shrapnel = 0,
}

[Serializable, NetSerializable]
public enum ForeignObjectDepth : byte
{
    Surface = 0,
    Deep,
    Surgical,
}

[Flags, Serializable, NetSerializable]
public enum ForeignObjectFlags : byte
{
    None = 0,
    CanExplode = 1 << 0,
}

[Serializable, NetSerializable]
public enum LimbPresence : byte
{
    Present = 0,
    Missing,
    Detached,
    Prosthetic,
}

[Serializable, NetSerializable]
public enum IncisionDepth : byte
{
    Closed = 0,
    OpenSkin,
    Retracted,
    DeepAccess,
}

[Serializable, NetSerializable]
public enum HudStatus : byte
{
    Healthy = 0,
    Stable,
    Wounded,
    Serious,
    Critical,
}

[Flags, Serializable, NetSerializable]
public enum InjuryFlags : ushort
{
    None = 0,
    Bandaged = 1 << 0,
    Salved = 1 << 1,
    Clamped = 1 << 2,
    Sutured = 1 << 3,
    Disinfected = 1 << 4,
    Necrotic = 1 << 5,
    Surgical = 1 << 6,
    Closed = 1 << 7,
    Debrided = 1 << 8,
}

[Flags, Serializable, NetSerializable]
public enum SkeletalStateFlags : byte
{
    None = 0,
    Broken = 1 << 0,
    Splinted = 1 << 1,
    Knitting = 1 << 2,
    Malunion = 1 << 3,
    BoneGelApplied = 1 << 4,
    BoneSet = 1 << 5,
    BoneGrafted = 1 << 6,
}

[Flags, Serializable, NetSerializable]
public enum TourniquetStateFlags : byte
{
    None = 0,
    Applied = 1 << 0,
    Necrotic = 1 << 1,
}

[Flags, Serializable, NetSerializable]
public enum OrganFlags : ushort
{
    None = 0,
    Missing = 1 << 0,
    Stasis = 1 << 1,
    Failing = 1 << 2,
    Necrotic = 1 << 3,
    Synthetic = 1 << 4,
}

[Flags, Serializable, NetSerializable]
public enum TreatmentFlags : ushort
{
    None = 0,
    Bandaged = 1 << 0,
    Salved = 1 << 1,
    Splinted = 1 << 2,
    Clamped = 1 << 3,
    Sutured = 1 << 4,
    Repaired = 1 << 5,
    Painkilled = 1 << 6,
    Anesthetized = 1 << 7,
    Stasis = 1 << 8,
    Closed = 1 << 9,
    Tourniquetted = 1 << 10,
    TemporarilySuppressed = 1 << 11,
}

[Flags, Serializable, NetSerializable]
public enum MedicalDirtyFlags : ushort
{
    None = 0,
    Regions = 1 << 0,
    Injuries = 1 << 1,
    Skeletal = 1 << 2,
    Organs = 1 << 3,
    Bleeding = 1 << 4,
    DetachedLimbs = 1 << 5,
    Summary = 1 << 6,
    ForeignObjects = 1 << 7,
}

[Flags, Serializable, NetSerializable]
public enum MedicalAlertFlags : ushort
{
    None = 0,
    ActiveBleeding = 1 << 0,
    InternalBleeding = 1 << 1,
    BrokenUnsplintedLimb = 1 << 2,
    OrganDamage = 1 << 3,
    OpenStump = 1 << 4,
    OpenIncision = 1 << 5,
    MissingLimb = 1 << 6,
    Critical = 1 << 7,
    Tourniquet = 1 << 8,
    NecroticRegion = 1 << 9,
    CoreFracture = 1 << 10,
    SuppressedBleedingNeedsSurgery = 1 << 11,
    SevereBurn = 1 << 12,
}

[Flags, Serializable, NetSerializable]
public enum MedicalActivityFlags : ushort
{
    None = 0,
    ActiveBleeding = 1 << 0,
    ActiveOrganSymptoms = 1 << 1,
    ActiveBoneKnitting = 1 << 2,
    ActiveMedicalSummaryDirty = 1 << 3,
    ActiveTourniquet = 1 << 4,
    ActiveTreatedWoundHealing = 1 << 5,
    ActiveUnsplintedFractureRisk = 1 << 6,
    ActiveEmbeddedObjectMovement = 1 << 7,
}

[Serializable, NetSerializable]
public enum MedicalEffectKind : byte
{
    None = 0,
    AddRegionDamage,
    RepairRegionDamage,
    AddInjury,
    SetSkeletalState,
    AddOrganDamage,
    AddBleedSource,
    SetRegionPresence,
    SetIncisionDepth,
    AddDetachedLimb,
    MarkDetachedLimbReattached,
    CloseStumpRecords,
    SetOrganMissing,
    SetOrganStasis,
    CloseBleedSources,
    UpdateSkeletalFlags,
    ConvertBleedSources,
    AddForeignObject,
    RemoveForeignObject,
}
