using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Human.Data;

[DataDefinition, Serializable, NetSerializable]
public partial record struct SkeletalState
{
    [DataField]
    public SkeletalStateFlags Flags;

    [DataField]
    public FixedPoint2 KnittingSecondsRemaining;

    [DataField]
    public FractureSeverity Severity;

    public bool Broken
    {
        readonly get => Flags.HasFlag(SkeletalStateFlags.Broken);
        set => SetFlag(SkeletalStateFlags.Broken, value);
    }

    public bool Splinted
    {
        readonly get => Flags.HasFlag(SkeletalStateFlags.Splinted);
        set => SetFlag(SkeletalStateFlags.Splinted, value);
    }

    public bool Knitting
    {
        readonly get => Flags.HasFlag(SkeletalStateFlags.Knitting);
        set => SetFlag(SkeletalStateFlags.Knitting, value);
    }

    public bool Malunion
    {
        readonly get => Flags.HasFlag(SkeletalStateFlags.Malunion);
        set => SetFlag(SkeletalStateFlags.Malunion, value);
    }

    public bool BoneGelApplied
    {
        readonly get => Flags.HasFlag(SkeletalStateFlags.BoneGelApplied);
        set => SetFlag(SkeletalStateFlags.BoneGelApplied, value);
    }

    public bool BoneSet
    {
        readonly get => Flags.HasFlag(SkeletalStateFlags.BoneSet);
        set => SetFlag(SkeletalStateFlags.BoneSet, value);
    }

    public bool BoneGrafted
    {
        readonly get => Flags.HasFlag(SkeletalStateFlags.BoneGrafted);
        set => SetFlag(SkeletalStateFlags.BoneGrafted, value);
    }

    private void SetFlag(SkeletalStateFlags flag, bool enabled)
    {
        if (enabled)
            Flags |= flag;
        else
            Flags &= ~flag;
    }
}

[DataDefinition, Serializable, NetSerializable]
public partial record struct TourniquetState
{
    [DataField]
    public TourniquetStateFlags Flags;

    [DataField]
    public FixedPoint2 NecrosisSecondsRemaining;

    [DataField]
    public EntProtoId? RefundOnRemove;

    public bool Applied
    {
        readonly get => Flags.HasFlag(TourniquetStateFlags.Applied);
        set => SetFlag(TourniquetStateFlags.Applied, value);
    }

    public bool Necrotic
    {
        readonly get => Flags.HasFlag(TourniquetStateFlags.Necrotic);
        set => SetFlag(TourniquetStateFlags.Necrotic, value);
    }

    private void SetFlag(TourniquetStateFlags flag, bool enabled)
    {
        if (enabled)
            Flags |= flag;
        else
            Flags &= ~flag;
    }
}

[DataDefinition, Serializable, NetSerializable]
public partial record struct RegionState
{
    [DataField]
    public BodyRegion Region;

    [DataField]
    public LimbPresence Presence;

    [DataField]
    public FixedPoint2 BruteDamage;

    [DataField]
    public FixedPoint2 BurnDamage;

    [DataField]
    public SkeletalState Skeletal;

    [DataField]
    public IncisionDepth Incision;

    [DataField]
    public TourniquetState Tourniquet;

    public RegionState(BodyRegion region)
    {
        Region = region;
        Presence = LimbPresence.Present;
        BruteDamage = FixedPoint2.Zero;
        BurnDamage = FixedPoint2.Zero;
        Skeletal = default;
        Incision = IncisionDepth.Closed;
        Tourniquet = default;
    }
}

[DataDefinition, Serializable, NetSerializable]
public partial record struct InjuryRecord
{
    [DataField]
    public int Id;

    [DataField]
    public BodyRegion Region;

    [DataField]
    public InjuryKind Kind;

    [DataField]
    public InjuryStage Stage;

    [DataField]
    public FixedPoint2 Damage;

    [DataField]
    public FixedPoint2 RecoveryRate;

    [DataField]
    public InjuryFlags Flags;

    public readonly bool IsOpenStump =>
        Kind == InjuryKind.Stump &&
        !Flags.HasFlag(InjuryFlags.Sutured) &&
        !Flags.HasFlag(InjuryFlags.Closed);
}

[DataDefinition, Serializable, NetSerializable]
public partial record struct OrganState
{
    [DataField]
    public OrganSlot Slot;

    [DataField]
    public BodyRegion Region;

    [DataField]
    public FixedPoint2 Damage;

    [DataField]
    public OrganDamageStatus Status;

    [DataField]
    public OrganFlags Flags;

    public OrganState(OrganSlot slot, BodyRegion region)
    {
        Slot = slot;
        Region = region;
        Damage = FixedPoint2.Zero;
        Status = OrganDamageStatus.None;
        Flags = OrganFlags.None;
    }

    public readonly bool Missing => Flags.HasFlag(OrganFlags.Missing);

    public readonly bool Symptomatic =>
        Status != OrganDamageStatus.None &&
        !Missing &&
        !Flags.HasFlag(OrganFlags.Stasis);
}

[DataDefinition, Serializable, NetSerializable]
public partial record struct BleedSource
{
    [DataField]
    public int Id;

    [DataField]
    public BodyRegion Region;

    [DataField]
    public BleedKind Kind;

    [DataField]
    public BleedFlags Flags;

    [DataField]
    public FixedPoint2 Rate;

    [DataField]
    public int SourceInjuryId;

    [DataField]
    public TreatmentFlags Treatment;

    public readonly bool Active =>
        Rate > FixedPoint2.Zero &&
        !Treatment.HasFlag(TreatmentFlags.Clamped) &&
        !Treatment.HasFlag(TreatmentFlags.Sutured) &&
        !Treatment.HasFlag(TreatmentFlags.Closed) &&
        !Treatment.HasFlag(TreatmentFlags.Tourniquetted) &&
        !Treatment.HasFlag(TreatmentFlags.TemporarilySuppressed);
}

[DataDefinition, Serializable, NetSerializable]
public partial record struct ForeignObjectRecord
{
    [DataField]
    public int Id;

    [DataField]
    public BodyRegion Region;

    [DataField]
    public ForeignObjectKind Kind;

    [DataField]
    public ForeignObjectDepth Depth;

    [DataField]
    public ForeignObjectFlags Flags;

    [DataField]
    public int Fragments;

    [DataField]
    public float Severity;

    [DataField]
    public FixedPoint2 MoveDamage;

    [DataField]
    public FixedPoint2 MoveDamagePerFragment;

    [DataField]
    public float ExplosionChance;

    public readonly bool Active => Fragments > 0;
}

[DataDefinition, Serializable, NetSerializable]
public partial record struct DetachedLimbRecord
{
    [DataField]
    public int Id;

    [DataField]
    public BodyRegion Region;

    [DataField]
    public bool Preserved;

    [DataField]
    public bool Reattached;
}

[DataDefinition, Serializable, NetSerializable]
public partial record struct MedicalSummary
{
    [DataField]
    public int Revision;

    [DataField]
    public HudStatus HudStatus;

    [DataField]
    public MedicalAlertFlags Alerts;

    [DataField]
    public BleedSeverity WorstBleed;

    [DataField]
    public float WalkingSlowdownPoints;

    [DataField]
    public float WheelchairSlowdownPoints;

    [DataField]
    public bool HasInternalBleeding;

    [DataField]
    public bool HasBrokenUnsplintedLimb;

    [DataField]
    public bool HasOrganDamage;

    [DataField]
    public bool HasOpenStump;

    [DataField]
    public bool HasOpenIncision;

    [DataField]
    public bool HasTourniquet;

    [DataField]
    public bool HasNecroticRegion;

    [DataField]
    public bool HasCoreFracture;

    [DataField]
    public bool HasSuppressedBleeding;

    [DataField]
    public bool HasSevereBurn;
}
