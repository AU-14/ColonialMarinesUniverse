using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Diagnostics;
using Content.Shared._CMU14.Medical.Human.Rules;
using Content.Shared._CMU14.Medical.Human.Care;
using Content.Shared.FixedPoint;

namespace Content.Shared._CMU14.Medical.Human.Systems;

public static class HumanMedicalLedger
{
    private static readonly BodyRegion[] DefaultRegions =
    {
        BodyRegion.Head,
        BodyRegion.Chest,
        BodyRegion.Groin,
        BodyRegion.LeftArm,
        BodyRegion.RightArm,
        BodyRegion.LeftHand,
        BodyRegion.RightHand,
        BodyRegion.LeftLeg,
        BodyRegion.RightLeg,
        BodyRegion.LeftFoot,
        BodyRegion.RightFoot,
    };

    private static readonly OrganState[] DefaultOrgans =
    {
        new(OrganSlot.Brain, BodyRegion.Head),
        new(OrganSlot.Heart, BodyRegion.Chest),
        new(OrganSlot.LeftLung, BodyRegion.Chest),
        new(OrganSlot.RightLung, BodyRegion.Chest),
        new(OrganSlot.Liver, BodyRegion.Chest),
        new(OrganSlot.Kidneys, BodyRegion.Groin),
        new(OrganSlot.Stomach, BodyRegion.Chest),
        new(OrganSlot.Eyes, BodyRegion.Head),
        new(OrganSlot.Ears, BodyRegion.Head),
    };

    public static HumanMedicalComponent CreateDefault()
    {
        var medical = new HumanMedicalComponent
        {
            Regions = CreateRegionArray(),
            Organs = CreateOrganArray(),
        };

        medical.Summary = MedicalSummaryBuilder.Build(medical);
        return medical;
    }

    public static void EnsureInitialized(HumanMedicalComponent medical)
    {
        if (medical.Regions.Length != HumanMedicalComponent.RegionSlotCount)
            medical.Regions = CreateRegionArray();
        else
            InitializeMissingRegions(medical.Regions);

        if (medical.Organs.Length != HumanMedicalComponent.OrganSlotCount)
            medical.Organs = CreateOrganArray();
        else
            InitializeMissingOrgans(medical.Organs);

        medical.Injuries ??= new List<InjuryRecord>();
        medical.BleedSources ??= new List<BleedSource>();
        medical.ForeignObjects ??= new List<ForeignObjectRecord>();
        medical.DetachedLimbs ??= new List<DetachedLimbRecord>();

        if (medical.Summary.Revision == 0 && medical.DirtyFlags == MedicalDirtyFlags.None)
            medical.Summary = MedicalSummaryBuilder.Build(medical);
    }

    public static void ResetToHealthy(HumanMedicalComponent medical)
    {
        medical.Regions = CreateRegionArray();
        medical.Organs = CreateOrganArray();
        medical.Injuries = new List<InjuryRecord>();
        medical.BleedSources = new List<BleedSource>();
        medical.ForeignObjects = new List<ForeignObjectRecord>();
        medical.DetachedLimbs = new List<DetachedLimbRecord>();
        medical.NextInjuryId = 1;
        medical.NextBleedSourceId = 1;
        medical.NextForeignObjectId = 1;
        medical.NextDetachedLimbId = 1;
        medical.Revision++;
        medical.DirtyFlags |= MedicalDirtyFlags.Regions
            | MedicalDirtyFlags.Injuries
            | MedicalDirtyFlags.Skeletal
            | MedicalDirtyFlags.Organs
            | MedicalDirtyFlags.Bleeding
            | MedicalDirtyFlags.ForeignObjects
            | MedicalDirtyFlags.DetachedLimbs
            | MedicalDirtyFlags.Summary;
    }

    public static MedicalTransactionResult ApplyTransaction(
        HumanMedicalComponent medical,
        MedicalTransaction transaction)
    {
        if (transaction.Count == 0)
            return new MedicalTransactionResult(false, medical.Revision, MedicalDirtyFlags.None, "Transaction has no effects.");

        EnsureInitialized(medical);
        var stage = new LedgerStage(medical);
        var dirty = MedicalDirtyFlags.None;
        var effects = transaction.Effects.Span;

        for (var i = 0; i < effects.Length; i++)
        {
            ref readonly var effect = ref effects[i];
            if (!TryApplyEffect(stage, in effect, out var effectDirty, out var failureReason))
                return new MedicalTransactionResult(false, medical.Revision, MedicalDirtyFlags.None, failureReason);

            dirty |= effectDirty;
        }

        if (dirty == MedicalDirtyFlags.None)
            return new MedicalTransactionResult(false, medical.Revision, MedicalDirtyFlags.None, "Transaction did not change the ledger.");

        dirty |= MedicalDirtyFlags.Summary;
        CommitStage(medical, stage, dirty);

        return new MedicalTransactionResult(true, medical.Revision, dirty, string.Empty);
    }

    public static bool RebuildSummaryIfDirty(HumanMedicalComponent medical)
    {
        if (!medical.DirtyFlags.HasFlag(MedicalDirtyFlags.Summary))
            return false;

        medical.Summary = MedicalSummaryBuilder.Build(medical);
        medical.DirtyFlags &= ~MedicalDirtyFlags.Summary;
        return true;
    }

    public static MedicalTransactionResult AdvanceBoneKnitting(
        HumanMedicalComponent medical,
        FixedPoint2 seconds)
    {
        if (seconds <= FixedPoint2.Zero)
            return new MedicalTransactionResult(false, medical.Revision, MedicalDirtyFlags.None, "Bone knitting tick must be positive.");

        EnsureInitialized(medical);
        var regions = (RegionState[]) medical.Regions.Clone();
        var changed = false;

        for (var i = 1; i < regions.Length; i++)
        {
            var region = regions[i];
            if (!region.Skeletal.Knitting)
                continue;

            var skeletal = region.Skeletal;
            if (skeletal.KnittingSecondsRemaining > seconds)
            {
                skeletal.KnittingSecondsRemaining -= seconds;
            }
            else
            {
                skeletal.KnittingSecondsRemaining = FixedPoint2.Zero;
                skeletal.Knitting = false;
                skeletal.Broken = false;
                skeletal.Malunion = false;
            }

            region.Skeletal = skeletal;
            regions[i] = region;
            changed = true;
        }

        if (!changed)
            return new MedicalTransactionResult(false, medical.Revision, MedicalDirtyFlags.None, "No bones are knitting.");

        medical.Regions = regions;
        medical.Revision++;
        medical.DirtyFlags |= MedicalDirtyFlags.Skeletal | MedicalDirtyFlags.Summary;

        return new MedicalTransactionResult(true, medical.Revision, MedicalDirtyFlags.Skeletal | MedicalDirtyFlags.Summary, string.Empty);
    }

    public static MedicalTransactionResult AdvanceTreatedWoundHealing(
        HumanMedicalComponent medical,
        FixedPoint2 seconds)
    {
        if (seconds <= FixedPoint2.Zero)
            return new MedicalTransactionResult(false, medical.Revision, MedicalDirtyFlags.None, "Treated wound healing tick must be positive.");

        EnsureInitialized(medical);
        var regions = (RegionState[]) medical.Regions.Clone();
        var injuries = new List<InjuryRecord>(medical.Injuries);
        var changed = false;
        var bruteHealed = FixedPoint2.Zero;
        var burnHealed = FixedPoint2.Zero;

        for (var regionIndex = 1; regionIndex < regions.Length; regionIndex++)
        {
            var region = regions[regionIndex];
            if (region.Region == BodyRegion.None)
                continue;

            CalculateTreatedRecoveryRates(
                medical,
                region.Region,
                out var bruteRecoveryBudget,
                out var burnRecoveryBudget,
                out _);

            bruteRecoveryBudget *= seconds;
            burnRecoveryBudget *= seconds;
            if (bruteRecoveryBudget <= FixedPoint2.Zero &&
                burnRecoveryBudget <= FixedPoint2.Zero)
            {
                continue;
            }

            for (var i = injuries.Count - 1; i >= 0; i--)
            {
                var injury = injuries[i];
                if (injury.Region != region.Region ||
                    !CanTreatedInjuryRecover(injury))
                {
                    continue;
                }

                var remainingBudget = injury.Kind == InjuryKind.Burn
                    ? burnRecoveryBudget
                    : bruteRecoveryBudget;
                var targetHeal = FixedPoint2.Min(injury.Damage, remainingBudget);
                if (targetHeal <= FixedPoint2.Zero)
                    continue;

                if (injury.Kind == InjuryKind.Burn)
                {
                    burnRecoveryBudget = FixedPoint2.Max(
                        FixedPoint2.Zero,
                        burnRecoveryBudget - targetHeal);
                    var regionHeal = FixedPoint2.Min(region.BurnDamage, targetHeal);
                    region.BurnDamage = FixedPoint2.Max(FixedPoint2.Zero, region.BurnDamage - regionHeal);
                    burnHealed += regionHeal;
                }
                else
                {
                    bruteRecoveryBudget = FixedPoint2.Max(
                        FixedPoint2.Zero,
                        bruteRecoveryBudget - targetHeal);
                    var regionHeal = FixedPoint2.Min(region.BruteDamage, targetHeal);
                    region.BruteDamage = FixedPoint2.Max(FixedPoint2.Zero, region.BruteDamage - regionHeal);
                    bruteHealed += regionHeal;
                }

                injury.Damage = FixedPoint2.Max(FixedPoint2.Zero, injury.Damage - targetHeal);
                if (injury.Damage <= FixedPoint2.Zero)
                {
                    if (CanRemoveRecoveredInjury(injury))
                    {
                        injuries.RemoveAt(i);
                    }
                    else
                    {
                        injury.RecoveryRate = FixedPoint2.Zero;
                        injuries[i] = injury;
                    }
                }
                else
                {
                    injury.Stage = InjuryRules.GetStage(injury.Kind, injury.Damage);
                    injuries[i] = injury;
                }

                changed = true;
            }

            regions[regionIndex] = region;
        }

        if (!changed)
            return new MedicalTransactionResult(false, medical.Revision, MedicalDirtyFlags.None, "No treated wounds are recovering.");

        medical.Regions = regions;
        medical.Injuries = injuries;
        medical.Revision++;
        medical.DirtyFlags |= MedicalDirtyFlags.Regions | MedicalDirtyFlags.Injuries | MedicalDirtyFlags.Summary;

        return new MedicalTransactionResult(
            true,
            medical.Revision,
            MedicalDirtyFlags.Regions | MedicalDirtyFlags.Injuries | MedicalDirtyFlags.Summary,
            string.Empty,
            bruteHealed,
            burnHealed);
    }

    public static void CalculateTreatedRecoveryRates(
        HumanMedicalComponent medical,
        BodyRegion region,
        out FixedPoint2 bruteRecoveryRate,
        out FixedPoint2 burnRecoveryRate,
        out int activeInjuries)
    {
        bruteRecoveryRate = FixedPoint2.Zero;
        burnRecoveryRate = FixedPoint2.Zero;
        activeInjuries = 0;
        foreach (var injury in medical.Injuries)
        {
            if (!CanTreatedInjuryRecover(injury) ||
                injury.Region != region)
            {
                continue;
            }

            activeInjuries++;
            if (injury.Kind == InjuryKind.Burn)
                burnRecoveryRate = FixedPoint2.Max(burnRecoveryRate, injury.RecoveryRate);
            else
                bruteRecoveryRate = FixedPoint2.Max(bruteRecoveryRate, injury.RecoveryRate);
        }
    }

    public static MedicalTransactionResult RepairAllSkeletalDamage(HumanMedicalComponent medical)
    {
        EnsureInitialized(medical);
        var regions = (RegionState[]) medical.Regions.Clone();
        var changed = false;

        for (var i = 1; i < regions.Length; i++)
        {
            var region = regions[i];
            if (region.Skeletal.Flags == SkeletalStateFlags.None &&
                region.Skeletal.KnittingSecondsRemaining <= FixedPoint2.Zero)
            {
                continue;
            }

            region.Skeletal = default;
            regions[i] = region;
            changed = true;
        }

        if (!changed)
            return new MedicalTransactionResult(false, medical.Revision, MedicalDirtyFlags.None, "No skeletal damage is present.");

        medical.Regions = regions;
        medical.Revision++;
        medical.DirtyFlags |= MedicalDirtyFlags.Skeletal | MedicalDirtyFlags.Summary;

        return new MedicalTransactionResult(true, medical.Revision, MedicalDirtyFlags.Skeletal | MedicalDirtyFlags.Summary, string.Empty);
    }

    public static MedicalTransactionResult AdvanceTourniquets(
        HumanMedicalComponent medical,
        FixedPoint2 seconds)
    {
        if (seconds <= FixedPoint2.Zero)
            return new MedicalTransactionResult(false, medical.Revision, MedicalDirtyFlags.None, "Tourniquet tick must be positive.");

        EnsureInitialized(medical);
        var regions = (RegionState[]) medical.Regions.Clone();
        var changed = false;

        for (var i = 1; i < regions.Length; i++)
        {
            var region = regions[i];
            if (!region.Tourniquet.Applied ||
                region.Tourniquet.Necrotic ||
                region.Tourniquet.NecrosisSecondsRemaining <= FixedPoint2.Zero)
            {
                continue;
            }

            var tourniquet = region.Tourniquet;
            if (tourniquet.NecrosisSecondsRemaining > seconds)
            {
                tourniquet.NecrosisSecondsRemaining -= seconds;
            }
            else
            {
                tourniquet.NecrosisSecondsRemaining = FixedPoint2.Zero;
                tourniquet.Necrotic = true;
            }

            region.Tourniquet = tourniquet;
            regions[i] = region;
            changed = true;
        }

        if (!changed)
            return new MedicalTransactionResult(false, medical.Revision, MedicalDirtyFlags.None, "No tourniquets are progressing.");

        medical.Regions = regions;
        medical.Revision++;
        medical.DirtyFlags |= MedicalDirtyFlags.Regions | MedicalDirtyFlags.Summary;

        return new MedicalTransactionResult(true, medical.Revision, MedicalDirtyFlags.Regions | MedicalDirtyFlags.Summary, string.Empty);
    }

    public static TreatmentResult ApplyTreatmentPlan(
        HumanMedicalComponent medical,
        TreatmentRuleResult plan)
    {
        if (!plan.Applied)
            return new TreatmentResult(false, MedicalDirtyFlags.None, plan.FailureReason);
        if (plan.Effects.Length == 0)
            return new TreatmentResult(false, MedicalDirtyFlags.None, "Treatment plan has no effects.");

        EnsureInitialized(medical);
        var stage = new LedgerStage(medical);
        var dirty = MedicalDirtyFlags.None;
        var effects = plan.Effects;

        for (var i = 0; i < effects.Length; i++)
        {
            ref readonly var effect = ref effects[i];
            if (!TryApplyTreatmentEffect(stage, in effect, out var effectDirty, out var failureReason))
                return new TreatmentResult(false, MedicalDirtyFlags.None, failureReason);

            dirty |= effectDirty;
        }

        if (dirty == MedicalDirtyFlags.None)
            return new TreatmentResult(false, MedicalDirtyFlags.None, "Treatment did not change the ledger.");

        dirty |= MedicalDirtyFlags.Summary;
        CommitStage(medical, stage, dirty);

        return new TreatmentResult(true, dirty, string.Empty);
    }

    public static RegionState GetRegion(HumanMedicalComponent medical, BodyRegion region)
    {
        return TryGetRegionIndex(medical.Regions, region, out var index)
            ? medical.Regions[index]
            : new RegionState(region);
    }

    public static OrganState GetOrgan(HumanMedicalComponent medical, OrganSlot slot)
    {
        return TryGetOrganIndex(medical.Organs, slot, out var index)
            ? medical.Organs[index]
            : new OrganState(slot, BodyRegion.None);
    }

    private static RegionState[] CreateRegionArray()
    {
        var regions = new RegionState[HumanMedicalComponent.RegionSlotCount];

        foreach (var region in DefaultRegions)
        {
            regions[(int) region] = new RegionState(region);
        }

        return regions;
    }

    private static OrganState[] CreateOrganArray()
    {
        var organs = new OrganState[HumanMedicalComponent.OrganSlotCount];

        foreach (var organ in DefaultOrgans)
        {
            organs[(int) organ.Slot] = organ;
        }

        return organs;
    }

    private static void InitializeMissingRegions(RegionState[] regions)
    {
        foreach (var region in DefaultRegions)
        {
            var index = (int) region;
            if (regions[index].Region == BodyRegion.None)
                regions[index] = new RegionState(region);
        }
    }

    private static void InitializeMissingOrgans(OrganState[] organs)
    {
        foreach (var organ in DefaultOrgans)
        {
            var index = (int) organ.Slot;
            if (organs[index].Slot == OrganSlot.None)
                organs[index] = organ;
        }
    }

    private static void CommitStage(
        HumanMedicalComponent medical,
        LedgerStage stage,
        MedicalDirtyFlags dirty)
    {
        medical.Regions = stage.Regions;
        medical.Injuries = stage.Injuries;
        medical.Organs = stage.Organs;
        medical.BleedSources = stage.BleedSources;
        medical.ForeignObjects = stage.ForeignObjects;
        medical.DetachedLimbs = stage.DetachedLimbs;
        medical.NextInjuryId = stage.NextInjuryId;
        medical.NextBleedSourceId = stage.NextBleedSourceId;
        medical.NextForeignObjectId = stage.NextForeignObjectId;
        medical.NextDetachedLimbId = stage.NextDetachedLimbId;
        medical.Revision++;
        medical.DirtyFlags |= dirty;
    }

    private static bool TryApplyEffect(
        LedgerStage stage,
        in MedicalEffect effect,
        out MedicalDirtyFlags dirty,
        out string failureReason)
    {
        dirty = MedicalDirtyFlags.None;
        failureReason = string.Empty;

        switch (effect.Kind)
        {
            case MedicalEffectKind.AddRegionDamage:
                return TryApplyRegionDamage(stage, in effect, out dirty, out failureReason);
            case MedicalEffectKind.RepairRegionDamage:
                return TryApplyRegionRepair(stage, in effect, out dirty, out failureReason);
            case MedicalEffectKind.AddInjury:
                return TryApplyInjury(stage, in effect, out dirty, out failureReason);
            case MedicalEffectKind.SetSkeletalState:
                return TryApplySkeletalState(stage, in effect, out dirty, out failureReason);
            case MedicalEffectKind.AddOrganDamage:
                return TryApplyOrganDamage(stage, in effect, out dirty, out failureReason);
            case MedicalEffectKind.AddBleedSource:
                return TryApplyBleedSource(stage, in effect, out dirty, out failureReason);
            case MedicalEffectKind.SetRegionPresence:
                return TryApplyRegionPresence(stage, in effect, out dirty, out failureReason);
            case MedicalEffectKind.SetIncisionDepth:
                return TryApplyIncisionDepth(stage, in effect, out dirty, out failureReason);
            case MedicalEffectKind.AddDetachedLimb:
                return TryApplyDetachedLimb(stage, in effect, out dirty, out failureReason);
            case MedicalEffectKind.MarkDetachedLimbReattached:
                return TryApplyDetachedLimbReattached(stage, in effect, out dirty, out failureReason);
            case MedicalEffectKind.CloseStumpRecords:
                return TryApplyCloseStumpRecords(stage, in effect, out dirty, out failureReason);
            case MedicalEffectKind.SetOrganMissing:
                return TryApplyOrganMissing(stage, in effect, out dirty, out failureReason);
            case MedicalEffectKind.SetOrganStasis:
                return TryApplyOrganStasis(stage, in effect, out dirty, out failureReason);
            case MedicalEffectKind.CloseBleedSources:
                return TryApplyCloseBleedSources(stage, in effect, out dirty, out failureReason);
            case MedicalEffectKind.UpdateSkeletalFlags:
                return TryApplySkeletalFlags(stage, in effect, out dirty, out failureReason);
            case MedicalEffectKind.ConvertBleedSources:
                return TryApplyConvertBleedSources(stage, in effect, out dirty, out failureReason);
            case MedicalEffectKind.AddForeignObject:
                return TryApplyAddForeignObject(stage, in effect, out dirty, out failureReason);
            case MedicalEffectKind.RemoveForeignObject:
                return TryApplyRemoveForeignObject(stage, in effect, out dirty, out failureReason);
            default:
                failureReason = $"Unsupported medical effect {effect.Kind}.";
                return false;
        }
    }

    private static bool TryApplyRegionDamage(
        LedgerStage stage,
        in MedicalEffect effect,
        out MedicalDirtyFlags dirty,
        out string failureReason)
    {
        dirty = MedicalDirtyFlags.None;
        if (!TryGetRegionIndex(stage.Regions, effect.Region, out var index))
        {
            failureReason = $"Transaction targets missing region {effect.Region}.";
            return false;
        }

        var region = stage.Regions[index];
        region.BruteDamage = FixedPoint2.Max(FixedPoint2.Zero, region.BruteDamage + effect.BruteDamage);
        region.BurnDamage = FixedPoint2.Max(FixedPoint2.Zero, region.BurnDamage + effect.BurnDamage);
        stage.Regions[index] = region;

        dirty = MedicalDirtyFlags.Regions;
        failureReason = string.Empty;
        return true;
    }

    private static bool TryApplyRegionRepair(
        LedgerStage stage,
        in MedicalEffect effect,
        out MedicalDirtyFlags dirty,
        out string failureReason)
    {
        dirty = MedicalDirtyFlags.None;
        if (effect.InjuryDamage <= FixedPoint2.Zero)
        {
            failureReason = "Region repair must have a positive amount.";
            return false;
        }

        if (!TryGetRegionIndex(stage.Regions, effect.Region, out var index))
        {
            failureReason = $"Transaction targets missing region {effect.Region}.";
            return false;
        }

        var region = stage.Regions[index];
        var floor = FixedPoint2.Max(FixedPoint2.Zero, effect.HealingFloor);
        var regionDamage = IsBurnRepair(effect.InjuryKind)
            ? region.BurnDamage
            : region.BruteDamage;
        var regionHeal = FixedPoint2.Min(
            effect.InjuryDamage,
            FixedPoint2.Max(FixedPoint2.Zero, regionDamage - floor));

        if (regionHeal <= FixedPoint2.Zero)
        {
            failureReason = string.Empty;
            return true;
        }

        if (IsBurnRepair(effect.InjuryKind))
            region.BurnDamage -= regionHeal;
        else
            region.BruteDamage -= regionHeal;

        stage.Regions[index] = region;
        dirty |= MedicalDirtyFlags.Regions;

        var injuryHeal = regionHeal;
        if (RepairMatchingInjuries(
                stage.Injuries,
                effect.Region,
                effect.InjuryKind,
                ref injuryHeal,
                floor))
        {
            dirty |= MedicalDirtyFlags.Injuries;
        }

        failureReason = string.Empty;
        return true;
    }

    private static bool TryApplyInjury(
        LedgerStage stage,
        in MedicalEffect effect,
        out MedicalDirtyFlags dirty,
        out string failureReason)
    {
        dirty = MedicalDirtyFlags.None;
        if (!TryGetRegionIndex(stage.Regions, effect.Region, out var index))
        {
            failureReason = $"Transaction targets missing region {effect.Region}.";
            return false;
        }

        if (TryWidenCompatibleInjury(stage, in effect, out dirty))
        {
            failureReason = string.Empty;
            return true;
        }

        if (stage.InjuryCounts[index] >= HumanMedicalComponent.MaxInjuriesPerRegion)
        {
            failureReason = $"Region {effect.Region} would exceed injury cap.";
            return false;
        }

        stage.InjuryCounts[index]++;
        stage.Injuries.Add(new InjuryRecord
        {
            Id = stage.NextInjuryId++,
            Region = effect.Region,
            Kind = effect.InjuryKind,
            Stage = effect.InjuryStage,
            Damage = effect.InjuryDamage,
            Flags = GetInitialInjuryFlags(in effect),
        });

        dirty = MedicalDirtyFlags.Injuries;
        failureReason = string.Empty;
        return true;
    }

    private static bool TryWidenCompatibleInjury(
        LedgerStage stage,
        in MedicalEffect effect,
        out MedicalDirtyFlags dirty)
    {
        dirty = MedicalDirtyFlags.None;
        if (!CanWidenInjury(effect.InjuryKind))
            return false;

        for (var i = 0; i < stage.Injuries.Count; i++)
        {
            var injury = stage.Injuries[i];
            if (injury.Region != effect.Region ||
                injury.Kind != effect.InjuryKind ||
                injury.Flags.HasFlag(InjuryFlags.Closed) ||
                injury.Flags.HasFlag(InjuryFlags.Sutured) ||
                injury.Flags.HasFlag(InjuryFlags.Debrided))
            {
                continue;
            }

            injury.Damage = FixedPoint2.Max(FixedPoint2.Zero, injury.Damage + effect.InjuryDamage);
            injury.Stage = InjuryRules.GetStage(injury.Kind, injury.Damage);
            injury.Flags |= GetThresholdInjuryFlags(in effect, injury.Damage);
            stage.Injuries[i] = injury;
            dirty = MedicalDirtyFlags.Injuries;
            return true;
        }

        return false;
    }

    private static bool CanWidenInjury(InjuryKind kind)
    {
        return kind is InjuryKind.Cut or InjuryKind.Puncture or InjuryKind.Bruise or InjuryKind.Burn;
    }

    private static InjuryFlags GetInitialInjuryFlags(in MedicalEffect effect)
    {
        return effect.InjuryFlags | GetThresholdInjuryFlags(in effect, effect.InjuryDamage);
    }

    private static InjuryFlags GetThresholdInjuryFlags(
        in MedicalEffect effect,
        FixedPoint2 injuryDamage)
    {
        if (effect.InjuryKind == InjuryKind.Burn &&
            effect.InjuryFlagThreshold > FixedPoint2.Zero &&
            injuryDamage >= effect.InjuryFlagThreshold)
        {
            return InjuryFlags.Necrotic;
        }

        return InjuryFlags.None;
    }

    private static bool TryApplySkeletalState(
        LedgerStage stage,
        in MedicalEffect effect,
        out MedicalDirtyFlags dirty,
        out string failureReason)
    {
        dirty = MedicalDirtyFlags.None;
        if (!TryGetRegionIndex(stage.Regions, effect.Region, out var index))
        {
            failureReason = $"Transaction targets missing region {effect.Region}.";
            return false;
        }

        var region = stage.Regions[index];
        if (effect.Broken)
        {
            region.Skeletal.Broken = true;
            region.Skeletal.Splinted = effect.Splinted;
            if (effect.FractureSeverity != FractureSeverity.None)
            {
                region.Skeletal.Severity = effect.FractureSeverity;
            }
            else if (region.Skeletal.Severity == FractureSeverity.None)
            {
                region.Skeletal.Severity = FractureSeverity.Simple;
            }
        }
        else
        {
            region.Skeletal.Broken = false;
            region.Skeletal.Splinted = false;
            region.Skeletal.Knitting = false;
            region.Skeletal.Malunion = false;
            region.Skeletal.BoneGelApplied = false;
            region.Skeletal.BoneSet = false;
            region.Skeletal.BoneGrafted = false;
            region.Skeletal.Severity = FractureSeverity.None;
            region.Skeletal.KnittingSecondsRemaining = FixedPoint2.Zero;
        }

        stage.Regions[index] = region;

        dirty = MedicalDirtyFlags.Skeletal;
        failureReason = string.Empty;
        return true;
    }

    private static bool TryApplySkeletalFlags(
        LedgerStage stage,
        in MedicalEffect effect,
        out MedicalDirtyFlags dirty,
        out string failureReason)
    {
        dirty = MedicalDirtyFlags.None;
        if (!TryGetRegionIndex(stage.Regions, effect.Region, out var index))
        {
            failureReason = $"Transaction targets missing region {effect.Region}.";
            return false;
        }

        var region = stage.Regions[index];
        region.Skeletal.Flags |= effect.SkeletalFlagsToSet;
        region.Skeletal.Flags &= ~effect.SkeletalFlagsToClear;
        stage.Regions[index] = region;

        dirty = MedicalDirtyFlags.Skeletal;
        failureReason = string.Empty;
        return true;
    }

    private static bool TryApplyOrganDamage(
        LedgerStage stage,
        in MedicalEffect effect,
        out MedicalDirtyFlags dirty,
        out string failureReason)
    {
        dirty = MedicalDirtyFlags.None;
        if (!TryGetOrganIndex(stage.Organs, effect.OrganSlot, out var index))
        {
            failureReason = $"Transaction targets missing organ {effect.OrganSlot}.";
            return false;
        }

        var organ = stage.Organs[index];
        organ.Damage = FixedPoint2.Max(FixedPoint2.Zero, organ.Damage + effect.OrganDamage);
        organ.Status = OrganRules.GetStatus(organ.Damage);
        stage.Organs[index] = organ;

        dirty = MedicalDirtyFlags.Organs;
        failureReason = string.Empty;
        return true;
    }

    private static bool TryApplyBleedSource(
        LedgerStage stage,
        in MedicalEffect effect,
        out MedicalDirtyFlags dirty,
        out string failureReason)
    {
        dirty = MedicalDirtyFlags.None;
        if (!TryGetRegionIndex(stage.Regions, effect.Region, out _))
        {
            failureReason = $"Transaction targets missing region {effect.Region}.";
            return false;
        }

        if (stage.BleedSources.Count >= HumanMedicalComponent.MaxBleedSources)
        {
            failureReason = "Body would exceed bleed source cap.";
            return false;
        }

        stage.BleedSources.Add(new BleedSource
        {
            Id = stage.NextBleedSourceId++,
            Region = effect.Region,
            Kind = effect.BleedKind,
            Flags = effect.BleedFlags,
            Rate = effect.BleedRate,
            SourceInjuryId = effect.SourceInjuryId,
        });

        dirty = MedicalDirtyFlags.Bleeding;
        failureReason = string.Empty;
        return true;
    }

    private static bool TryApplyCloseBleedSources(
        LedgerStage stage,
        in MedicalEffect effect,
        out MedicalDirtyFlags dirty,
        out string failureReason)
    {
        dirty = MedicalDirtyFlags.None;
        if (!TryGetRegionIndex(stage.Regions, effect.Region, out _))
        {
            failureReason = $"Transaction targets missing region {effect.Region}.";
            return false;
        }

        for (var i = 0; i < stage.BleedSources.Count; i++)
        {
            var source = stage.BleedSources[i];
            if (source.Region != effect.Region ||
                source.Kind != effect.BleedKind)
            {
                continue;
            }

            if (effect.BleedFlags != BleedFlags.None &&
                (source.Flags & effect.BleedFlags) != effect.BleedFlags)
            {
                continue;
            }

            if (source.Treatment.HasFlag(TreatmentFlags.Closed) &&
                source.Rate == FixedPoint2.Zero)
            {
                continue;
            }

            source.Treatment |= TreatmentFlags.Closed;
            source.Rate = FixedPoint2.Zero;
            stage.BleedSources[i] = source;
            dirty |= MedicalDirtyFlags.Bleeding;
        }

        failureReason = string.Empty;
        return true;
    }

    private static bool TryApplyConvertBleedSources(
        LedgerStage stage,
        in MedicalEffect effect,
        out MedicalDirtyFlags dirty,
        out string failureReason)
    {
        dirty = MedicalDirtyFlags.None;
        if (!TryGetRegionIndex(stage.Regions, effect.Region, out _))
        {
            failureReason = $"Transaction targets missing region {effect.Region}.";
            return false;
        }

        for (var i = 0; i < stage.BleedSources.Count; i++)
        {
            var source = stage.BleedSources[i];
            if (source.Region != effect.Region ||
                source.Kind != effect.BleedKind ||
                !source.Active)
            {
                continue;
            }

            if (effect.BleedFlags != BleedFlags.None &&
                (source.Flags & effect.BleedFlags) != effect.BleedFlags)
            {
                continue;
            }

            source.Kind = effect.TargetBleedKind;
            stage.BleedSources[i] = source;
            dirty |= MedicalDirtyFlags.Bleeding;
        }

        failureReason = string.Empty;
        return true;
    }

    private static bool TryApplyAddForeignObject(
        LedgerStage stage,
        in MedicalEffect effect,
        out MedicalDirtyFlags dirty,
        out string failureReason)
    {
        dirty = MedicalDirtyFlags.None;
        if (effect.ForeignObjectFragments <= 0 || effect.ForeignObjectSeverity <= 0f)
        {
            failureReason = "Foreign object effect must have positive fragments and severity.";
            return false;
        }

        if (!TryGetRegionIndex(stage.Regions, effect.Region, out var regionIndex))
        {
            failureReason = $"Transaction targets missing region {effect.Region}.";
            return false;
        }

        var region = stage.Regions[regionIndex];
        if (region.Presence != LimbPresence.Present)
        {
            failureReason = $"Transaction targets non-biological region {effect.Region}.";
            return false;
        }

        var existingIndex = FindCompatibleForeignObject(stage.ForeignObjects, in effect);
        if (existingIndex >= 0)
        {
            var foreignObject = stage.ForeignObjects[existingIndex];
            var capacity = HumanMedicalComponent.MaxForeignObjectFragmentsPerRegion - foreignObject.Fragments;
            if (capacity <= 0)
            {
                failureReason = $"Region {effect.Region} would exceed foreign object fragment cap.";
                return false;
            }

            var added = Math.Min(capacity, effect.ForeignObjectFragments);
            foreignObject.Fragments += added;
            foreignObject.Severity = Math.Clamp(
                MathF.Max(foreignObject.Severity, effect.ForeignObjectSeverity),
                0f,
                70f);
            if (effect.ForeignObjectDepth > foreignObject.Depth)
                foreignObject.Depth = effect.ForeignObjectDepth;
            if (effect.ForeignObjectMoveDamage > foreignObject.MoveDamage)
                foreignObject.MoveDamage = effect.ForeignObjectMoveDamage;
            if (effect.ForeignObjectMoveDamagePerFragment > FixedPoint2.Zero)
                foreignObject.MoveDamagePerFragment = effect.ForeignObjectMoveDamagePerFragment;

            foreignObject.Flags |= effect.ForeignObjectFlags;
            foreignObject.ExplosionChance = MathF.Max(foreignObject.ExplosionChance, effect.ForeignObjectExplosionChance);
            stage.ForeignObjects[existingIndex] = foreignObject;
            dirty = MedicalDirtyFlags.ForeignObjects;
            failureReason = string.Empty;
            return true;
        }

        if (stage.ForeignObjects.Count >= HumanMedicalComponent.MaxForeignObjects)
        {
            failureReason = "Body would exceed foreign object cap.";
            return false;
        }

        var fragments = Math.Min(
            HumanMedicalComponent.MaxForeignObjectFragmentsPerRegion,
            effect.ForeignObjectFragments);
        stage.ForeignObjects.Add(new ForeignObjectRecord
        {
            Id = stage.NextForeignObjectId++,
            Region = effect.Region,
            Kind = effect.ForeignObjectKind,
            Depth = effect.ForeignObjectDepth,
            Flags = effect.ForeignObjectFlags,
            Fragments = fragments,
            Severity = Math.Clamp(effect.ForeignObjectSeverity, 0f, 70f),
            MoveDamage = effect.ForeignObjectMoveDamage,
            MoveDamagePerFragment = effect.ForeignObjectMoveDamagePerFragment,
            ExplosionChance = MathF.Max(0f, effect.ForeignObjectExplosionChance),
        });

        dirty = MedicalDirtyFlags.ForeignObjects;
        failureReason = string.Empty;
        return true;
    }

    private static bool TryApplyRemoveForeignObject(
        LedgerStage stage,
        in MedicalEffect effect,
        out MedicalDirtyFlags dirty,
        out string failureReason)
    {
        dirty = MedicalDirtyFlags.None;
        if (effect.ForeignObjectFragments <= 0)
        {
            failureReason = "Foreign object removal must remove at least one fragment.";
            return false;
        }

        if (!TryGetRegionIndex(stage.Regions, effect.Region, out _))
        {
            failureReason = $"Transaction targets missing region {effect.Region}.";
            return false;
        }

        for (var i = 0; i < stage.ForeignObjects.Count; i++)
        {
            var foreignObject = stage.ForeignObjects[i];
            if (foreignObject.Id != effect.ForeignObjectId ||
                foreignObject.Region != effect.Region ||
                foreignObject.Fragments <= 0)
            {
                continue;
            }

            var oldFragments = foreignObject.Fragments;
            var removed = Math.Min(effect.ForeignObjectFragments, oldFragments);
            foreignObject.Fragments -= removed;
            if (foreignObject.Fragments <= 0)
            {
                stage.ForeignObjects.RemoveAt(i);
            }
            else
            {
                foreignObject.Severity *= (float) foreignObject.Fragments / oldFragments;
                stage.ForeignObjects[i] = foreignObject;
            }

            dirty = MedicalDirtyFlags.ForeignObjects;
            failureReason = string.Empty;
            return true;
        }

        failureReason = $"Transaction targets missing foreign object {effect.ForeignObjectId}.";
        return false;
    }

    private static bool TryApplyRegionPresence(
        LedgerStage stage,
        in MedicalEffect effect,
        out MedicalDirtyFlags dirty,
        out string failureReason)
    {
        dirty = MedicalDirtyFlags.None;
        if (!TryGetRegionIndex(stage.Regions, effect.Region, out var index))
        {
            failureReason = $"Transaction targets missing region {effect.Region}.";
            return false;
        }

        var region = stage.Regions[index];
        region.Presence = effect.Presence;
        if (effect.Presence is LimbPresence.Missing or LimbPresence.Detached or LimbPresence.Prosthetic)
        {
            region.BruteDamage = FixedPoint2.Zero;
            region.BurnDamage = FixedPoint2.Zero;
            region.Skeletal = default;
            region.Incision = IncisionDepth.Closed;
            region.Tourniquet = default;
        }

        stage.Regions[index] = region;

        dirty = MedicalDirtyFlags.Regions;
        if (effect.Presence is LimbPresence.Missing or LimbPresence.Detached or LimbPresence.Prosthetic)
        {
            dirty |= MedicalDirtyFlags.Skeletal;
            dirty |= CleanMissingRegionRecords(stage, effect.Region);
        }

        failureReason = string.Empty;
        return true;
    }

    private static MedicalDirtyFlags CleanMissingRegionRecords(
        LedgerStage stage,
        BodyRegion region)
    {
        var dirty = MedicalDirtyFlags.None;

        for (var i = 0; i < stage.Injuries.Count; i++)
        {
            var injury = stage.Injuries[i];
            if (injury.Region != region)
                continue;

            if (!injury.Flags.HasFlag(InjuryFlags.Closed) ||
                injury.Damage != FixedPoint2.Zero ||
                injury.RecoveryRate != FixedPoint2.Zero)
            {
                injury.Flags |= InjuryFlags.Closed;
                injury.Damage = FixedPoint2.Zero;
                injury.RecoveryRate = FixedPoint2.Zero;
                stage.Injuries[i] = injury;
                dirty |= MedicalDirtyFlags.Injuries;
            }
        }

        for (var i = 0; i < stage.BleedSources.Count; i++)
        {
            var source = stage.BleedSources[i];
            if (source.Region != region)
                continue;

            if (!source.Treatment.HasFlag(TreatmentFlags.Closed) ||
                source.Rate != FixedPoint2.Zero)
            {
                source.Treatment |= TreatmentFlags.Closed;
                source.Rate = FixedPoint2.Zero;
                stage.BleedSources[i] = source;
                dirty |= MedicalDirtyFlags.Bleeding;
            }
        }

        for (var i = stage.ForeignObjects.Count - 1; i >= 0; i--)
        {
            var foreignObject = stage.ForeignObjects[i];
            if (foreignObject.Region != region)
                continue;

            stage.ForeignObjects.RemoveAt(i);
            dirty |= MedicalDirtyFlags.ForeignObjects;
        }

        return dirty;
    }

    private static bool TryApplyIncisionDepth(
        LedgerStage stage,
        in MedicalEffect effect,
        out MedicalDirtyFlags dirty,
        out string failureReason)
    {
        dirty = MedicalDirtyFlags.None;
        if (!TryGetRegionIndex(stage.Regions, effect.Region, out var index))
        {
            failureReason = $"Transaction targets missing region {effect.Region}.";
            return false;
        }

        var region = stage.Regions[index];
        region.Incision = effect.IncisionDepth;
        stage.Regions[index] = region;

        dirty = MedicalDirtyFlags.Regions;
        failureReason = string.Empty;
        return true;
    }

    private static bool TryApplyDetachedLimb(
        LedgerStage stage,
        in MedicalEffect effect,
        out MedicalDirtyFlags dirty,
        out string failureReason)
    {
        dirty = MedicalDirtyFlags.None;
        if (!TryGetRegionIndex(stage.Regions, effect.Region, out _))
        {
            failureReason = $"Transaction targets missing region {effect.Region}.";
            return false;
        }

        stage.DetachedLimbs.Add(new DetachedLimbRecord
        {
            Id = stage.NextDetachedLimbId++,
            Region = effect.Region,
        });

        dirty = MedicalDirtyFlags.DetachedLimbs;
        failureReason = string.Empty;
        return true;
    }

    private static bool TryApplyDetachedLimbReattached(
        LedgerStage stage,
        in MedicalEffect effect,
        out MedicalDirtyFlags dirty,
        out string failureReason)
    {
        dirty = MedicalDirtyFlags.None;

        for (var i = 0; i < stage.DetachedLimbs.Count; i++)
        {
            var limb = stage.DetachedLimbs[i];
            if (limb.Region != effect.Region ||
                limb.Reattached ||
                effect.DetachedLimbId != 0 && limb.Id != effect.DetachedLimbId)
            {
                continue;
            }

            limb.Reattached = true;
            stage.DetachedLimbs[i] = limb;
            dirty = MedicalDirtyFlags.DetachedLimbs;
            failureReason = string.Empty;
            return true;
        }

        failureReason = string.Empty;
        return true;
    }

    private static bool TryApplyCloseStumpRecords(
        LedgerStage stage,
        in MedicalEffect effect,
        out MedicalDirtyFlags dirty,
        out string failureReason)
    {
        dirty = MedicalDirtyFlags.None;
        if (!TryGetRegionIndex(stage.Regions, effect.Region, out _))
        {
            failureReason = $"Transaction targets missing region {effect.Region}.";
            return false;
        }

        var stumpIndex = -1;
        var stumpId = 0;
        for (var i = 0; i < stage.Injuries.Count; i++)
        {
            var injury = stage.Injuries[i];
            if (injury.Region != effect.Region ||
                injury.Kind != InjuryKind.Stump ||
                injury.Flags.HasFlag(InjuryFlags.Closed))
            {
                continue;
            }

            if (stumpIndex != -1)
            {
                failureReason = string.Empty;
                return true;
            }

            stumpIndex = i;
            stumpId = injury.Id;
        }

        if (stumpIndex != -1)
        {
            var injury = stage.Injuries[stumpIndex];
            injury.Flags |= InjuryFlags.Closed | InjuryFlags.Sutured;
            injury.Damage = FixedPoint2.Zero;
            injury.RecoveryRate = FixedPoint2.Zero;
            stage.Injuries[stumpIndex] = injury;
            dirty |= MedicalDirtyFlags.Injuries;
        }

        for (var i = 0; i < stage.BleedSources.Count; i++)
        {
            var source = stage.BleedSources[i];
            if (source.Region != effect.Region ||
                source.Kind != BleedKind.Stump ||
                stumpId != 0 && source.SourceInjuryId != 0 && source.SourceInjuryId != stumpId)
            {
                continue;
            }

            source.Treatment |= TreatmentFlags.Closed | TreatmentFlags.Sutured;
            source.Rate = FixedPoint2.Zero;
            stage.BleedSources[i] = source;
            dirty |= MedicalDirtyFlags.Bleeding;
        }

        failureReason = string.Empty;
        return true;
    }

    private static bool TryApplyOrganMissing(
        LedgerStage stage,
        in MedicalEffect effect,
        out MedicalDirtyFlags dirty,
        out string failureReason)
    {
        dirty = MedicalDirtyFlags.None;
        if (!TryGetOrganIndex(stage.Organs, effect.OrganSlot, out var index))
        {
            failureReason = $"Transaction targets missing organ slot {effect.OrganSlot}.";
            return false;
        }

        var missing = effect.Presence == LimbPresence.Missing;
        var organ = stage.Organs[index];
        if (organ.Missing == missing)
        {
            failureReason = string.Empty;
            return true;
        }

        if (missing)
            organ.Flags |= OrganFlags.Missing;
        else
            organ.Flags &= ~OrganFlags.Missing;

        stage.Organs[index] = organ;
        dirty = MedicalDirtyFlags.Organs;
        failureReason = string.Empty;
        return true;
    }

    private static bool TryApplyOrganStasis(
        LedgerStage stage,
        in MedicalEffect effect,
        out MedicalDirtyFlags dirty,
        out string failureReason)
    {
        dirty = MedicalDirtyFlags.None;
        if (!TryGetOrganIndex(stage.Organs, effect.OrganSlot, out var index))
        {
            failureReason = $"Transaction targets missing organ slot {effect.OrganSlot}.";
            return false;
        }

        var organ = stage.Organs[index];
        var hasStasis = organ.Flags.HasFlag(OrganFlags.Stasis);
        if (hasStasis == effect.OrganStasis)
        {
            failureReason = string.Empty;
            return true;
        }

        if (effect.OrganStasis)
            organ.Flags |= OrganFlags.Stasis;
        else
            organ.Flags &= ~OrganFlags.Stasis;

        stage.Organs[index] = organ;
        dirty = MedicalDirtyFlags.Organs;
        failureReason = string.Empty;
        return true;
    }

    private static bool TryApplyTreatmentEffect(
        LedgerStage stage,
        in TreatmentEffect effect,
        out MedicalDirtyFlags dirty,
        out string failureReason)
    {
        dirty = MedicalDirtyFlags.None;
        failureReason = string.Empty;

        switch (effect.Kind)
        {
            case TreatmentEffectKind.UpdateBleedSource:
                return TryApplyBleedSourceTreatment(stage, in effect, out dirty, out failureReason);
            case TreatmentEffectKind.UpdateInjury:
                return TryApplyInjuryTreatment(stage, in effect, out dirty, out failureReason);
            case TreatmentEffectKind.SetSkeletalSplinted:
                return TryApplySplintTreatment(stage, in effect, out dirty, out failureReason);
            case TreatmentEffectKind.StartBoneKnitting:
                return TryApplyCastTreatment(stage, in effect, out dirty, out failureReason);
            case TreatmentEffectKind.SetTourniquet:
                return TryApplyTourniquetTreatment(stage, in effect, out dirty, out failureReason);
            case TreatmentEffectKind.ReduceBurnDamage:
                return TryApplyBurnTreatment(stage, in effect, out dirty, out failureReason);
            case TreatmentEffectKind.RepairOrgan:
                return TryApplyOrganRepairTreatment(stage, in effect, out dirty, out failureReason);
            case TreatmentEffectKind.StartInjuryRecovery:
                return TryApplyInjuryRecoveryTreatment(stage, in effect, out dirty, out failureReason);
            case TreatmentEffectKind.ReduceInjuryDamage:
                return TryApplyInjuryDamageReductionTreatment(stage, in effect, out dirty, out failureReason);
            case TreatmentEffectKind.RepairRegionDamage:
                return TryApplyMechanicalRegionRepairTreatment(stage, in effect, out dirty, out failureReason);
            default:
                failureReason = $"Unsupported treatment effect {effect.Kind}.";
                return false;
        }
    }

    private static bool TryApplyBleedSourceTreatment(
        LedgerStage stage,
        in TreatmentEffect effect,
        out MedicalDirtyFlags dirty,
        out string failureReason)
    {
        dirty = MedicalDirtyFlags.None;
        for (var i = 0; i < stage.BleedSources.Count; i++)
        {
            var source = stage.BleedSources[i];
            if (source.Id != effect.BleedSourceId)
                continue;
            if (effect.Region != BodyRegion.None && source.Region != effect.Region)
                continue;

            source.Treatment |= effect.TreatmentFlags;
            if (effect.SetBleedRate)
                source.Rate = FixedPoint2.Max(FixedPoint2.Zero, effect.BleedRate);

            stage.BleedSources[i] = source;
            dirty = MedicalDirtyFlags.Bleeding;
            failureReason = string.Empty;
            return true;
        }

        failureReason = $"Treatment targets missing bleed source {effect.BleedSourceId}.";
        return false;
    }

    private static bool TryApplyInjuryTreatment(
        LedgerStage stage,
        in TreatmentEffect effect,
        out MedicalDirtyFlags dirty,
        out string failureReason)
    {
        dirty = MedicalDirtyFlags.None;
        for (var i = 0; i < stage.Injuries.Count; i++)
        {
            var injury = stage.Injuries[i];
            if (injury.Id != effect.InjuryId)
                continue;
            if (effect.Region != BodyRegion.None && injury.Region != effect.Region)
                continue;

            injury.Flags |= effect.InjuryFlags;
            injury.Flags &= ~effect.ClearInjuryFlags;
            stage.Injuries[i] = injury;
            dirty = MedicalDirtyFlags.Injuries;
            failureReason = string.Empty;
            return true;
        }

        failureReason = $"Treatment targets missing injury {effect.InjuryId}.";
        return false;
    }

    private static bool TryApplyInjuryRecoveryTreatment(
        LedgerStage stage,
        in TreatmentEffect effect,
        out MedicalDirtyFlags dirty,
        out string failureReason)
    {
        dirty = MedicalDirtyFlags.None;
        if (effect.Amount <= FixedPoint2.Zero)
        {
            failureReason = "Injury recovery treatment must have a positive recovery rate.";
            return false;
        }

        for (var i = 0; i < stage.Injuries.Count; i++)
        {
            var injury = stage.Injuries[i];
            if (injury.Id != effect.InjuryId)
                continue;
            if (effect.Region != BodyRegion.None && injury.Region != effect.Region)
                continue;
            if (injury.Damage <= FixedPoint2.Zero)
            {
                failureReason = $"Treatment targets healed injury {effect.InjuryId}.";
                return false;
            }

            injury.RecoveryRate = FixedPoint2.Max(injury.RecoveryRate, effect.Amount);
            stage.Injuries[i] = injury;
            dirty = MedicalDirtyFlags.Injuries;
            failureReason = string.Empty;
            return true;
        }

        failureReason = $"Treatment targets missing injury {effect.InjuryId}.";
        return false;
    }

    private static bool TryApplyInjuryDamageReductionTreatment(
        LedgerStage stage,
        in TreatmentEffect effect,
        out MedicalDirtyFlags dirty,
        out string failureReason)
    {
        dirty = MedicalDirtyFlags.None;
        if (effect.Amount <= FixedPoint2.Zero)
        {
            failureReason = "Injury damage reduction treatment must have a positive amount.";
            return false;
        }

        for (var i = 0; i < stage.Injuries.Count; i++)
        {
            var injury = stage.Injuries[i];
            if (injury.Id != effect.InjuryId)
                continue;
            if (effect.Region != BodyRegion.None && injury.Region != effect.Region)
                continue;
            if (!TryGetRegionIndex(stage.Regions, injury.Region, out var regionIndex))
            {
                failureReason = $"Treatment targets missing region {injury.Region}.";
                return false;
            }

            var region = stage.Regions[regionIndex];
            var regionDamage = injury.Kind == InjuryKind.Burn
                ? region.BurnDamage
                : region.BruteDamage;
            var heal = FixedPoint2.Min(effect.Amount, FixedPoint2.Min(injury.Damage, regionDamage));
            if (heal <= FixedPoint2.Zero)
            {
                failureReason = $"Treatment targets healed injury {effect.InjuryId}.";
                return false;
            }

            if (injury.Kind == InjuryKind.Burn)
                region.BurnDamage = FixedPoint2.Max(FixedPoint2.Zero, region.BurnDamage - heal);
            else
                region.BruteDamage = FixedPoint2.Max(FixedPoint2.Zero, region.BruteDamage - heal);

            injury.Damage = FixedPoint2.Max(FixedPoint2.Zero, injury.Damage - heal);
            injury.Flags |= effect.InjuryFlags;
            if (injury.Damage <= FixedPoint2.Zero && CanRemoveRecoveredInjury(injury))
                stage.Injuries.RemoveAt(i);
            else
            {
                injury.Stage = InjuryRules.GetStage(injury.Kind, injury.Damage);
                stage.Injuries[i] = injury;
            }

            stage.Regions[regionIndex] = region;
            dirty = MedicalDirtyFlags.Regions | MedicalDirtyFlags.Injuries;
            failureReason = string.Empty;
            return true;
        }

        failureReason = $"Treatment targets missing injury {effect.InjuryId}.";
        return false;
    }

    private static bool TryApplyMechanicalRegionRepairTreatment(
        LedgerStage stage,
        in TreatmentEffect effect,
        out MedicalDirtyFlags dirty,
        out string failureReason)
    {
        dirty = MedicalDirtyFlags.None;
        if (effect.Amount <= FixedPoint2.Zero)
        {
            failureReason = "Mechanical repair treatment must have a positive amount.";
            return false;
        }

        if (!TryGetRegionIndex(stage.Regions, effect.Region, out var index))
        {
            failureReason = $"Treatment targets missing region {effect.Region}.";
            return false;
        }

        var region = stage.Regions[index];
        if (region.Presence != LimbPresence.Prosthetic)
        {
            failureReason = $"Treatment targets non-prosthetic region {effect.Region}.";
            return false;
        }

        if (IsBurnRepair(effect.InjuryKind))
        {
            var repair = FixedPoint2.Min(effect.Amount, region.BurnDamage);
            if (repair <= FixedPoint2.Zero)
            {
                failureReason = $"Treatment targets undamaged prosthetic region {effect.Region}.";
                return false;
            }

            region.BurnDamage = FixedPoint2.Max(FixedPoint2.Zero, region.BurnDamage - repair);
        }
        else
        {
            var repair = FixedPoint2.Min(effect.Amount, region.BruteDamage);
            if (repair <= FixedPoint2.Zero)
            {
                failureReason = $"Treatment targets undamaged prosthetic region {effect.Region}.";
                return false;
            }

            region.BruteDamage = FixedPoint2.Max(FixedPoint2.Zero, region.BruteDamage - repair);
        }

        stage.Regions[index] = region;
        dirty = MedicalDirtyFlags.Regions;
        failureReason = string.Empty;
        return true;
    }

    private static bool TryApplySplintTreatment(
        LedgerStage stage,
        in TreatmentEffect effect,
        out MedicalDirtyFlags dirty,
        out string failureReason)
    {
        dirty = MedicalDirtyFlags.None;
        if (!TryGetRegionIndex(stage.Regions, effect.Region, out var index))
        {
            failureReason = $"Treatment targets missing region {effect.Region}.";
            return false;
        }

        var region = stage.Regions[index];
        region.Skeletal.Splinted = effect.Splinted;
        stage.Regions[index] = region;

        dirty = MedicalDirtyFlags.Skeletal;
        failureReason = string.Empty;
        return true;
    }

    private static bool TryApplyCastTreatment(
        LedgerStage stage,
        in TreatmentEffect effect,
        out MedicalDirtyFlags dirty,
        out string failureReason)
    {
        dirty = MedicalDirtyFlags.None;
        if (effect.Amount <= FixedPoint2.Zero)
        {
            failureReason = "Cast treatment must have a positive knitting duration.";
            return false;
        }

        if (!TryGetRegionIndex(stage.Regions, effect.Region, out var index))
        {
            failureReason = $"Treatment targets missing region {effect.Region}.";
            return false;
        }

        var region = stage.Regions[index];
        if (!region.Skeletal.Broken)
        {
            failureReason = $"Treatment targets unbroken region {effect.Region}.";
            return false;
        }

        region.Skeletal.Splinted = true;
        region.Skeletal.Knitting = true;
        region.Skeletal.KnittingSecondsRemaining = effect.Amount;
        stage.Regions[index] = region;

        dirty = MedicalDirtyFlags.Skeletal;
        failureReason = string.Empty;
        return true;
    }

    private static bool TryApplyTourniquetTreatment(
        LedgerStage stage,
        in TreatmentEffect effect,
        out MedicalDirtyFlags dirty,
        out string failureReason)
    {
        dirty = MedicalDirtyFlags.None;
        if (!TryGetRegionIndex(stage.Regions, effect.Region, out var index))
        {
            failureReason = $"Treatment targets missing region {effect.Region}.";
            return false;
        }

        if (!IsTourniquetable(effect.Region))
        {
            failureReason = $"Treatment targets non-tourniquetable region {effect.Region}.";
            return false;
        }

        var region = stage.Regions[index];
        if (effect.TourniquetApplied)
        {
            if (effect.Amount <= FixedPoint2.Zero)
            {
                failureReason = "Tourniquet treatment must have a positive necrosis duration.";
                return false;
            }

            if (region.Tourniquet.Applied)
            {
                failureReason = $"Region {effect.Region} already has a tourniquet.";
                return false;
            }

            region.Tourniquet = new TourniquetState
            {
                Flags = TourniquetStateFlags.Applied,
                NecrosisSecondsRemaining = effect.Amount,
                RefundOnRemove = effect.RefundOnRemove,
            };
            ApplyTourniquetBleedFlag(stage, effect.Region, enabled: true);
        }
        else
        {
            if (!region.Tourniquet.Applied)
            {
                failureReason = $"Region {effect.Region} has no tourniquet.";
                return false;
            }

            region.Tourniquet = default;
            ApplyTourniquetBleedFlag(stage, effect.Region, enabled: false);
        }

        stage.Regions[index] = region;
        dirty = MedicalDirtyFlags.Regions | MedicalDirtyFlags.Bleeding;
        failureReason = string.Empty;
        return true;
    }

    private static bool TryApplyBurnTreatment(
        LedgerStage stage,
        in TreatmentEffect effect,
        out MedicalDirtyFlags dirty,
        out string failureReason)
    {
        dirty = MedicalDirtyFlags.None;
        if (!TryGetRegionIndex(stage.Regions, effect.Region, out var index))
        {
            failureReason = $"Treatment targets missing region {effect.Region}.";
            return false;
        }

        var region = stage.Regions[index];
        region.BurnDamage = FixedPoint2.Max(FixedPoint2.Zero, region.BurnDamage - effect.Amount);
        stage.Regions[index] = region;

        dirty = MedicalDirtyFlags.Regions;
        failureReason = string.Empty;
        return true;
    }

    private static bool TryApplyOrganRepairTreatment(
        LedgerStage stage,
        in TreatmentEffect effect,
        out MedicalDirtyFlags dirty,
        out string failureReason)
    {
        dirty = MedicalDirtyFlags.None;
        if (!TryGetOrganIndex(stage.Organs, effect.OrganSlot, out var index))
        {
            failureReason = $"Treatment targets missing organ {effect.OrganSlot}.";
            return false;
        }

        var organ = stage.Organs[index];
        organ.Damage = FixedPoint2.Max(FixedPoint2.Zero, organ.Damage - effect.Amount);
        organ.Status = OrganRules.GetStatus(organ.Damage);
        stage.Organs[index] = organ;

        dirty = MedicalDirtyFlags.Organs;
        failureReason = string.Empty;
        return true;
    }

    private static void ApplyTourniquetBleedFlag(
        LedgerStage stage,
        BodyRegion tourniquetRegion,
        bool enabled)
    {
        for (var i = 0; i < stage.BleedSources.Count; i++)
        {
            var source = stage.BleedSources[i];
            if (!IsSurfaceBleed(source.Kind) ||
                !IsDistalToTourniquet(tourniquetRegion, source.Region))
            {
                continue;
            }

            if (enabled)
                source.Treatment |= TreatmentFlags.Tourniquetted;
            else
                source.Treatment &= ~TreatmentFlags.Tourniquetted;

            stage.BleedSources[i] = source;
        }
    }

    private static bool IsSurfaceBleed(BleedKind kind)
    {
        return kind is BleedKind.External or BleedKind.Stump;
    }

    private static int FindCompatibleForeignObject(
        List<ForeignObjectRecord> foreignObjects,
        in MedicalEffect effect)
    {
        for (var i = 0; i < foreignObjects.Count; i++)
        {
            var foreignObject = foreignObjects[i];
            if (foreignObject.Region == effect.Region &&
                foreignObject.Kind == effect.ForeignObjectKind)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool RepairMatchingInjuries(
        List<InjuryRecord> injuries,
        BodyRegion region,
        InjuryKind repairKind,
        ref FixedPoint2 remaining,
        FixedPoint2 floor)
    {
        var changed = false;

        while (remaining > FixedPoint2.Zero)
        {
            var index = FindLargestRepairableInjury(injuries, region, repairKind, floor);
            if (index < 0)
                break;

            var injury = injuries[index];
            var heal = FixedPoint2.Min(remaining, FixedPoint2.Max(FixedPoint2.Zero, injury.Damage - floor));
            if (heal <= FixedPoint2.Zero)
                break;

            injury.Damage -= heal;
            remaining -= heal;

            if (injury.Damage <= FixedPoint2.Zero)
            {
                injuries.RemoveAt(index);
            }
            else
            {
                injury.Stage = InjuryRules.GetStage(injury.Kind, injury.Damage);
                injuries[index] = injury;
            }

            changed = true;
        }

        return changed;
    }

    private static int FindLargestRepairableInjury(
        List<InjuryRecord> injuries,
        BodyRegion region,
        InjuryKind repairKind,
        FixedPoint2 floor)
    {
        var best = -1;
        var bestDamage = FixedPoint2.Zero;

        for (var i = 0; i < injuries.Count; i++)
        {
            var injury = injuries[i];
            if (injury.Region != region ||
                !MatchesRepairKind(injury.Kind, repairKind) ||
                injury.Damage <= floor ||
                injury.Damage <= bestDamage)
            {
                continue;
            }

            best = i;
            bestDamage = injury.Damage;
        }

        return best;
    }

    private static bool MatchesRepairKind(
        InjuryKind injuryKind,
        InjuryKind repairKind)
    {
        if (IsBurnRepair(repairKind))
            return injuryKind == InjuryKind.Burn;

        return injuryKind is InjuryKind.Cut or InjuryKind.Puncture or InjuryKind.Bruise;
    }

    private static bool IsBurnRepair(InjuryKind repairKind)
    {
        return repairKind == InjuryKind.Burn;
    }

    public static bool CanTreatedInjuryRecover(InjuryRecord injury)
    {
        if (injury.Damage <= FixedPoint2.Zero ||
            injury.RecoveryRate <= FixedPoint2.Zero)
        {
            return false;
        }

        return injury.Kind switch
        {
            InjuryKind.Burn => injury.Flags.HasFlag(InjuryFlags.Salved),
            InjuryKind.Cut or InjuryKind.Puncture or InjuryKind.Bruise => injury.Flags.HasFlag(InjuryFlags.Bandaged) ||
                injury.Flags.HasFlag(InjuryFlags.Sutured) ||
                injury.Flags.HasFlag(InjuryFlags.Closed),
            InjuryKind.Stump => injury.Flags.HasFlag(InjuryFlags.Bandaged) ||
                injury.Flags.HasFlag(InjuryFlags.Sutured) ||
                injury.Flags.HasFlag(InjuryFlags.Closed),
            _ => false,
        };
    }

    private static bool CanRemoveRecoveredInjury(InjuryRecord injury)
    {
        return injury.Kind is InjuryKind.Cut or InjuryKind.Puncture or InjuryKind.Bruise or InjuryKind.Burn;
    }

    private static bool IsTourniquetable(BodyRegion region)
    {
        return region is BodyRegion.LeftArm or BodyRegion.RightArm or BodyRegion.LeftLeg or BodyRegion.RightLeg;
    }

    private static bool IsDistalToTourniquet(
        BodyRegion tourniquetRegion,
        BodyRegion sourceRegion)
    {
        return tourniquetRegion switch
        {
            BodyRegion.LeftArm => sourceRegion is BodyRegion.LeftArm or BodyRegion.LeftHand,
            BodyRegion.RightArm => sourceRegion is BodyRegion.RightArm or BodyRegion.RightHand,
            BodyRegion.LeftLeg => sourceRegion is BodyRegion.LeftLeg or BodyRegion.LeftFoot,
            BodyRegion.RightLeg => sourceRegion is BodyRegion.RightLeg or BodyRegion.RightFoot,
            _ => false,
        };
    }

    private static bool TryGetRegionIndex(
        RegionState[] regions,
        BodyRegion region,
        out int index)
    {
        index = (int) region;
        return region != BodyRegion.None &&
            index > 0 &&
            index < regions.Length &&
            regions[index].Region == region;
    }

    private static bool TryGetOrganIndex(
        OrganState[] organs,
        OrganSlot slot,
        out int index)
    {
        index = (int) slot;
        return slot != OrganSlot.None &&
            index > 0 &&
            index < organs.Length &&
            organs[index].Slot == slot;
    }

    private sealed class LedgerStage
    {
        public readonly int[] InjuryCounts = new int[HumanMedicalComponent.RegionSlotCount];
        public RegionState[] Regions;
        public List<InjuryRecord> Injuries;
        public OrganState[] Organs;
        public List<BleedSource> BleedSources;
        public List<ForeignObjectRecord> ForeignObjects;
        public List<DetachedLimbRecord> DetachedLimbs;
        public int NextInjuryId;
        public int NextBleedSourceId;
        public int NextForeignObjectId;
        public int NextDetachedLimbId;

        public LedgerStage(HumanMedicalComponent medical)
        {
            Regions = (RegionState[]) medical.Regions.Clone();
            Injuries = new List<InjuryRecord>(medical.Injuries);
            Organs = (OrganState[]) medical.Organs.Clone();
            BleedSources = new List<BleedSource>(medical.BleedSources);
            ForeignObjects = new List<ForeignObjectRecord>(medical.ForeignObjects);
            DetachedLimbs = new List<DetachedLimbRecord>(medical.DetachedLimbs);
            NextInjuryId = medical.NextInjuryId;
            NextBleedSourceId = medical.NextBleedSourceId;
            NextForeignObjectId = medical.NextForeignObjectId;
            NextDetachedLimbId = medical.NextDetachedLimbId;

            foreach (var injury in Injuries)
            {
                var index = (int) injury.Region;
                if (index > 0 && index < InjuryCounts.Length)
                    InjuryCounts[index]++;
            }
        }
    }
}
