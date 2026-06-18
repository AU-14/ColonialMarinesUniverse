using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Human.Care;

[Serializable, NetSerializable]
public enum TreatmentKind : byte
{
    Gauze = 0,
    Ointment,
    Salve,
    Splint,
    Cast,
    ClampBleed,
    ApplyTourniquet,
    RemoveTourniquet,
    Suture,
    RepairOrgan,
    CloseIncision,
    ReattachLimb,
    FitProsthetic,
    TemporaryBleedSuppression,
    SurgicalLine,
    SyntheticGraft,
    RepairProstheticBrute,
    RepairProstheticBurn,
    RepairProstheticComposite,
}

[Serializable, NetSerializable]
public readonly record struct TreatmentAttempt(
    TreatmentKind Kind,
    BodyRegion Region,
    OrganSlot OrganSlot = OrganSlot.None,
    int InjuryId = 0,
    int BleedSourceId = 0,
    FixedPoint2 Amount = default,
    EntProtoId? RefundOnRemove = null);

public readonly record struct TreatmentResult(
    bool Applied,
    MedicalDirtyFlags DirtyFlags,
    string FailureReason);

[Serializable, NetSerializable]
public enum TreatmentEffectKind : byte
{
    None = 0,
    UpdateBleedSource,
    UpdateInjury,
    SetSkeletalSplinted,
    StartBoneKnitting,
    SetTourniquet,
    ReduceBurnDamage,
    RepairOrgan,
    StartInjuryRecovery,
    ReduceInjuryDamage,
    RepairRegionDamage,
}

public readonly record struct TreatmentEffect
{
    public readonly TreatmentEffectKind Kind;
    public readonly BodyRegion Region;
    public readonly OrganSlot OrganSlot;
    public readonly int InjuryId;
    public readonly int BleedSourceId;
    public readonly FixedPoint2 Amount;
    public readonly InjuryKind InjuryKind;
    public readonly InjuryFlags InjuryFlags;
    public readonly InjuryFlags ClearInjuryFlags;
    public readonly TreatmentFlags TreatmentFlags;
    public readonly bool SetBleedRate;
    public readonly FixedPoint2 BleedRate;
    public readonly bool Splinted;
    public readonly bool TourniquetApplied;
    public readonly EntProtoId? RefundOnRemove;

    private TreatmentEffect(
        TreatmentEffectKind kind,
        BodyRegion region,
        OrganSlot organSlot,
        int injuryId,
        int bleedSourceId,
        FixedPoint2 amount,
        InjuryKind injuryKind,
        InjuryFlags injuryFlags,
        InjuryFlags clearInjuryFlags,
        TreatmentFlags treatmentFlags,
        bool setBleedRate,
        FixedPoint2 bleedRate,
        bool splinted,
        bool tourniquetApplied,
        EntProtoId? refundOnRemove)
    {
        Kind = kind;
        Region = region;
        OrganSlot = organSlot;
        InjuryId = injuryId;
        BleedSourceId = bleedSourceId;
        Amount = amount;
        InjuryKind = injuryKind;
        InjuryFlags = injuryFlags;
        ClearInjuryFlags = clearInjuryFlags;
        TreatmentFlags = treatmentFlags;
        SetBleedRate = setBleedRate;
        BleedRate = bleedRate;
        Splinted = splinted;
        TourniquetApplied = tourniquetApplied;
        RefundOnRemove = refundOnRemove;
    }

    public static TreatmentEffect UpdateBleedSource(
        BodyRegion region,
        int bleedSourceId,
        TreatmentFlags treatmentFlags,
        bool setBleedRate,
        FixedPoint2 bleedRate)
    {
        return Create(
            TreatmentEffectKind.UpdateBleedSource,
            region,
            bleedSourceId: bleedSourceId,
            treatmentFlags: treatmentFlags,
            setBleedRate: setBleedRate,
            bleedRate: bleedRate);
    }

    public static TreatmentEffect UpdateInjury(
        BodyRegion region,
        int injuryId,
        InjuryFlags injuryFlags,
        InjuryFlags clearInjuryFlags = InjuryFlags.None)
    {
        return Create(
            TreatmentEffectKind.UpdateInjury,
            region,
            injuryId: injuryId,
            injuryFlags: injuryFlags,
            clearInjuryFlags: clearInjuryFlags);
    }

    public static TreatmentEffect SetSkeletalSplinted(BodyRegion region, bool splinted)
    {
        return Create(
            TreatmentEffectKind.SetSkeletalSplinted,
            region,
            splinted: splinted);
    }

    public static TreatmentEffect StartBoneKnitting(BodyRegion region, FixedPoint2 seconds)
    {
        return Create(
            TreatmentEffectKind.StartBoneKnitting,
            region,
            amount: seconds,
            treatmentFlags: TreatmentFlags.Splinted,
            splinted: true);
    }

    public static TreatmentEffect SetTourniquet(
        BodyRegion region,
        bool applied,
        FixedPoint2 necrosisSeconds,
        EntProtoId? refundOnRemove)
    {
        return Create(
            TreatmentEffectKind.SetTourniquet,
            region,
            amount: necrosisSeconds,
            tourniquetApplied: applied,
            refundOnRemove: refundOnRemove);
    }

    public static TreatmentEffect ReduceBurnDamage(BodyRegion region, FixedPoint2 amount)
    {
        return Create(
            TreatmentEffectKind.ReduceBurnDamage,
            region,
            amount: amount);
    }

    public static TreatmentEffect RepairOrgan(OrganSlot organSlot, FixedPoint2 amount)
    {
        return Create(
            TreatmentEffectKind.RepairOrgan,
            organSlot: organSlot,
            amount: amount);
    }

    public static TreatmentEffect StartInjuryRecovery(
        BodyRegion region,
        int injuryId,
        FixedPoint2 recoveryRate)
    {
        return Create(
            TreatmentEffectKind.StartInjuryRecovery,
            region,
            injuryId: injuryId,
            amount: recoveryRate);
    }

    public static TreatmentEffect ReduceInjuryDamage(
        BodyRegion region,
        int injuryId,
        FixedPoint2 amount,
        InjuryFlags injuryFlags)
    {
        return Create(
            TreatmentEffectKind.ReduceInjuryDamage,
            region,
            injuryId: injuryId,
            amount: amount,
            injuryFlags: injuryFlags);
    }

    public static TreatmentEffect RepairRegionDamage(
        BodyRegion region,
        InjuryKind kind,
        FixedPoint2 amount)
    {
        return Create(
            TreatmentEffectKind.RepairRegionDamage,
            region,
            amount: amount,
            injuryKind: kind);
    }

    private static TreatmentEffect Create(
        TreatmentEffectKind kind,
        BodyRegion region = BodyRegion.None,
        OrganSlot organSlot = OrganSlot.None,
        int injuryId = 0,
        int bleedSourceId = 0,
        FixedPoint2 amount = default,
        InjuryKind injuryKind = default,
        InjuryFlags injuryFlags = default,
        InjuryFlags clearInjuryFlags = default,
        TreatmentFlags treatmentFlags = default,
        bool setBleedRate = false,
        FixedPoint2 bleedRate = default,
        bool splinted = false,
        bool tourniquetApplied = false,
        EntProtoId? refundOnRemove = null)
    {
        return new TreatmentEffect(
            kind,
            region,
            organSlot,
            injuryId,
            bleedSourceId,
            amount,
            injuryKind,
            injuryFlags,
            clearInjuryFlags,
            treatmentFlags,
            setBleedRate,
            bleedRate,
            splinted,
            tourniquetApplied,
            refundOnRemove);
    }
}

public readonly record struct TreatmentRuleResult(
    bool Applied,
    TreatmentEffect[] Effects,
    MedicalDirtyFlags DirtyFlags,
    string FailureReason);
