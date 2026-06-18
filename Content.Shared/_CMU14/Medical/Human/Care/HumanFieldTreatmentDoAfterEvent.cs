using Content.Shared._CMU14.Medical.Targeting;
using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Human.Care;

[Serializable, NetSerializable]
public sealed partial class HumanFieldTreatmentDoAfterEvent : DoAfterEvent
{
    [DataField]
    public TreatmentAttempt Attempt;

    [DataField]
    public MedicalActionKind Action;

    [DataField]
    public FixedPoint2 RecoveryAmount;

    [DataField]
    public bool ChainByZone;

    [DataField]
    public TargetBodyZone TargetZone;

    public HumanFieldTreatmentDoAfterEvent(
        TreatmentAttempt attempt,
        MedicalActionKind action,
        FixedPoint2 recoveryAmount,
        bool chainByZone,
        TargetBodyZone targetZone)
    {
        Attempt = attempt;
        Action = action;
        RecoveryAmount = recoveryAmount;
        ChainByZone = chainByZone;
        TargetZone = targetZone;
    }

    public HumanFieldTreatmentDoAfterEvent()
    {
    }

    public override DoAfterEvent Clone()
    {
        return new HumanFieldTreatmentDoAfterEvent(
            Attempt,
            Action,
            RecoveryAmount,
            ChainByZone,
            TargetZone);
    }
}
