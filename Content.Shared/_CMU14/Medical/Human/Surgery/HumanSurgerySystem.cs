using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Rules;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._CMU14.Medical.Human.Care;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Shared._CMU14.Medical.Human.Surgery;

public sealed partial class HumanSurgerySystem : EntitySystem
{
    [Dependency] private SharedHumanMedicalSystem _medical = default!;

    public SurgeryResult TryApplySurgery(
        EntityUid patient,
        SurgeryAttempt attempt,
        HumanMedicalComponent? medical = null,
        EntityUid? surgeon = null)
    {
        if (!Resolve(patient, ref medical))
            return Fail("Target has no human medical ledger.");

        var result = TryApplySurgery(medical, attempt);
        if (!result.Applied)
            return result;

        _medical.RefreshActiveMarkers(patient, medical);

        var changed = new HumanSurgeryAppliedEvent(attempt, result, surgeon);
        RaiseLocalEvent(patient, ref changed);

        if (result.PainEventRequired)
        {
            var pain = new HumanSurgeryPainEvent(attempt, result, surgeon);
            RaiseLocalEvent(patient, ref pain);
        }

        return result;
    }

    public static SurgeryResult TryApplySurgery(
        HumanMedicalComponent medical,
        SurgeryAttempt attempt)
    {
        if (attempt.Region == BodyRegion.None)
            return Fail("Surgery must target a body region.");

        if (!CanApplySurgeryToRegion(medical, attempt))
            return Fail("The target region is missing.");

        return attempt.Step switch
        {
            SurgeryStepKind.OpenIncision => OpenIncision(medical, attempt),
            SurgeryStepKind.PrepareIncision => PrepareIncision(medical, attempt),
            SurgeryStepKind.ClampBleeders => ClampBleeders(medical, attempt),
            SurgeryStepKind.RetractIncision => ApplyIncisionTransition(
                medical,
                attempt,
                IncisionDepth.OpenSkin,
                IncisionDepth.Retracted),
            SurgeryStepKind.DeepAccess => ApplyIncisionTransition(
                medical,
                attempt,
                IncisionDepth.Retracted,
                IncisionDepth.DeepAccess),
            SurgeryStepKind.MendBoneAccess => ApplyIncisionTransition(
                medical,
                attempt,
                IncisionDepth.DeepAccess,
                IncisionDepth.Retracted),
            SurgeryStepKind.SutureWound => SutureWound(medical, attempt),
            SurgeryStepKind.CloseIncision => CloseIncision(medical, attempt),
            SurgeryStepKind.RepairOrgan => RepairOrgan(medical, attempt),
            SurgeryStepKind.RepairEyes => RepairSpecificOrgan(medical, attempt, OrganSlot.Eyes),
            SurgeryStepKind.RepairBrainDamage => RepairSpecificOrgan(medical, attempt, OrganSlot.Brain),
            SurgeryStepKind.RepairFracture => RepairFracture(medical, attempt),
            SurgeryStepKind.ApplyBoneGel => ApplyBoneGel(medical, attempt),
            SurgeryStepKind.SetBone => SetBone(medical, attempt),
            SurgeryStepKind.SealBoneWithGel => SealBoneWithGel(medical, attempt),
            SurgeryStepKind.ApplyBoneGraft => ApplyBoneGraft(medical, attempt),
            SurgeryStepKind.SetGraftedBone => SetGraftedBone(medical, attempt),
            SurgeryStepKind.RepairInternalBleed => RepairInternalBleed(medical, attempt),
            SurgeryStepKind.RepairStump => RepairStump(medical, attempt),
            SurgeryStepKind.RemoveForeignObject => Applied(MedicalDirtyFlags.None, attempt),
            SurgeryStepKind.RemoveEschar => RemoveEschar(medical, attempt),
            SurgeryStepKind.CutEmbryoRoots => Applied(MedicalDirtyFlags.None, attempt),
            SurgeryStepKind.RemoveEmbryo => Applied(MedicalDirtyFlags.None, attempt),
            SurgeryStepKind.SeverMuscles => Applied(MedicalDirtyFlags.None, attempt),
            SurgeryStepKind.CancelAmputation => Applied(MedicalDirtyFlags.None, attempt),
            SurgeryStepKind.AmputateLimb => AmputateLimb(medical, attempt),
            SurgeryStepKind.AttachBiologicalLimb => AttachLimb(medical, attempt, LimbPresence.Present),
            SurgeryStepKind.FitProsthetic => AttachLimb(medical, attempt, LimbPresence.Prosthetic),
            SurgeryStepKind.RemoveProsthetic => RemoveProsthetic(medical, attempt),
            _ => Fail($"Surgery step {attempt.Step} is not implemented."),
        };
    }

    public bool TryReserveOperation(
        EntityUid patient,
        SurgeryAttempt attempt,
        out string failure)
    {
        var operation = EnsureComp<ActiveHumanSurgeryOperationComponent>(patient);
        if (!HumanSurgeryProcedureRules.TryReserveOperation(operation.Operations, attempt, out failure))
        {
            if (operation.Operations.Count == 0)
                RemComp<ActiveHumanSurgeryOperationComponent>(patient);
            return false;
        }

        Dirty(patient, operation);
        return true;
    }

    public void MarkOperationApplied(
        EntityUid patient,
        SurgeryAttempt attempt)
    {
        if (!TryComp<ActiveHumanSurgeryOperationComponent>(patient, out var operation))
            return;

        var complete = HumanSurgeryProcedureRules.IsProcedureCompleteAfterStep(attempt);
        if (!HumanSurgeryProcedureRules.MarkOperationApplied(operation.Operations, attempt, complete))
            return;

        if (operation.Operations.Count == 0)
        {
            RemComp<ActiveHumanSurgeryOperationComponent>(patient);
            return;
        }

        Dirty(patient, operation);
    }

    public bool CancelUncommittedOperation(
        EntityUid patient,
        SurgeryAttempt attempt)
    {
        if (!TryComp<ActiveHumanSurgeryOperationComponent>(patient, out var operation))
            return false;

        if (!HumanSurgeryProcedureRules.CancelUncommittedOperation(operation.Operations, attempt))
            return false;

        if (operation.Operations.Count == 0)
        {
            RemComp<ActiveHumanSurgeryOperationComponent>(patient);
            return true;
        }

        Dirty(patient, operation);
        return true;
    }

    public void ClearActiveOperations(EntityUid patient)
    {
        if (HasComp<ActiveHumanSurgeryOperationComponent>(patient))
            RemComp<ActiveHumanSurgeryOperationComponent>(patient);
    }

    public bool HasCommittedOperation(
        EntityUid patient,
        SurgeryAttempt attempt)
    {
        return TryComp<ActiveHumanSurgeryOperationComponent>(patient, out var operation) &&
            HumanSurgeryProcedureRules.HasCommittedOperation(operation.Operations, attempt);
    }

    public bool TryGetActiveOperation(
        EntityUid patient,
        BodyRegion region,
        out SurgeryOperationState operation)
    {
        if (TryComp<ActiveHumanSurgeryOperationComponent>(patient, out var active))
        {
            foreach (var candidate in active.Operations)
            {
                if (candidate.Region != region)
                    continue;

                operation = candidate;
                return true;
            }
        }

        operation = default;
        return false;
    }

    public bool TryGetSingleActiveOperationRegion(
        EntityUid patient,
        out BodyRegion region)
    {
        if (TryComp<ActiveHumanSurgeryOperationComponent>(patient, out var active) &&
            active.Operations.Count == 1)
        {
            region = active.Operations[0].Region;
            return true;
        }

        region = BodyRegion.None;
        return false;
    }

    private static bool CanApplySurgeryToRegion(
        HumanMedicalComponent medical,
        SurgeryAttempt attempt)
    {
        var region = HumanMedicalLedger.GetRegion(medical, attempt.Region);

        if (IsIncisionManagementStep(attempt.Step) &&
            (region.Incision != IncisionDepth.Closed ||
             HasOpenStump(medical, attempt.Region)))
        {
            return true;
        }

        if (attempt.Step == SurgeryStepKind.RepairStump)
            return true;

        if (attempt.Step is SurgeryStepKind.AttachBiologicalLimb or SurgeryStepKind.FitProsthetic)
        {
            return region.Presence is LimbPresence.Missing or LimbPresence.Detached;
        }

        if (attempt.Step == SurgeryStepKind.RemoveProsthetic)
            return region.Presence == LimbPresence.Prosthetic;

        return region.Presence == LimbPresence.Present;
    }

    private static bool IsIncisionManagementStep(SurgeryStepKind step)
    {
        return step is
            SurgeryStepKind.OpenIncision or
            SurgeryStepKind.PrepareIncision or
            SurgeryStepKind.ClampBleeders or
            SurgeryStepKind.RetractIncision or
            SurgeryStepKind.DeepAccess or
            SurgeryStepKind.MendBoneAccess or
            SurgeryStepKind.CloseIncision;
    }

    private static bool HasOpenStump(HumanMedicalComponent medical, BodyRegion region)
    {
        foreach (var injury in medical.Injuries)
        {
            if (injury.Region == region && injury.IsOpenStump)
                return true;
        }

        return false;
    }

    private static SurgeryResult OpenIncision(
        HumanMedicalComponent medical,
        SurgeryAttempt attempt)
    {
        var region = HumanMedicalLedger.GetRegion(medical, attempt.Region);
        if (region.Incision != IncisionDepth.Closed)
            return Fail($"Surgery expected incision depth {IncisionDepth.Closed} but found {region.Incision}.");

        var transaction = new MedicalTransaction(attempt.Region);
        transaction.Add(MedicalEffect.SetIncisionDepth(attempt.Region, IncisionDepth.OpenSkin));
        transaction.Add(MedicalEffect.AddBleedSource(
            attempt.Region,
            BleedKind.External,
            FixedPoint2.New(1),
            flags: BleedFlags.Surgical));

        return FromTransaction(HumanMedicalLedger.ApplyTransaction(medical, transaction), attempt);
    }

    private static SurgeryResult PrepareIncision(
        HumanMedicalComponent medical,
        SurgeryAttempt attempt)
    {
        var region = HumanMedicalLedger.GetRegion(medical, attempt.Region);
        var targetDepth = attempt.Region is BodyRegion.Head or BodyRegion.Chest
            ? IncisionDepth.DeepAccess
            : IncisionDepth.Retracted;
        if (region.Incision >= targetDepth)
            return Fail($"Surgery expected incision depth below {targetDepth} but found {region.Incision}.");

        var transaction = new MedicalTransaction(attempt.Region);
        transaction.Add(MedicalEffect.SetIncisionDepth(attempt.Region, targetDepth));
        transaction.Add(MedicalEffect.AddBleedSource(
            attempt.Region,
            BleedKind.External,
            FixedPoint2.New(1),
            flags: BleedFlags.Surgical));

        return FromTransaction(HumanMedicalLedger.ApplyTransaction(medical, transaction), attempt);
    }

    private static SurgeryResult ApplyIncisionTransition(
        HumanMedicalComponent medical,
        SurgeryAttempt attempt,
        IncisionDepth expected,
        IncisionDepth next)
    {
        var region = HumanMedicalLedger.GetRegion(medical, attempt.Region);
        if (region.Incision != expected)
            return Fail($"Surgery expected incision depth {expected} but found {region.Incision}.");

        var transaction = new MedicalTransaction(attempt.Region);
        transaction.Add(MedicalEffect.SetIncisionDepth(attempt.Region, next));
        return FromTransaction(HumanMedicalLedger.ApplyTransaction(medical, transaction), attempt);
    }

    private static SurgeryResult CloseIncision(
        HumanMedicalComponent medical,
        SurgeryAttempt attempt)
    {
        var region = HumanMedicalLedger.GetRegion(medical, attempt.Region);
        if (region.Incision == IncisionDepth.Closed)
            return Fail("The target incision is already closed.");

        var transaction = new MedicalTransaction(attempt.Region);
        transaction.Add(MedicalEffect.SetIncisionDepth(attempt.Region, IncisionDepth.Closed));
        transaction.Add(MedicalEffect.ConvertBleedSources(
            attempt.Region,
            BleedKind.External,
            BleedKind.Internal,
            BleedFlags.Surgical));
        return FromTransaction(HumanMedicalLedger.ApplyTransaction(medical, transaction), attempt);
    }

    private static SurgeryResult ClampBleeders(
        HumanMedicalComponent medical,
        SurgeryAttempt attempt)
    {
        var region = HumanMedicalLedger.GetRegion(medical, attempt.Region);
        if (region.Incision != IncisionDepth.OpenSkin)
            return Fail($"Surgery expected incision depth {IncisionDepth.OpenSkin} but found {region.Incision}.");
        if (!HasActiveSurgicalBleed(medical, attempt.Region))
            return Fail("No surgical bleeders are open in the target region.");

        var transaction = new MedicalTransaction(attempt.Region);
        transaction.Add(MedicalEffect.CloseBleedSources(
            attempt.Region,
            BleedKind.External,
            BleedFlags.Surgical));
        return FromTransaction(HumanMedicalLedger.ApplyTransaction(medical, transaction), attempt);
    }

    private static SurgeryResult SutureWound(
        HumanMedicalComponent medical,
        SurgeryAttempt attempt)
    {
        var result = HumanTreatmentSystem.TryApplyTreatment(
            medical,
            new TreatmentAttempt(
                TreatmentKind.Suture,
                attempt.Region,
                InjuryId: attempt.InjuryId,
                BleedSourceId: attempt.BleedSourceId));
        return FromTreatment(result, attempt);
    }

    private static SurgeryResult RepairOrgan(
        HumanMedicalComponent medical,
        SurgeryAttempt attempt)
    {
        var organ = HumanMedicalLedger.GetOrgan(medical, attempt.OrganSlot);
        if (organ.Slot == OrganSlot.None || organ.Region != attempt.Region)
            return Fail("The target organ is not in the surgical region.");
        if (!HasOrganAccess(medical, organ))
            return Fail("Organ repair requires the correct surgical access.");

        var result = HumanTreatmentSystem.TryApplyTreatment(
            medical,
            new TreatmentAttempt(
                TreatmentKind.RepairOrgan,
                attempt.Region,
                attempt.OrganSlot));
        return FromTreatment(result, attempt);
    }

    private static SurgeryResult RepairFracture(
        HumanMedicalComponent medical,
        SurgeryAttempt attempt)
    {
        _ = medical;
        _ = attempt;
        return Fail("Fracture repair requires staged bone gel, bone setting, and graft steps.");
    }

    private static SurgeryResult ApplyBoneGel(
        HumanMedicalComponent medical,
        SurgeryAttempt attempt)
    {
        if (!TryGetRepairableFracture(medical, attempt.Region, out var skeletal, out var failure))
            return Fail(failure);
        if (skeletal.BoneGelApplied)
            return Fail("Bone gel is already applied.");

        var transaction = new MedicalTransaction(attempt.Region);
        transaction.Add(MedicalEffect.UpdateSkeletalFlags(
            attempt.Region,
            SkeletalStateFlags.BoneGelApplied));
        return FromTransaction(HumanMedicalLedger.ApplyTransaction(medical, transaction), attempt);
    }

    private static SurgeryResult SetBone(
        HumanMedicalComponent medical,
        SurgeryAttempt attempt)
    {
        if (!TryGetRepairableFracture(medical, attempt.Region, out var skeletal, out var failure))
            return Fail(failure);
        if (!skeletal.BoneGelApplied)
            return Fail("Bone setting requires bone gel first.");
        if (skeletal.BoneSet)
            return Fail("The bone is already set.");

        var transaction = new MedicalTransaction(attempt.Region);
        transaction.Add(MedicalEffect.UpdateSkeletalFlags(
            attempt.Region,
            SkeletalStateFlags.BoneSet));
        return FromTransaction(HumanMedicalLedger.ApplyTransaction(medical, transaction), attempt);
    }

    private static SurgeryResult SealBoneWithGel(
        HumanMedicalComponent medical,
        SurgeryAttempt attempt)
    {
        if (!TryGetRepairableFracture(medical, attempt.Region, out var skeletal, out var failure))
            return Fail(failure);
        if (RequiresBoneGraft(skeletal))
            return Fail("A shattered fracture requires a bone graft before final setting.");
        if (!skeletal.BoneGelApplied || !skeletal.BoneSet)
            return Fail("Bone sealing requires gel and setting first.");

        var transaction = new MedicalTransaction(attempt.Region);
        transaction.Add(MedicalEffect.SetSkeletalState(
            attempt.Region,
            broken: false,
            splinted: false));
        return FromTransaction(HumanMedicalLedger.ApplyTransaction(medical, transaction), attempt);
    }

    private static SurgeryResult ApplyBoneGraft(
        HumanMedicalComponent medical,
        SurgeryAttempt attempt)
    {
        if (!TryGetRepairableFracture(medical, attempt.Region, out var skeletal, out var failure))
            return Fail(failure);
        if (!RequiresBoneGraft(skeletal))
            return Fail("This fracture does not require a bone graft.");
        if (!skeletal.BoneGelApplied || !skeletal.BoneSet)
            return Fail("Bone grafting requires gel and initial setting first.");
        if (skeletal.BoneGrafted)
            return Fail("A bone graft is already applied.");

        var transaction = new MedicalTransaction(attempt.Region);
        transaction.Add(MedicalEffect.UpdateSkeletalFlags(
            attempt.Region,
            SkeletalStateFlags.BoneGrafted));
        return FromTransaction(HumanMedicalLedger.ApplyTransaction(medical, transaction), attempt);
    }

    private static SurgeryResult SetGraftedBone(
        HumanMedicalComponent medical,
        SurgeryAttempt attempt)
    {
        if (!TryGetRepairableFracture(medical, attempt.Region, out var skeletal, out var failure))
            return Fail(failure);
        if (!RequiresBoneGraft(skeletal))
            return Fail("This fracture does not require final graft setting.");
        if (!skeletal.BoneGelApplied || !skeletal.BoneSet || !skeletal.BoneGrafted)
            return Fail("Final setting requires gel, initial setting, and bone graft first.");

        var transaction = new MedicalTransaction(attempt.Region);
        transaction.Add(MedicalEffect.SetSkeletalState(
            attempt.Region,
            broken: false,
            splinted: false));
        return FromTransaction(HumanMedicalLedger.ApplyTransaction(medical, transaction), attempt);
    }

    private static SurgeryResult RepairInternalBleed(
        HumanMedicalComponent medical,
        SurgeryAttempt attempt)
    {
        if (!HasShallowAccess(medical, attempt.Region))
            return Fail("Internal bleed repair requires shallow surgical access.");

        if (!TryFindInternalBleed(medical, attempt, out var bleed))
            return Fail("No internal bleed source exists in the surgical region.");

        var plan = new TreatmentRuleResult(
            true,
            new[]
            {
                TreatmentEffect.UpdateBleedSource(
                    bleed.Region,
                    bleed.Id,
                    TreatmentFlags.Sutured | TreatmentFlags.Closed,
                    setBleedRate: true,
                    FixedPoint2.Zero),
            },
            MedicalDirtyFlags.Bleeding,
            string.Empty);

        return FromTreatment(HumanMedicalLedger.ApplyTreatmentPlan(medical, plan), attempt);
    }

    private static SurgeryResult RepairStump(
        HumanMedicalComponent medical,
        SurgeryAttempt attempt)
    {
        if (!HasShallowAccess(medical, attempt.Region))
            return Fail("Stump repair requires shallow surgical access.");

        if (!TryFindStump(medical, attempt, out var stump))
            return Fail("No open stump exists in the surgical region.");

        var bleedSourceId = attempt.BleedSourceId != 0
            ? attempt.BleedSourceId
            : FindActiveBleedId(medical, stump.Region);

        var result = HumanTreatmentSystem.TryApplyTreatment(
            medical,
            new TreatmentAttempt(
                TreatmentKind.Suture,
                attempt.Region,
                InjuryId: stump.Id,
                BleedSourceId: bleedSourceId));
        return FromTreatment(result, attempt);
    }

    private static SurgeryResult RepairSpecificOrgan(
        HumanMedicalComponent medical,
        SurgeryAttempt attempt,
        OrganSlot slot)
    {
        var organ = HumanMedicalLedger.GetOrgan(medical, slot);
        if (organ.Slot == OrganSlot.None || organ.Region != attempt.Region)
            return Fail("The target organ is not in the surgical region.");
        if (attempt.OrganSlot != OrganSlot.None && attempt.OrganSlot != slot)
            return Fail("The surgery targets a different organ.");
        if (!HasOrganAccess(medical, organ))
            return Fail("Organ repair requires the correct surgical access.");

        var result = HumanTreatmentSystem.TryApplyTreatment(
            medical,
            new TreatmentAttempt(
                TreatmentKind.RepairOrgan,
                attempt.Region,
                slot));
        return FromTreatment(result, attempt);
    }

    private static SurgeryResult RemoveEschar(
        HumanMedicalComponent medical,
        SurgeryAttempt attempt)
    {
        if (!HasShallowAccess(medical, attempt.Region))
            return Fail("Eschar removal requires shallow surgical access.");

        if (!HumanSurgeryProcedureRules.TryFindDebridementBurn(medical, attempt.Region, out var burn))
            return Fail("No eschar or severe burn exists in the surgical region.");

        var injuryId = attempt.InjuryId != 0
            ? attempt.InjuryId
            : burn.Id;
        if (injuryId != burn.Id)
            return Fail("The selected burn is not debridable.");

        var plan = new TreatmentRuleResult(
            true,
            new[]
            {
                TreatmentEffect.UpdateInjury(
                    burn.Region,
                    burn.Id,
                    InjuryFlags.Debrided,
                    InjuryFlags.Necrotic),
            },
            MedicalDirtyFlags.Injuries | MedicalDirtyFlags.Summary,
            string.Empty);

        return FromTreatment(HumanMedicalLedger.ApplyTreatmentPlan(medical, plan), attempt);
    }

    private static SurgeryResult AttachLimb(
        HumanMedicalComponent medical,
        SurgeryAttempt attempt,
        LimbPresence presence)
    {
        var region = HumanMedicalLedger.GetRegion(medical, attempt.Region);
        if (region.Presence is not (LimbPresence.Missing or LimbPresence.Detached))
            return Fail("The target region is not missing a limb.");

        var transaction = new MedicalTransaction(attempt.Region);
        transaction.Add(MedicalEffect.SetRegionPresence(attempt.Region, presence));

        var stumpRegion = LimbLossRules.GetStumpAnchorRegion(attempt.Region);
        if (stumpRegion != BodyRegion.None)
            transaction.Add(MedicalEffect.CloseStumpRecords(stumpRegion));

        if (presence == LimbPresence.Present)
            transaction.Add(MedicalEffect.MarkDetachedLimbReattached(attempt.Region));

        return FromTransaction(HumanMedicalLedger.ApplyTransaction(medical, transaction), attempt);
    }

    private static SurgeryResult AmputateLimb(
        HumanMedicalComponent medical,
        SurgeryAttempt attempt)
    {
        var region = HumanMedicalLedger.GetRegion(medical, attempt.Region);
        if (region.Presence != LimbPresence.Present)
            return Fail("The target region is not present.");

        var transaction = LimbLossRules.CreateTraumaticSeverance(
            attempt.Region,
            BleedKind.Stump,
            FixedPoint2.New(4),
            BleedFlags.Surgical);
        return FromTransaction(HumanMedicalLedger.ApplyTransaction(medical, transaction), attempt);
    }

    private static SurgeryResult RemoveProsthetic(
        HumanMedicalComponent medical,
        SurgeryAttempt attempt)
    {
        var region = HumanMedicalLedger.GetRegion(medical, attempt.Region);
        if (region.Presence != LimbPresence.Prosthetic)
            return Fail("The target region is not a prosthetic limb.");

        var transaction = new MedicalTransaction(attempt.Region);
        transaction.Add(MedicalEffect.SetRegionPresence(attempt.Region, LimbPresence.Missing));
        return FromTransaction(HumanMedicalLedger.ApplyTransaction(medical, transaction), attempt);
    }

    private static bool HasDeepAccess(HumanMedicalComponent medical, BodyRegion region)
    {
        var state = HumanMedicalLedger.GetRegion(medical, region);
        return state.Incision == IncisionDepth.DeepAccess ||
            HasFracturedBoneAccess(region, state);
    }

    private static bool HasShallowAccess(HumanMedicalComponent medical, BodyRegion region)
    {
        return HumanMedicalLedger.GetRegion(medical, region).Incision >= IncisionDepth.Retracted;
    }

    private static bool HasOrganAccess(HumanMedicalComponent medical, OrganState organ)
    {
        return organ.Region switch
        {
            BodyRegion.Head or BodyRegion.Chest => HasDeepAccess(medical, organ.Region),
            BodyRegion.Groin => HasShallowAccess(medical, organ.Region),
            _ => HasShallowAccess(medical, organ.Region),
        };
    }

    private static bool HasRequiredRepairAccess(HumanMedicalComponent medical, BodyRegion region)
    {
        return region is BodyRegion.Head or BodyRegion.Chest
            ? HasDeepAccess(medical, region)
            : HasShallowAccess(medical, region);
    }

    private static bool TryGetRepairableFracture(
        HumanMedicalComponent medical,
        BodyRegion region,
        out SkeletalState skeletal,
        out string failure)
    {
        if (!HasRequiredRepairAccess(medical, region))
        {
            skeletal = default;
            failure = "Fracture repair requires the correct surgical access.";
            return false;
        }

        skeletal = HumanMedicalLedger.GetRegion(medical, region).Skeletal;
        if (!skeletal.Broken)
        {
            failure = "The target region is not broken.";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    private static bool HasFracturedBoneAccess(BodyRegion region, RegionState state)
    {
        return region is BodyRegion.Head or BodyRegion.Chest &&
            state.Incision >= IncisionDepth.Retracted &&
            state.Skeletal.Broken &&
            state.Skeletal.Severity.IsAtLeast(FractureSeverity.Compound);
    }

    private static bool RequiresBoneGraft(SkeletalState skeletal)
    {
        return skeletal.Severity.IsAtLeast(FractureSeverity.Shattered);
    }

    private static bool TryFindInternalBleed(
        HumanMedicalComponent medical,
        SurgeryAttempt attempt,
        out BleedSource bleed)
    {
        foreach (var source in medical.BleedSources)
        {
            if (attempt.BleedSourceId != 0 && source.Id != attempt.BleedSourceId)
                continue;
            if (attempt.BleedSourceId == 0 && source.Region != attempt.Region)
                continue;
            if (source.Kind != BleedKind.Internal)
                continue;
            if (source.Treatment.HasFlag(TreatmentFlags.Closed) ||
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

    private static bool HasActiveSurgicalBleed(
        HumanMedicalComponent medical,
        BodyRegion region)
    {
        foreach (var source in medical.BleedSources)
        {
            if (source.Region == region &&
                source.Kind == BleedKind.External &&
                source.Flags.HasFlag(BleedFlags.Surgical) &&
                source.Active)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindStump(
        HumanMedicalComponent medical,
        SurgeryAttempt attempt,
        out InjuryRecord stump)
    {
        foreach (var injury in medical.Injuries)
        {
            if (attempt.InjuryId != 0 && injury.Id != attempt.InjuryId)
                continue;
            if (attempt.InjuryId == 0 && injury.Region != attempt.Region)
                continue;
            if (!injury.IsOpenStump)
                continue;

            stump = injury;
            return true;
        }

        stump = default;
        return false;
    }

    private static int FindActiveBleedId(HumanMedicalComponent medical, BodyRegion region)
    {
        foreach (var source in medical.BleedSources)
        {
            if (source.Region == region && source.Active)
                return source.Id;
        }

        return 0;
    }

    private static SurgeryResult FromTransaction(
        MedicalTransactionResult result,
        SurgeryAttempt attempt)
    {
        return result.Applied
            ? Applied(result.DirtyFlags, attempt)
            : Fail(result.FailureReason);
    }

    private static SurgeryResult FromTreatment(
        TreatmentResult result,
        SurgeryAttempt attempt)
    {
        return result.Applied
            ? Applied(result.DirtyFlags, attempt)
            : Fail(result.FailureReason);
    }

    private static SurgeryResult Applied(MedicalDirtyFlags dirty, SurgeryAttempt attempt)
    {
        return new SurgeryResult(true, dirty, string.Empty)
        {
            PainEventRequired = RequiresPainEvent(attempt),
        };
    }

    private static SurgeryResult Fail(string reason)
    {
        return new SurgeryResult(false, MedicalDirtyFlags.None, reason);
    }

    private static bool RequiresPainEvent(SurgeryAttempt attempt)
    {
        return !attempt.PatientAnesthetized && !attempt.PatientPainkilled;
    }
}

[ByRefEvent]
public readonly record struct HumanSurgeryAppliedEvent(
    SurgeryAttempt Attempt,
    SurgeryResult Result,
    EntityUid? Surgeon = null);

[ByRefEvent]
public readonly record struct HumanSurgeryPainEvent(
    SurgeryAttempt Attempt,
    SurgeryResult Result,
    EntityUid? Surgeon = null);
