using System;
using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared.FixedPoint;

namespace Content.Shared._CMU14.Medical.Human.Data;

public sealed class MedicalTransaction
{
    private MedicalEffect[] _effects = new MedicalEffect[4];

    public MedicalTransaction(BodyRegion primaryRegion)
    {
        PrimaryRegion = primaryRegion;
    }

    public BodyRegion PrimaryRegion { get; }

    public ReadOnlyMemory<MedicalEffect> Effects => _effects.AsMemory(0, Count);

    public int Count { get; private set; }

    public void Add(MedicalEffect effect)
    {
        if (Count == _effects.Length)
            Array.Resize(ref _effects, _effects.Length * 2);

        _effects[Count++] = effect;
    }
}

public readonly partial record struct MedicalEffect
{
    public readonly MedicalEffectKind Kind;
    public readonly BodyRegion Region;
    public readonly FixedPoint2 BruteDamage;
    public readonly FixedPoint2 BurnDamage;
    public readonly InjuryKind InjuryKind;
    public readonly InjuryStage InjuryStage;
    public readonly InjuryFlags InjuryFlags;
    public readonly FixedPoint2 InjuryDamage;
    public readonly FixedPoint2 InjuryFlagThreshold;
    public readonly FixedPoint2 HealingFloor;
    public readonly OrganSlot OrganSlot;
    public readonly FixedPoint2 OrganDamage;
    public readonly BleedKind BleedKind;
    public readonly BleedKind TargetBleedKind;
    public readonly BleedFlags BleedFlags;
    public readonly FixedPoint2 BleedRate;
    public readonly int SourceInjuryId;
    public readonly int ForeignObjectId;
    public readonly ForeignObjectKind ForeignObjectKind;
    public readonly ForeignObjectDepth ForeignObjectDepth;
    public readonly ForeignObjectFlags ForeignObjectFlags;
    public readonly int ForeignObjectFragments;
    public readonly float ForeignObjectSeverity;
    public readonly FixedPoint2 ForeignObjectMoveDamage;
    public readonly FixedPoint2 ForeignObjectMoveDamagePerFragment;
    public readonly float ForeignObjectExplosionChance;
    public readonly bool Broken;
    public readonly bool Splinted;
    public readonly FractureSeverity FractureSeverity;
    public readonly SkeletalStateFlags SkeletalFlagsToSet;
    public readonly SkeletalStateFlags SkeletalFlagsToClear;
    public readonly bool OrganStasis;
    public readonly LimbPresence Presence;
    public readonly IncisionDepth IncisionDepth;
    public readonly int DetachedLimbId;

    private MedicalEffect(
        MedicalEffectKind kind,
        BodyRegion region = default,
        FixedPoint2 bruteDamage = default,
        FixedPoint2 burnDamage = default,
        InjuryKind injuryKind = default,
        InjuryStage injuryStage = default,
        InjuryFlags injuryFlags = default,
        FixedPoint2 injuryDamage = default,
        FixedPoint2 injuryFlagThreshold = default,
        FixedPoint2 healingFloor = default,
        OrganSlot organSlot = default,
        FixedPoint2 organDamage = default,
        BleedKind bleedKind = default,
        BleedKind targetBleedKind = default,
        FixedPoint2 bleedRate = default,
        int sourceInjuryId = default,
        int foreignObjectId = default,
        ForeignObjectKind foreignObjectKind = default,
        ForeignObjectDepth foreignObjectDepth = default,
        ForeignObjectFlags foreignObjectFlags = ForeignObjectFlags.None,
        int foreignObjectFragments = default,
        float foreignObjectSeverity = default,
        FixedPoint2 foreignObjectMoveDamage = default,
        FixedPoint2 foreignObjectMoveDamagePerFragment = default,
        float foreignObjectExplosionChance = default,
        bool broken = default,
        bool splinted = default,
        FractureSeverity fractureSeverity = FractureSeverity.None,
        SkeletalStateFlags skeletalFlagsToSet = SkeletalStateFlags.None,
        SkeletalStateFlags skeletalFlagsToClear = SkeletalStateFlags.None,
        bool organStasis = default,
        LimbPresence presence = default,
        IncisionDepth incisionDepth = default,
        BleedFlags bleedFlags = BleedFlags.None,
        int detachedLimbId = default)
    {
        Kind = kind;
        Region = region;
        BruteDamage = bruteDamage;
        BurnDamage = burnDamage;
        InjuryKind = injuryKind;
        InjuryStage = injuryStage;
        InjuryFlags = injuryFlags;
        InjuryDamage = injuryDamage;
        InjuryFlagThreshold = injuryFlagThreshold;
        HealingFloor = healingFloor;
        OrganSlot = organSlot;
        OrganDamage = organDamage;
        BleedKind = bleedKind;
        TargetBleedKind = targetBleedKind;
        BleedFlags = bleedFlags;
        BleedRate = bleedRate;
        SourceInjuryId = sourceInjuryId;
        ForeignObjectId = foreignObjectId;
        ForeignObjectKind = foreignObjectKind;
        ForeignObjectDepth = foreignObjectDepth;
        ForeignObjectFlags = foreignObjectFlags;
        ForeignObjectFragments = foreignObjectFragments;
        ForeignObjectSeverity = foreignObjectSeverity;
        ForeignObjectMoveDamage = foreignObjectMoveDamage;
        ForeignObjectMoveDamagePerFragment = foreignObjectMoveDamagePerFragment;
        ForeignObjectExplosionChance = foreignObjectExplosionChance;
        Broken = broken;
        Splinted = splinted;
        FractureSeverity = fractureSeverity;
        SkeletalFlagsToSet = skeletalFlagsToSet;
        SkeletalFlagsToClear = skeletalFlagsToClear;
        OrganStasis = organStasis;
        Presence = presence;
        IncisionDepth = incisionDepth;
        DetachedLimbId = detachedLimbId;
    }

    public static MedicalEffect AddRegionDamage(
        BodyRegion region,
        FixedPoint2 bruteDamage,
        FixedPoint2 burnDamage)
    {
        return new MedicalEffect(
            MedicalEffectKind.AddRegionDamage,
            region,
            bruteDamage: bruteDamage,
            burnDamage: burnDamage);
    }

    public static MedicalEffect RepairRegionDamage(
        BodyRegion region,
        InjuryKind kind,
        FixedPoint2 amount,
        FixedPoint2 healingFloor = default)
    {
        return new MedicalEffect(
            MedicalEffectKind.RepairRegionDamage,
            region,
            injuryKind: kind,
            injuryDamage: amount,
            healingFloor: healingFloor);
    }

    public static MedicalEffect AddInjury(
        BodyRegion region,
        InjuryKind kind,
        InjuryStage stage,
        FixedPoint2 damage,
        InjuryFlags flags = InjuryFlags.None,
        FixedPoint2 flagThreshold = default)
    {
        return new MedicalEffect(
            MedicalEffectKind.AddInjury,
            region,
            injuryKind: kind,
            injuryStage: stage,
            injuryFlags: flags,
            injuryDamage: damage,
            injuryFlagThreshold: flagThreshold);
    }

    public static MedicalEffect SetSkeletalState(
        BodyRegion region,
        bool broken,
        bool splinted,
        FractureSeverity severity = FractureSeverity.None)
    {
        return new MedicalEffect(
            MedicalEffectKind.SetSkeletalState,
            region,
            broken: broken,
            splinted: splinted,
            fractureSeverity: severity);
    }

    public static MedicalEffect UpdateSkeletalFlags(
        BodyRegion region,
        SkeletalStateFlags setFlags,
        SkeletalStateFlags clearFlags = SkeletalStateFlags.None)
    {
        return new MedicalEffect(
            MedicalEffectKind.UpdateSkeletalFlags,
            region,
            skeletalFlagsToSet: setFlags,
            skeletalFlagsToClear: clearFlags);
    }

    public static MedicalEffect AddOrganDamage(
        OrganSlot organSlot,
        FixedPoint2 damage)
    {
        return new MedicalEffect(
            MedicalEffectKind.AddOrganDamage,
            organSlot: organSlot,
            organDamage: damage);
    }

    public static MedicalEffect AddBleedSource(
        BodyRegion region,
        BleedKind kind,
        FixedPoint2 rate,
        int sourceInjuryId = 0,
        BleedFlags flags = BleedFlags.None)
    {
        return new MedicalEffect(
            MedicalEffectKind.AddBleedSource,
            region,
            bleedKind: kind,
            bleedRate: rate,
            sourceInjuryId: sourceInjuryId,
            bleedFlags: flags);
    }

    public static MedicalEffect SetRegionPresence(
        BodyRegion region,
        LimbPresence presence)
    {
        return new MedicalEffect(
            MedicalEffectKind.SetRegionPresence,
            region,
            presence: presence);
    }

    public static MedicalEffect SetIncisionDepth(
        BodyRegion region,
        IncisionDepth incisionDepth)
    {
        return new MedicalEffect(
            MedicalEffectKind.SetIncisionDepth,
            region,
            incisionDepth: incisionDepth);
    }

    public static MedicalEffect AddDetachedLimb(BodyRegion region)
    {
        return new MedicalEffect(
            MedicalEffectKind.AddDetachedLimb,
            region);
    }

    public static MedicalEffect MarkDetachedLimbReattached(BodyRegion region, int detachedLimbId = 0)
    {
        return new MedicalEffect(
            MedicalEffectKind.MarkDetachedLimbReattached,
            region,
            detachedLimbId: detachedLimbId);
    }

    public static MedicalEffect CloseStumpRecords(BodyRegion region)
    {
        return new MedicalEffect(
            MedicalEffectKind.CloseStumpRecords,
            region);
    }

    public static MedicalEffect SetOrganMissing(
        OrganSlot organSlot,
        bool missing)
    {
        return new MedicalEffect(
            MedicalEffectKind.SetOrganMissing,
            organSlot: organSlot,
            presence: missing ? LimbPresence.Missing : LimbPresence.Present);
    }

    public static MedicalEffect SetOrganStasis(
        OrganSlot organSlot,
        bool stasis)
    {
        return new MedicalEffect(
            MedicalEffectKind.SetOrganStasis,
            organSlot: organSlot,
            organStasis: stasis);
    }

    public static MedicalEffect CloseBleedSources(
        BodyRegion region,
        BleedKind kind,
        BleedFlags requiredFlags = BleedFlags.None)
    {
        return new MedicalEffect(
            MedicalEffectKind.CloseBleedSources,
            region,
            bleedKind: kind,
            bleedFlags: requiredFlags);
    }

    public static MedicalEffect ConvertBleedSources(
        BodyRegion region,
        BleedKind fromKind,
        BleedKind toKind,
        BleedFlags requiredFlags = BleedFlags.None)
    {
        return new MedicalEffect(
            MedicalEffectKind.ConvertBleedSources,
            region,
            bleedKind: fromKind,
            targetBleedKind: toKind,
            bleedFlags: requiredFlags);
    }

    public static MedicalEffect AddForeignObject(
        BodyRegion region,
        ForeignObjectKind kind,
        int fragments,
        float severity,
        ForeignObjectDepth depth,
        FixedPoint2 moveDamage = default,
        FixedPoint2 moveDamagePerFragment = default,
        ForeignObjectFlags flags = ForeignObjectFlags.None,
        float explosionChance = 0f)
    {
        return new MedicalEffect(
            MedicalEffectKind.AddForeignObject,
            region,
            foreignObjectKind: kind,
            foreignObjectDepth: depth,
            foreignObjectFlags: flags,
            foreignObjectFragments: fragments,
            foreignObjectSeverity: severity,
            foreignObjectMoveDamage: moveDamage,
            foreignObjectMoveDamagePerFragment: moveDamagePerFragment,
            foreignObjectExplosionChance: explosionChance);
    }

    public static MedicalEffect RemoveForeignObject(
        BodyRegion region,
        int foreignObjectId,
        int fragments)
    {
        return new MedicalEffect(
            MedicalEffectKind.RemoveForeignObject,
            region,
            foreignObjectId: foreignObjectId,
            foreignObjectFragments: fragments);
    }
}

public readonly record struct MedicalTransactionResult(
    bool Applied,
    int Revision,
    MedicalDirtyFlags DirtyFlags,
    string FailureReason,
    FixedPoint2 BruteHealed = default,
    FixedPoint2 BurnHealed = default);
