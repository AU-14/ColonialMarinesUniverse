using System;
using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._CMU14.Medical.Human.Care;
using Content.Shared.FixedPoint;

namespace Content.Shared._CMU14.Medical.Human.Rules;

public static class TreatmentRules
{
    private static readonly FixedPoint2 DefaultBruteRecoveryRate = FixedPoint2.New(0.375);
    private static readonly FixedPoint2 DefaultBurnRecoveryRate = FixedPoint2.New(0.375);
    private static readonly FixedPoint2 RecoveryAmountRateDivisor = FixedPoint2.New(10);
    private static readonly FixedPoint2 FieldLineRepairAmount = FixedPoint2.New(10);
    private static readonly FixedPoint2 OrganRepairAmount = FixedPoint2.New(15);
    private static readonly FixedPoint2 ProstheticRepairAmount = FixedPoint2.New(15);
    private static readonly FixedPoint2 DefaultCastKnittingSeconds = FixedPoint2.New(300);
    private static readonly FixedPoint2 DefaultTourniquetNecrosisSeconds = FixedPoint2.New(300);

    public static TreatmentRuleResult TryCreateTreatmentPlan(
        HumanMedicalComponent medical,
        TreatmentAttempt attempt)
    {
        return attempt.Kind switch
        {
            TreatmentKind.Gauze => TryCreateGauzePlan(medical, attempt),
            TreatmentKind.Ointment or TreatmentKind.Salve => TryCreateSalvePlan(medical, attempt),
            TreatmentKind.Splint => TryCreateSplintPlan(medical, attempt),
            TreatmentKind.Cast => TryCreateCastPlan(medical, attempt),
            TreatmentKind.ClampBleed => TryCreateClampPlan(medical, attempt),
            TreatmentKind.ApplyTourniquet => TryCreateApplyTourniquetPlan(medical, attempt),
            TreatmentKind.RemoveTourniquet => TryCreateRemoveTourniquetPlan(medical, attempt),
            TreatmentKind.Suture => TryCreateSuturePlan(medical, attempt),
            TreatmentKind.RepairOrgan => TryCreateOrganRepairPlan(medical, attempt),
            TreatmentKind.TemporaryBleedSuppression => TryCreateTemporaryBleedSuppressionPlan(medical, attempt),
            TreatmentKind.SurgicalLine => TryCreateSurgicalLinePlan(medical, attempt),
            TreatmentKind.SyntheticGraft => TryCreateSyntheticGraftPlan(medical, attempt),
            TreatmentKind.RepairProstheticBrute => TryCreateProstheticRepairPlan(
                medical,
                attempt,
                repairBrute: true,
                repairBurn: false),
            TreatmentKind.RepairProstheticBurn => TryCreateProstheticRepairPlan(
                medical,
                attempt,
                repairBrute: false,
                repairBurn: true),
            TreatmentKind.RepairProstheticComposite => TryCreateProstheticRepairPlan(
                medical,
                attempt,
                repairBrute: true,
                repairBurn: true),
            _ => Fail($"Treatment {attempt.Kind} is not implemented."),
        };
    }

    private static TreatmentRuleResult TryCreateGauzePlan(
        HumanMedicalComponent medical,
        TreatmentAttempt attempt)
    {
        var effects = new List<TreatmentEffect>(3);
        var dirty = MedicalDirtyFlags.None;
        var recoveryRate = GetRecoveryRate(attempt, DefaultBruteRecoveryRate);

        if (TryFindSurfaceBleedSource(medical, attempt, requireActive: true, out var bleed))
        {
            effects.Add(TreatmentEffect.UpdateBleedSource(
                bleed.Region,
                bleed.Id,
                TreatmentFlags.Bandaged | TreatmentFlags.Closed,
                setBleedRate: true,
                FixedPoint2.Zero));
            dirty |= MedicalDirtyFlags.Bleeding;
        }

        if (TryFindBandageableInjury(medical, attempt, out var injury))
        {
            effects.Add(TreatmentEffect.UpdateInjury(
                injury.Region,
                injury.Id,
                InjuryFlags.Bandaged));

            if (injury.Damage > FixedPoint2.Zero &&
                recoveryRate > FixedPoint2.Zero)
            {
                effects.Add(TreatmentEffect.StartInjuryRecovery(
                    injury.Region,
                    injury.Id,
                    recoveryRate));
            }

            dirty |= MedicalDirtyFlags.Injuries;
        }
        else if (TryFindRecoverableInjury(
                     medical,
                     attempt,
                     recoveryRate,
                     requireBurn: false,
                     out injury))
        {
            effects.Add(TreatmentEffect.StartInjuryRecovery(
                injury.Region,
                injury.Id,
                recoveryRate));
            dirty |= MedicalDirtyFlags.Injuries;
        }

        return effects.Count == 0
            ? Fail("No active external bleed source or open wound can be bandaged.")
            : Applied(effects.ToArray(), dirty);
    }

    private static TreatmentRuleResult TryCreateSalvePlan(
        HumanMedicalComponent medical,
        TreatmentAttempt attempt)
    {
        var effects = new List<TreatmentEffect>(4);
        var dirty = MedicalDirtyFlags.None;
        var recoveryRate = GetRecoveryRate(attempt, DefaultBurnRecoveryRate);

        foreach (var injury in medical.Injuries)
        {
            if (injury.Region != attempt.Region ||
                injury.Kind != InjuryKind.Burn ||
                injury.Flags.HasFlag(InjuryFlags.Salved))
            {
                continue;
            }

            effects.Add(TreatmentEffect.UpdateInjury(
                attempt.Region,
                injury.Id,
                InjuryFlags.Salved));
            effects.Add(TreatmentEffect.StartInjuryRecovery(
                attempt.Region,
                injury.Id,
                recoveryRate));
            dirty |= MedicalDirtyFlags.Injuries;
        }

        if (effects.Count == 0 &&
            TryFindRecoverableInjury(
                medical,
                attempt,
                recoveryRate,
                requireBurn: true,
                out var treatedBurn))
        {
            effects.Add(TreatmentEffect.StartInjuryRecovery(
                treatedBurn.Region,
                treatedBurn.Id,
                recoveryRate));
            dirty |= MedicalDirtyFlags.Injuries;
        }

        return effects.Count == 0
            ? Fail("No burn injury can be salved.")
            : Applied(effects.ToArray(), dirty);
    }

    private static TreatmentRuleResult TryCreateSplintPlan(
        HumanMedicalComponent medical,
        TreatmentAttempt attempt)
    {
        var region = HumanMedicalLedger.GetRegion(medical, attempt.Region);
        if (!region.Skeletal.Broken)
            return Fail("The target region is not broken.");
        if (region.Skeletal.Splinted)
            return Fail("The target region is already splinted.");

        return Applied(
            new[]
            {
                TreatmentEffect.SetSkeletalSplinted(attempt.Region, splinted: true),
            },
            MedicalDirtyFlags.Skeletal);
    }

    private static TreatmentRuleResult TryCreateCastPlan(
        HumanMedicalComponent medical,
        TreatmentAttempt attempt)
    {
        var region = HumanMedicalLedger.GetRegion(medical, attempt.Region);
        if (!region.Skeletal.Broken)
            return Fail("The target region is not broken.");
        if (region.Skeletal.Knitting)
            return Fail("The target region is already knitting.");

        var duration = attempt.Amount > FixedPoint2.Zero
            ? attempt.Amount
            : DefaultCastKnittingSeconds;

        return Applied(
            new[]
            {
                TreatmentEffect.StartBoneKnitting(attempt.Region, duration),
            },
            MedicalDirtyFlags.Skeletal);
    }

    private static TreatmentRuleResult TryCreateClampPlan(
        HumanMedicalComponent medical,
        TreatmentAttempt attempt)
    {
        if (!TryFindBleedSource(medical, attempt, BleedKind.Internal, requireActive: true, out var bleed))
            return Fail("No active internal bleed source can be clamped.");

        return Applied(
            new[]
            {
                TreatmentEffect.UpdateBleedSource(
                    bleed.Region,
                    bleed.Id,
                    TreatmentFlags.Clamped,
                    setBleedRate: false,
                    bleed.Rate),
            },
            MedicalDirtyFlags.Bleeding);
    }

    private static TreatmentRuleResult TryCreateApplyTourniquetPlan(
        HumanMedicalComponent medical,
        TreatmentAttempt attempt)
    {
        if (!IsTourniquetable(attempt.Region))
            return Fail("Tourniquets can only be applied to arms or legs.");

        var region = HumanMedicalLedger.GetRegion(medical, attempt.Region);
        if (region.Presence != LimbPresence.Present)
            return Fail("The target region is missing.");
        if (region.Tourniquet.Applied)
            return Fail("The target region already has a tourniquet.");

        var duration = attempt.Amount > FixedPoint2.Zero
            ? attempt.Amount
            : DefaultTourniquetNecrosisSeconds;

        return Applied(
            new[]
            {
                TreatmentEffect.SetTourniquet(
                    attempt.Region,
                    applied: true,
                    necrosisSeconds: duration,
                    refundOnRemove: attempt.RefundOnRemove),
            },
            MedicalDirtyFlags.Regions | MedicalDirtyFlags.Bleeding);
    }

    private static TreatmentRuleResult TryCreateRemoveTourniquetPlan(
        HumanMedicalComponent medical,
        TreatmentAttempt attempt)
    {
        if (!IsTourniquetable(attempt.Region))
            return Fail("Tourniquets can only be removed from arms or legs.");

        var region = HumanMedicalLedger.GetRegion(medical, attempt.Region);
        if (!region.Tourniquet.Applied)
            return Fail("The target region has no tourniquet.");

        return Applied(
            new[]
            {
                TreatmentEffect.SetTourniquet(
                    attempt.Region,
                    applied: false,
                    FixedPoint2.Zero,
                    refundOnRemove: null),
            },
            MedicalDirtyFlags.Regions | MedicalDirtyFlags.Bleeding);
    }

    private static TreatmentRuleResult TryCreateSuturePlan(
        HumanMedicalComponent medical,
        TreatmentAttempt attempt)
    {
        if (!TryFindSuturableInjury(medical, attempt, out var injury))
            return Fail("No open wound or stump can be sutured.");

        var effects = new List<TreatmentEffect>
        {
            TreatmentEffect.UpdateInjury(
                injury.Region,
                injury.Id,
                InjuryFlags.Sutured | InjuryFlags.Closed),
        };
        var dirty = MedicalDirtyFlags.Injuries;

        if (TryFindLinkedBleedSource(medical, attempt, injury, out var bleed))
        {
            effects.Add(TreatmentEffect.UpdateBleedSource(
                bleed.Region,
                bleed.Id,
                TreatmentFlags.Sutured | TreatmentFlags.Closed,
                setBleedRate: true,
                FixedPoint2.Zero));
            dirty |= MedicalDirtyFlags.Bleeding;
        }

        return Applied(effects.ToArray(), dirty);
    }

    private static TreatmentRuleResult TryCreateSurgicalLinePlan(
        HumanMedicalComponent medical,
        TreatmentAttempt attempt)
    {
        var effects = new List<TreatmentEffect>(4);
        var dirty = MedicalDirtyFlags.None;

        if (TryFindReducibleInjury(medical, attempt, requireBurn: false, out var injury) &&
            TryGetAreaRepairAmount(
                HumanMedicalLedger.GetRegion(medical, injury.Region).BruteDamage,
                injury.Damage,
                attempt,
                out var repairAmount))
        {
            effects.Add(TreatmentEffect.ReduceInjuryDamage(
                injury.Region,
                injury.Id,
                repairAmount,
                InjuryFlags.Sutured));
            dirty |= MedicalDirtyFlags.Regions | MedicalDirtyFlags.Injuries;
        }

        AddTemporaryBleedSuppressionEffects(medical, attempt, effects, ref dirty);

        return effects.Count == 0
            ? Fail("No brute injury or active bleed can be treated with surgical line.")
            : Applied(effects.ToArray(), dirty);
    }

    private static TreatmentRuleResult TryCreateSyntheticGraftPlan(
        HumanMedicalComponent medical,
        TreatmentAttempt attempt)
    {
        if (!TryFindReducibleInjury(medical, attempt, requireBurn: true, out var injury) ||
            !TryGetAreaRepairAmount(
                HumanMedicalLedger.GetRegion(medical, injury.Region).BurnDamage,
                injury.Damage,
                attempt,
                out var repairAmount))
        {
            return Fail("No burn injury can be treated with a synth graft.");
        }

        return Applied(
            new[]
            {
                TreatmentEffect.ReduceInjuryDamage(
                    injury.Region,
                    injury.Id,
                    repairAmount,
                    InjuryFlags.Salved),
            },
            MedicalDirtyFlags.Regions | MedicalDirtyFlags.Injuries);
    }

    private static TreatmentRuleResult TryCreateTemporaryBleedSuppressionPlan(
        HumanMedicalComponent medical,
        TreatmentAttempt attempt)
    {
        var effects = new List<TreatmentEffect>(2);
        var dirty = MedicalDirtyFlags.None;
        AddTemporaryBleedSuppressionEffects(medical, attempt, effects, ref dirty);

        return effects.Count == 0
            ? Fail("No active bleed source can be temporarily suppressed.")
            : Applied(effects.ToArray(), dirty);
    }

    private static TreatmentRuleResult TryCreateProstheticRepairPlan(
        HumanMedicalComponent medical,
        TreatmentAttempt attempt,
        bool repairBrute,
        bool repairBurn)
    {
        var region = HumanMedicalLedger.GetRegion(medical, attempt.Region);
        if (region.Presence != LimbPresence.Prosthetic)
            return Fail("The target region is not a prosthetic limb.");

        var amount = attempt.Amount > FixedPoint2.Zero
            ? attempt.Amount
            : ProstheticRepairAmount;
        var effects = new List<TreatmentEffect>(2);

        if (repairBrute && region.BruteDamage > FixedPoint2.Zero)
        {
            effects.Add(TreatmentEffect.RepairRegionDamage(
                attempt.Region,
                InjuryKind.Bruise,
                FixedPoint2.Min(region.BruteDamage, amount)));
        }

        if (repairBurn && region.BurnDamage > FixedPoint2.Zero)
        {
            effects.Add(TreatmentEffect.RepairRegionDamage(
                attempt.Region,
                InjuryKind.Burn,
                FixedPoint2.Min(region.BurnDamage, amount)));
        }

        return effects.Count == 0
            ? Fail("The target prosthetic has no matching damage to repair.")
            : Applied(effects.ToArray(), MedicalDirtyFlags.Regions);
    }

    private static void AddTemporaryBleedSuppressionEffects(
        HumanMedicalComponent medical,
        TreatmentAttempt attempt,
        List<TreatmentEffect> effects,
        ref MedicalDirtyFlags dirty)
    {
        foreach (var source in medical.BleedSources)
        {
            if (source.Region != attempt.Region)
                continue;
            if (!source.Active)
                continue;
            if (source.Kind is not BleedKind.External and not BleedKind.Internal and not BleedKind.Stump)
                continue;

            effects.Add(TreatmentEffect.UpdateBleedSource(
                source.Region,
                source.Id,
                TreatmentFlags.TemporarilySuppressed,
                setBleedRate: true,
                FixedPoint2.Zero));
            dirty |= MedicalDirtyFlags.Bleeding;
        }
    }

    private static TreatmentRuleResult TryCreateOrganRepairPlan(
        HumanMedicalComponent medical,
        TreatmentAttempt attempt)
    {
        var organ = HumanMedicalLedger.GetOrgan(medical, attempt.OrganSlot);
        if (organ.Slot == OrganSlot.None)
            return Fail("No organ slot was targeted.");
        if (organ.Damage <= FixedPoint2.Zero)
            return Fail("The target organ has no damage to repair.");

        var amount = attempt.Amount > FixedPoint2.Zero
            ? attempt.Amount
            : OrganRepairAmount;

        return Applied(
            new[]
            {
                TreatmentEffect.RepairOrgan(attempt.OrganSlot, amount),
            },
            MedicalDirtyFlags.Organs);
    }

    private static bool TryFindBleedSource(
        HumanMedicalComponent medical,
        TreatmentAttempt attempt,
        BleedKind kind,
        bool requireActive,
        out BleedSource bleed)
    {
        foreach (var source in medical.BleedSources)
        {
            if (attempt.BleedSourceId != 0 && source.Id != attempt.BleedSourceId)
                continue;
            if (attempt.BleedSourceId == 0 && source.Region != attempt.Region)
                continue;
            if (source.Kind != kind)
                continue;
            if (requireActive && !source.Active)
                continue;

            bleed = source;
            return true;
        }

        bleed = default;
        return false;
    }

    private static bool TryFindSurfaceBleedSource(
        HumanMedicalComponent medical,
        TreatmentAttempt attempt,
        bool requireActive,
        out BleedSource bleed)
    {
        foreach (var source in medical.BleedSources)
        {
            if (attempt.BleedSourceId != 0 && source.Id != attempt.BleedSourceId)
                continue;
            if (attempt.BleedSourceId == 0 && source.Region != attempt.Region)
                continue;
            if (source.Kind is not BleedKind.External and not BleedKind.Stump)
                continue;
            if (requireActive && !source.Active)
                continue;

            bleed = source;
            return true;
        }

        bleed = default;
        return false;
    }

    private static bool TryFindSuturableInjury(
        HumanMedicalComponent medical,
        TreatmentAttempt attempt,
        out InjuryRecord injury)
    {
        foreach (var candidate in medical.Injuries)
        {
            if (attempt.InjuryId != 0 && candidate.Id != attempt.InjuryId)
                continue;
            if (attempt.InjuryId == 0 && candidate.Region != attempt.Region)
                continue;
            if (candidate.Flags.HasFlag(InjuryFlags.Sutured) ||
                candidate.Flags.HasFlag(InjuryFlags.Closed))
            {
                continue;
            }
            if (!IsSuturable(candidate.Kind))
                continue;

            injury = candidate;
            return true;
        }

        injury = default;
        return false;
    }

    private static bool TryFindBandageableInjury(
        HumanMedicalComponent medical,
        TreatmentAttempt attempt,
        out InjuryRecord injury)
    {
        foreach (var candidate in medical.Injuries)
        {
            if (attempt.InjuryId != 0 && candidate.Id != attempt.InjuryId)
                continue;
            if (attempt.InjuryId == 0 && candidate.Region != attempt.Region)
                continue;
            if (candidate.Flags.HasFlag(InjuryFlags.Bandaged) ||
                candidate.Flags.HasFlag(InjuryFlags.Closed) ||
                candidate.Flags.HasFlag(InjuryFlags.Sutured))
            {
                continue;
            }
            if (!IsBandageable(candidate.Kind))
                continue;

            injury = candidate;
            return true;
        }

        injury = default;
        return false;
    }

    private static bool TryFindRecoverableInjury(
        HumanMedicalComponent medical,
        TreatmentAttempt attempt,
        FixedPoint2 recoveryRate,
        bool requireBurn,
        out InjuryRecord injury)
    {
        foreach (var candidate in medical.Injuries)
        {
            if (attempt.InjuryId != 0 && candidate.Id != attempt.InjuryId)
                continue;
            if (attempt.InjuryId == 0 && candidate.Region != attempt.Region)
                continue;
            if (candidate.Damage <= FixedPoint2.Zero ||
                candidate.RecoveryRate >= recoveryRate)
            {
                continue;
            }

            if (requireBurn)
            {
                if (candidate.Kind != InjuryKind.Burn ||
                    !candidate.Flags.HasFlag(InjuryFlags.Salved))
                {
                    continue;
                }
            }
            else if (!IsRecoverableBruteInjury(candidate))
            {
                continue;
            }

            injury = candidate;
            return true;
        }

        injury = default;
        return false;
    }

    private static bool TryFindReducibleInjury(
        HumanMedicalComponent medical,
        TreatmentAttempt attempt,
        bool requireBurn,
        out InjuryRecord injury)
    {
        foreach (var candidate in medical.Injuries)
        {
            if (attempt.InjuryId != 0 && candidate.Id != attempt.InjuryId)
                continue;
            if (attempt.InjuryId == 0 && candidate.Region != attempt.Region)
                continue;
            if (candidate.Damage <= FixedPoint2.Zero)
                continue;

            if (requireBurn)
            {
                if (candidate.Kind != InjuryKind.Burn ||
                    candidate.Flags.HasFlag(InjuryFlags.Salved))
                {
                    continue;
                }
            }
            else if (candidate.Flags.HasFlag(InjuryFlags.Sutured) ||
                     !IsLineRepairableBruteInjury(candidate.Kind))
            {
                continue;
            }

            injury = candidate;
            return true;
        }

        injury = default;
        return false;
    }

    private static bool TryFindLinkedBleedSource(
        HumanMedicalComponent medical,
        TreatmentAttempt attempt,
        InjuryRecord injury,
        out BleedSource bleed)
    {
        foreach (var source in medical.BleedSources)
        {
            if (!source.Active)
                continue;
            if (attempt.BleedSourceId != 0 && source.Id != attempt.BleedSourceId)
            {
                continue;
            }
            if (attempt.BleedSourceId == 0 &&
                source.SourceInjuryId != injury.Id &&
                source.Region != injury.Region)
            {
                continue;
            }

            bleed = source;
            return true;
        }

        bleed = default;
        return false;
    }

    private static bool IsSuturable(InjuryKind kind)
    {
        return kind is InjuryKind.Cut or InjuryKind.Puncture or InjuryKind.Stump or InjuryKind.SurgicalIncision;
    }

    private static bool IsBandageable(InjuryKind kind)
    {
        return kind is InjuryKind.Cut or InjuryKind.Puncture or InjuryKind.Bruise or InjuryKind.Stump;
    }

    private static bool IsRecoverableBruteInjury(InjuryRecord injury)
    {
        if (!IsBandageable(injury.Kind))
            return false;

        return injury.Flags.HasFlag(InjuryFlags.Bandaged) ||
            injury.Flags.HasFlag(InjuryFlags.Sutured) ||
            injury.Flags.HasFlag(InjuryFlags.Closed);
    }

    private static bool IsLineRepairableBruteInjury(InjuryKind kind)
    {
        return kind is InjuryKind.Cut or InjuryKind.Puncture or InjuryKind.Bruise;
    }

    private static bool TryGetRecoveryRate(
        TreatmentAttempt attempt,
        FixedPoint2 defaultRate,
        out FixedPoint2 recoveryRate)
    {
        if (attempt.Amount <= FixedPoint2.Zero)
        {
            recoveryRate = FixedPoint2.Zero;
            return false;
        }

        recoveryRate = FixedPoint2.Max(defaultRate, attempt.Amount / RecoveryAmountRateDivisor);
        return true;
    }

    private static FixedPoint2 GetRecoveryRate(
        TreatmentAttempt attempt,
        FixedPoint2 defaultRate)
    {
        if (attempt.Amount <= FixedPoint2.Zero)
            return defaultRate;

        return FixedPoint2.Max(defaultRate, attempt.Amount / RecoveryAmountRateDivisor);
    }

    private static bool TryGetAreaRepairAmount(
        FixedPoint2 regionDamage,
        FixedPoint2 injuryDamage,
        TreatmentAttempt attempt,
        out FixedPoint2 amount)
    {
        var requested = attempt.Amount > FixedPoint2.Zero
            ? attempt.Amount
            : FieldLineRepairAmount;
        var capped = FixedPoint2.Min(regionDamage / FixedPoint2.New(2), injuryDamage);
        amount = FixedPoint2.Min(requested, capped);
        return amount > FixedPoint2.Zero;
    }

    private static bool IsTourniquetable(BodyRegion region)
    {
        return region is BodyRegion.LeftArm or BodyRegion.RightArm or BodyRegion.LeftLeg or BodyRegion.RightLeg;
    }

    private static TreatmentRuleResult Applied(
        TreatmentEffect[] effects,
        MedicalDirtyFlags dirty)
    {
        return new TreatmentRuleResult(true, effects, dirty, string.Empty);
    }

    private static TreatmentRuleResult Fail(string reason)
    {
        return new TreatmentRuleResult(false, Array.Empty<TreatmentEffect>(), MedicalDirtyFlags.None, reason);
    }
}
