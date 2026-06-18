using System;
using Content.Shared._CMU14.Medical.Human.Data;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Foundation;

[Serializable, NetSerializable]
public enum MedicalActionKind : byte
{
    None = 0,
    ApplyGauze,
    ApplySalve,
    ApplySplint,
    ApplyCast,
    ApplySuture,
    ApplyClamp,
    ApplyTourniquet,
    RemoveTourniquet,
    ApplySyntheticGraft,
    ApplySurgicalLine,
    Incise,
    Retract,
    CloseIncision,
    RepairInternalBleeding,
    RepairOrgan,
    SetBone,
    Amputate,
    ReattachLimb,
    FitProsthetic,
    DebrideEschar,
    RemoveShrapnel,
    RemoveOrgan,
    InsertOrgan,
    Scan,
    Stethoscope,
    Defibrillate,
    StabilizeOrgan,
}

[Serializable, NetSerializable]
public enum MedicalActionTargetKind : byte
{
    None = 0,
    Patient,
    Region,
    Organ,
    DetachedLimb,
    Machine,
}

[Serializable, NetSerializable]
public enum MedicalActionSourceKind : byte
{
    None = 0,
    HandItem,
    SurgeryTool,
    Machine,
    Chemical,
    Trauma,
}

[Serializable, NetSerializable]
public enum MedicalActionOutcome : byte
{
    None = 0,
    Accepted,
    RequiresDoAfter,
    Rejected,
    MissingLedger,
    InvalidTarget,
    WrongTool,
    BlockedByState,
    NoEffect,
}

[Flags, Serializable, NetSerializable]
public enum MedicalActionFlags : ushort
{
    None = 0,
    RequiresBodyPartPicker = 1 << 0,
    RequiresDoAfter = 1 << 1,
    RequiresDeepAccess = 1 << 3,
    StopsBleeding = 1 << 5,
    SuppressesBleeding = 1 << 6,
    RequiresFollowupTreatment = 1 << 7,
    DirtySummary = 1 << 8,
}

public readonly record struct MedicalActionRequest(
    EntityUid Actor,
    EntityUid Patient,
    EntityUid? Tool,
    MedicalActionKind Kind,
    MedicalActionSourceKind Source,
    MedicalActionTargetKind Target,
    BodyRegion Region = BodyRegion.None,
    OrganSlot OrganSlot = OrganSlot.None,
    int LedgerRevision = 0);

[Serializable, NetSerializable]
public readonly record struct MedicalActionResult(
    MedicalActionOutcome Outcome,
    MedicalActionKind Kind,
    MedicalActionFlags Flags,
    string FailureReason = "");
