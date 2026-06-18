using System;
using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Systems;

namespace Content.Shared._CMU14.Medical.Human.Surgery;

public static class HumanSurgeryProcedureRules
{
    public static bool TryGetRequiredProcedureForRegion(
        HumanMedicalComponent medical,
        BodyRegion region,
        out SurgeryProcedureId procedureId)
    {
        if (region == BodyRegion.None)
        {
            procedureId = SurgeryProcedureId.None;
            return false;
        }

        if (TryFindOpenStump(medical, region, out _))
        {
            procedureId = SurgeryProcedureId.SealStump;
            return true;
        }

        if (TryFindSuturableWound(medical, region, out _))
        {
            procedureId = SurgeryProcedureId.SutureWound;
            return true;
        }

        if (TryFindInternalBleed(medical, region, out _))
        {
            procedureId = SurgeryProcedureId.RepairInternalBleeding;
            return true;
        }

        if (TryFindDebridementBurn(medical, region, out _))
        {
            procedureId = SurgeryProcedureId.RemoveEschar;
            return true;
        }

        if (TryFindDamagedOrgan(medical, region, out _))
        {
            procedureId = SurgeryProcedureId.RepairOrgan;
            return true;
        }

        var state = HumanMedicalLedger.GetRegion(medical, region);
        if (state.Skeletal.Broken)
        {
            procedureId = SurgeryProcedureId.RepairFracture;
            return true;
        }

        procedureId = SurgeryProcedureId.None;
        return false;
    }

    public static bool TryReserveOperation(
        List<SurgeryOperationState> operations,
        SurgeryAttempt attempt,
        out string failure)
    {
        failure = string.Empty;

        if (attempt.Region == BodyRegion.None)
        {
            failure = "Surgery must target a body region.";
            return false;
        }

        if (attempt.ProcedureId == SurgeryProcedureId.None)
        {
            failure = "Surgery must have a procedure identity.";
            return false;
        }

        var index = FindOperationIndex(operations, attempt.Region);
        if (index < 0)
        {
            operations.Add(new SurgeryOperationState(
                attempt.Region,
                attempt.ProcedureId,
                attempt.StepIndex,
                committed: false));
            return true;
        }

        var active = operations[index];
        if (active.ProcedureId == attempt.ProcedureId)
            return true;

        failure = $"A {active.ProcedureId} procedure is already active on {active.Region}.";
        return false;
    }

    public static bool MarkOperationApplied(
        List<SurgeryOperationState> operations,
        SurgeryAttempt attempt,
        bool completeProcedure)
    {
        var index = FindOperationIndex(operations, attempt.Region);
        if (index < 0)
            return false;

        var active = operations[index];
        if (active.ProcedureId != attempt.ProcedureId)
            return false;

        if (completeProcedure)
        {
            operations.RemoveAt(index);
            return true;
        }

        operations[index] = active with
        {
            Committed = true,
            StepIndex = Math.Max(active.StepIndex, attempt.StepIndex + 1),
        };
        return true;
    }

    public static bool CancelUncommittedOperation(
        List<SurgeryOperationState> operations,
        SurgeryAttempt attempt)
    {
        var index = FindOperationIndex(operations, attempt.Region);
        if (index < 0)
            return false;

        var active = operations[index];
        if (active.ProcedureId != attempt.ProcedureId ||
            active.Committed)
        {
            return false;
        }

        operations.RemoveAt(index);
        return true;
    }

    public static bool HasCommittedOperation(
        IReadOnlyList<SurgeryOperationState> operations,
        SurgeryAttempt attempt)
    {
        var index = FindOperationIndex(operations, attempt.Region);
        return index >= 0 &&
            operations[index].ProcedureId == attempt.ProcedureId &&
            operations[index].Committed;
    }

    public static bool IsProcedureCompleteAfterStep(SurgeryAttempt attempt)
    {
        if (attempt.Step == SurgeryStepKind.RemoveForeignObject &&
            attempt.ProcedureId == SurgeryProcedureId.BrainDamageSurgery)
        {
            return false;
        }

        if (attempt.ProcedureId == SurgeryProcedureId.SurgicalAccess)
        {
            return attempt.Step switch
            {
                SurgeryStepKind.PrepareIncision => true,
                SurgeryStepKind.OpenIncision => false,
                SurgeryStepKind.ClampBleeders => false,
                SurgeryStepKind.RetractIncision => !IsEncasedRegion(attempt.Region),
                SurgeryStepKind.DeepAccess => true,
                SurgeryStepKind.MendBoneAccess => true,
                SurgeryStepKind.CloseIncision => true,
                _ => false,
            };
        }

        return attempt.Step is
            SurgeryStepKind.SutureWound or
            SurgeryStepKind.RemoveForeignObject or
            SurgeryStepKind.RepairOrgan or
            SurgeryStepKind.RepairEyes or
            SurgeryStepKind.RepairBrainDamage or
            SurgeryStepKind.RepairFracture or
            SurgeryStepKind.SealBoneWithGel or
            SurgeryStepKind.SetGraftedBone or
            SurgeryStepKind.RepairInternalBleed or
            SurgeryStepKind.RepairStump or
            SurgeryStepKind.RemoveEschar or
            SurgeryStepKind.RemoveEmbryo or
            SurgeryStepKind.CancelAmputation or
            SurgeryStepKind.AmputateLimb or
            SurgeryStepKind.AttachBiologicalLimb or
            SurgeryStepKind.FitProsthetic or
            SurgeryStepKind.RemoveProsthetic or
            SurgeryStepKind.CloseIncision;
    }

    private static bool IsEncasedRegion(BodyRegion region)
    {
        return region is BodyRegion.Head or BodyRegion.Chest;
    }

    public static bool TryFindSuturableWound(
        HumanMedicalComponent medical,
        BodyRegion region,
        out InjuryRecord injury)
    {
        foreach (var candidate in medical.Injuries)
        {
            if (candidate.Region != region ||
                candidate.Flags.HasFlag(InjuryFlags.Sutured) ||
                candidate.Flags.HasFlag(InjuryFlags.Closed))
            {
                continue;
            }

            if (candidate.Kind is not (InjuryKind.Cut or InjuryKind.Puncture or InjuryKind.SurgicalIncision))
                continue;

            injury = candidate;
            return true;
        }

        injury = default;
        return false;
    }

    public static bool TryFindOpenStump(
        HumanMedicalComponent medical,
        BodyRegion region,
        out InjuryRecord stump)
    {
        foreach (var injury in medical.Injuries)
        {
            if (injury.Region != region ||
                !injury.IsOpenStump)
            {
                continue;
            }

            stump = injury;
            return true;
        }

        stump = default;
        return false;
    }

    public static bool TryFindDebridementBurn(
        HumanMedicalComponent medical,
        BodyRegion region,
        out InjuryRecord burn)
    {
        foreach (var injury in medical.Injuries)
        {
            if (injury.Region != region ||
                !IsDebridementBurn(injury))
            {
                continue;
            }

            burn = injury;
            return true;
        }

        burn = default;
        return false;
    }

    public static bool IsDebridementBurn(InjuryRecord injury)
    {
        if (injury.Kind != InjuryKind.Burn ||
            injury.Flags.HasFlag(InjuryFlags.Debrided))
        {
            return false;
        }

        return injury.Stage is InjuryStage.Severe or InjuryStage.Carbonised ||
            injury.Flags.HasFlag(InjuryFlags.Necrotic);
    }

    private static int FindOperationIndex(
        IReadOnlyList<SurgeryOperationState> operations,
        BodyRegion region)
    {
        for (var i = 0; i < operations.Count; i++)
        {
            if (operations[i].Region == region)
                return i;
        }

        return -1;
    }

    private static bool TryFindInternalBleed(
        HumanMedicalComponent medical,
        BodyRegion region,
        out BleedSource bleed)
    {
        foreach (var source in medical.BleedSources)
        {
            if (source.Region != region ||
                source.Kind != BleedKind.Internal ||
                source.Treatment.HasFlag(TreatmentFlags.Closed) ||
                source.Treatment.HasFlag(TreatmentFlags.Sutured))
            {
                continue;
            }

            bleed = source;
            return true;
        }

        bleed = default;
        return false;
    }

    private static bool TryFindDamagedOrgan(
        HumanMedicalComponent medical,
        BodyRegion region,
        out OrganState organ)
    {
        for (var i = 1; i < medical.Organs.Length; i++)
        {
            organ = medical.Organs[i];
            if (organ.Region == region &&
                organ.Damage > 0 &&
                !organ.Missing &&
                organ.Slot is not OrganSlot.Brain and not OrganSlot.Eyes)
            {
                return true;
            }
        }

        organ = default;
        return false;
    }
}
