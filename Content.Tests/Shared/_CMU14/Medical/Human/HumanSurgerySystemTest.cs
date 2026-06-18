using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Rules;
using Content.Shared._CMU14.Medical.Human.Surgery;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.Tests.Shared._CMU14.Medical.Human;

[TestFixture]
public sealed class HumanSurgerySystemTest
{
    [Test]
    public void IncisionProgressesClosedToOpenSkinToRetractedToDeepAccess()
    {
        var medical = HumanMedicalLedger.CreateDefault();

        var open = HumanSurgerySystem.TryApplySurgery(medical, Attempt(BodyRegion.Chest, SurgeryStepKind.OpenIncision));
        var clamp = HumanSurgerySystem.TryApplySurgery(medical, Attempt(BodyRegion.Chest, SurgeryStepKind.ClampBleeders));
        var retract = HumanSurgerySystem.TryApplySurgery(medical, Attempt(BodyRegion.Chest, SurgeryStepKind.RetractIncision));
        var deep = HumanSurgerySystem.TryApplySurgery(medical, Attempt(BodyRegion.Chest, SurgeryStepKind.DeepAccess));
        var region = HumanMedicalLedger.GetRegion(medical, BodyRegion.Chest);

        Assert.Multiple(() =>
        {
            Assert.That(open.Applied, Is.True);
            Assert.That(clamp.Applied, Is.True);
            Assert.That(retract.Applied, Is.True);
            Assert.That(deep.Applied, Is.True);
            Assert.That(region.Incision, Is.EqualTo(IncisionDepth.DeepAccess));
            Assert.That(HasActiveSurgicalBleed(medical, BodyRegion.Chest), Is.False);
        });
    }

    [Test]
    public void ImpossibleIncisionTransitionFailsWithoutMutation()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var before = HumanMedicalLedger.GetRegion(medical, BodyRegion.Chest).Incision;
        var revision = medical.Revision;

        var result = HumanSurgerySystem.TryApplySurgery(medical, Attempt(BodyRegion.Chest, SurgeryStepKind.DeepAccess));
        var after = HumanMedicalLedger.GetRegion(medical, BodyRegion.Chest).Incision;

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.False);
            Assert.That(result.FailureReason, Is.Not.Empty);
            Assert.That(after, Is.EqualTo(before));
            Assert.That(medical.Revision, Is.EqualTo(revision));
        });
    }

    [Test]
    public void MissingRegionRejectsNonStumpSurgeryWithoutMutation()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        SetRegionPresence(medical, BodyRegion.RightArm, LimbPresence.Missing);
        var before = HumanMedicalLedger.GetRegion(medical, BodyRegion.RightArm);
        var revision = medical.Revision;

        var result = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.RightArm, SurgeryStepKind.OpenIncision));
        var after = HumanMedicalLedger.GetRegion(medical, BodyRegion.RightArm);

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.False);
            Assert.That(result.FailureReason, Does.Contain("missing"));
            Assert.That(after.Incision, Is.EqualTo(before.Incision));
            Assert.That(after.Presence, Is.EqualTo(before.Presence));
            Assert.That(medical.Revision, Is.EqualTo(revision));
        });
    }

    [Test]
    public void ProcedureLockRejectsDifferentProcedureInSameRegion()
    {
        var operations = new List<SurgeryOperationState>();
        var first = Attempt(
            BodyRegion.Chest,
            SurgeryStepKind.OpenIncision,
            procedureId: SurgeryProcedureId.RepairInternalBleeding);
        var continuation = Attempt(
            BodyRegion.Chest,
            SurgeryStepKind.RetractIncision,
            procedureId: SurgeryProcedureId.RepairInternalBleeding);
        var competing = Attempt(
            BodyRegion.Chest,
            SurgeryStepKind.OpenIncision,
            procedureId: SurgeryProcedureId.RepairOrgan);

        var reserved = HumanSurgeryProcedureRules.TryReserveOperation(operations, first, out var reserveFailure);
        var continued = HumanSurgeryProcedureRules.TryReserveOperation(operations, continuation, out var continueFailure);
        var rejected = HumanSurgeryProcedureRules.TryReserveOperation(operations, competing, out var rejectFailure);

        Assert.Multiple(() =>
        {
            Assert.That(reserved, Is.True, reserveFailure);
            Assert.That(continued, Is.True, continueFailure);
            Assert.That(rejected, Is.False);
            Assert.That(rejectFailure, Does.Contain("procedure"));
            Assert.That(operations, Has.Count.EqualTo(1));
            Assert.That(operations[0].ProcedureId, Is.EqualTo(SurgeryProcedureId.RepairInternalBleeding));
        });
    }

    [Test]
    public void CompletedProcedureReleasesRegionLock()
    {
        var operations = new List<SurgeryOperationState>();
        var repair = Attempt(
            BodyRegion.Chest,
            SurgeryStepKind.RepairInternalBleed,
            procedureId: SurgeryProcedureId.RepairInternalBleeding);
        var next = Attempt(
            BodyRegion.Chest,
            SurgeryStepKind.OpenIncision,
            procedureId: SurgeryProcedureId.RepairOrgan);

        HumanSurgeryProcedureRules.TryReserveOperation(operations, repair, out _);
        HumanSurgeryProcedureRules.MarkOperationApplied(operations, repair, completeProcedure: true);
        var reservedNext = HumanSurgeryProcedureRules.TryReserveOperation(operations, next, out var failure);

        Assert.Multiple(() =>
        {
            Assert.That(operations, Has.Count.EqualTo(1));
            Assert.That(reservedNext, Is.True, failure);
            Assert.That(operations[0].ProcedureId, Is.EqualTo(SurgeryProcedureId.RepairOrgan));
        });
    }

    [Test]
    public void HealthyRegionHasNoRequiredSurgicalProcedure()
    {
        var medical = HumanMedicalLedger.CreateDefault();

        var found = HumanSurgeryProcedureRules.TryGetRequiredProcedureForRegion(
            medical,
            BodyRegion.Chest,
            out var procedure);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.False);
            Assert.That(procedure, Is.EqualTo(SurgeryProcedureId.None));
        });
    }

    [Test]
    public void InternalBleedingSelectsInternalBleedingProcedure()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        AddBleed(medical, BodyRegion.Chest, BleedKind.Internal, FixedPoint2.New(2));

        var found = HumanSurgeryProcedureRules.TryGetRequiredProcedureForRegion(
            medical,
            BodyRegion.Chest,
            out var procedure);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(procedure, Is.EqualTo(SurgeryProcedureId.RepairInternalBleeding));
        });
    }

    [Test]
    public void OpenStumpSelectsSealStumpProcedure()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var severance = LimbLossRules.CreateTraumaticSeverance(
            BodyRegion.RightArm,
            BleedKind.Stump,
            FixedPoint2.New(4));
        HumanMedicalLedger.ApplyTransaction(medical, severance);

        var found = HumanSurgeryProcedureRules.TryGetRequiredProcedureForRegion(
            medical,
            BodyRegion.RightArm,
            out var procedure);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(procedure, Is.EqualTo(SurgeryProcedureId.SealStump));
        });
    }

    [Test]
    public void SealStumpProcedureKeepsRegionLockedThroughAccess()
    {
        var operations = new List<SurgeryOperationState>();
        var open = Attempt(
            BodyRegion.RightArm,
            SurgeryStepKind.OpenIncision,
            procedureId: SurgeryProcedureId.SealStump);
        var retract = Attempt(
            BodyRegion.RightArm,
            SurgeryStepKind.RetractIncision,
            procedureId: SurgeryProcedureId.SealStump);

        HumanSurgeryProcedureRules.TryReserveOperation(operations, open, out _);
        HumanSurgeryProcedureRules.MarkOperationApplied(
            operations,
            open,
            HumanSurgeryProcedureRules.IsProcedureCompleteAfterStep(open));
        HumanSurgeryProcedureRules.MarkOperationApplied(
            operations,
            retract,
            HumanSurgeryProcedureRules.IsProcedureCompleteAfterStep(retract));

        Assert.Multiple(() =>
        {
            Assert.That(operations, Has.Count.EqualTo(1));
            Assert.That(operations[0].ProcedureId, Is.EqualTo(SurgeryProcedureId.SealStump));
            Assert.That(operations[0].Committed, Is.True);
        });
    }

    [Test]
    public void ClosingGenericSurgicalAccessReleasesRegionLock()
    {
        var operations = new List<SurgeryOperationState>();
        var open = Attempt(
            BodyRegion.RightArm,
            SurgeryStepKind.OpenIncision,
            procedureId: SurgeryProcedureId.SurgicalAccess);
        var clamp = Attempt(
            BodyRegion.RightArm,
            SurgeryStepKind.ClampBleeders,
            procedureId: SurgeryProcedureId.SurgicalAccess);
        var close = Attempt(
            BodyRegion.RightArm,
            SurgeryStepKind.CloseIncision,
            procedureId: SurgeryProcedureId.SurgicalAccess);

        HumanSurgeryProcedureRules.TryReserveOperation(operations, open, out _);
        HumanSurgeryProcedureRules.MarkOperationApplied(
            operations,
            open,
            HumanSurgeryProcedureRules.IsProcedureCompleteAfterStep(open));
        HumanSurgeryProcedureRules.MarkOperationApplied(
            operations,
            clamp,
            HumanSurgeryProcedureRules.IsProcedureCompleteAfterStep(clamp));
        HumanSurgeryProcedureRules.MarkOperationApplied(
            operations,
            close,
            HumanSurgeryProcedureRules.IsProcedureCompleteAfterStep(close));

        Assert.That(operations, Is.Empty);
    }

    [Test]
    public void ChestOrganRepairRequiresDeepAccess()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        DamageOrgan(medical, OrganSlot.LeftLung, FixedPoint2.New(30));
        var before = HumanMedicalLedger.GetOrgan(medical, OrganSlot.LeftLung).Damage;

        var failed = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.Chest, SurgeryStepKind.RepairOrgan, organSlot: OrganSlot.LeftLung));
        OpenToDeepAccess(medical, BodyRegion.Chest);
        var repaired = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.Chest, SurgeryStepKind.RepairOrgan, organSlot: OrganSlot.LeftLung));
        var after = HumanMedicalLedger.GetOrgan(medical, OrganSlot.LeftLung).Damage;

        Assert.Multiple(() =>
        {
            Assert.That(failed.Applied, Is.False);
            Assert.That(repaired.Applied, Is.True);
            Assert.That(after, Is.LessThan(before));
        });
    }

    [Test]
    public void SutureWoundClosesCutAndLinkedBleed()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        AddCut(medical, BodyRegion.LeftArm, FixedPoint2.New(8));
        AddBleed(medical, BodyRegion.LeftArm, BleedKind.External, FixedPoint2.New(1));
        var injuryId = medical.Injuries[0].Id;
        var bleedId = medical.BleedSources[0].Id;

        var sutured = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(
                BodyRegion.LeftArm,
                SurgeryStepKind.SutureWound,
                injuryId: injuryId,
                bleedSourceId: bleedId,
                procedureId: SurgeryProcedureId.SutureWound));
        var wound = medical.Injuries[0];
        var bleed = medical.BleedSources[0];

        Assert.Multiple(() =>
        {
            Assert.That(sutured.Applied, Is.True);
            Assert.That(wound.Flags.HasFlag(InjuryFlags.Sutured), Is.True);
            Assert.That(wound.Flags.HasFlag(InjuryFlags.Closed), Is.True);
            Assert.That(bleed.Active, Is.False);
            Assert.That(bleed.Treatment.HasFlag(TreatmentFlags.Sutured), Is.True);
            Assert.That(bleed.Treatment.HasFlag(TreatmentFlags.Closed), Is.True);
        });
    }

    [Test]
    public void GroinOrganRepairUsesShallowAccess()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        DamageOrgan(medical, OrganSlot.Kidneys, FixedPoint2.New(30));
        OpenToShallowAccess(medical, BodyRegion.Groin);
        var before = HumanMedicalLedger.GetOrgan(medical, OrganSlot.Kidneys).Damage;

        var repaired = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.Groin, SurgeryStepKind.RepairOrgan, organSlot: OrganSlot.Kidneys));
        var after = HumanMedicalLedger.GetOrgan(medical, OrganSlot.Kidneys).Damage;

        Assert.Multiple(() =>
        {
            Assert.That(repaired.Applied, Is.True);
            Assert.That(after, Is.LessThan(before));
        });
    }

    [Test]
    public void FractureRepairUsesShallowAccessAndChecksSkeletalState()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        OpenToShallowAccess(medical, BodyRegion.LeftArm);

        var failed = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.LeftArm, SurgeryStepKind.ApplyBoneGel));
        BreakRegion(medical, BodyRegion.LeftArm, FractureSeverity.Simple);
        var shortcut = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.LeftArm, SurgeryStepKind.RepairFracture));
        var gel = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.LeftArm, SurgeryStepKind.ApplyBoneGel));
        var set = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.LeftArm, SurgeryStepKind.SetBone));
        var sealedBone = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.LeftArm, SurgeryStepKind.SealBoneWithGel));
        var skeletal = HumanMedicalLedger.GetRegion(medical, BodyRegion.LeftArm).Skeletal;

        Assert.Multiple(() =>
        {
            Assert.That(failed.Applied, Is.False);
            Assert.That(shortcut.Applied, Is.False);
            Assert.That(gel.Applied, Is.True);
            Assert.That(set.Applied, Is.True);
            Assert.That(sealedBone.Applied, Is.True);
            Assert.That(skeletal.Broken, Is.False);
            Assert.That(skeletal.Severity, Is.EqualTo(FractureSeverity.None));
            Assert.That(skeletal.Splinted, Is.False);
            Assert.That(skeletal.Knitting, Is.False);
        });
    }

    [Test]
    public void ShatteredFractureRequiresBoneGraftBeforeFinalSetting()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        OpenToShallowAccess(medical, BodyRegion.RightArm);
        BreakRegion(medical, BodyRegion.RightArm, FractureSeverity.Shattered);

        var gel = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.RightArm, SurgeryStepKind.ApplyBoneGel));
        var set = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.RightArm, SurgeryStepKind.SetBone));
        var wrongFinish = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.RightArm, SurgeryStepKind.SealBoneWithGel));
        var graft = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.RightArm, SurgeryStepKind.ApplyBoneGraft));
        var finalSet = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.RightArm, SurgeryStepKind.SetGraftedBone));
        var skeletal = HumanMedicalLedger.GetRegion(medical, BodyRegion.RightArm).Skeletal;

        Assert.Multiple(() =>
        {
            Assert.That(gel.Applied, Is.True);
            Assert.That(set.Applied, Is.True);
            Assert.That(wrongFinish.Applied, Is.False);
            Assert.That(graft.Applied, Is.True);
            Assert.That(finalSet.Applied, Is.True);
            Assert.That(skeletal.Broken, Is.False);
            Assert.That(skeletal.Severity, Is.EqualTo(FractureSeverity.None));
        });
    }

    [Test]
    public void LimbInternalBleedRepairUsesShallowAccessAndClosesIt()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        OpenToShallowAccess(medical, BodyRegion.LeftArm);

        var missing = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.LeftArm, SurgeryStepKind.RepairInternalBleed, bleedSourceId: 404));
        AddBleed(medical, BodyRegion.LeftArm, BleedKind.Internal, FixedPoint2.New(2));
        var bleedIndex = FindBleedIndex(medical, BleedKind.Internal);
        var bleedId = medical.BleedSources[bleedIndex].Id;
        var repaired = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.LeftArm, SurgeryStepKind.RepairInternalBleed, bleedSourceId: bleedId));
        var bleed = medical.BleedSources[bleedIndex];

        Assert.Multiple(() =>
        {
            Assert.That(missing.Applied, Is.False);
            Assert.That(repaired.Applied, Is.True);
            Assert.That(bleed.Active, Is.False);
            Assert.That(bleed.Rate, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(bleed.Treatment.HasFlag(TreatmentFlags.Closed), Is.True);
        });
    }

    [Test]
    public void ClosingIncisionConvertsUnclampedSurgicalBleedingToInternalBleeding()
    {
        var medical = HumanMedicalLedger.CreateDefault();

        var open = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.LeftArm, SurgeryStepKind.OpenIncision));
        var surgicalBleedId = medical.BleedSources[0].Id;
        var closed = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.LeftArm, SurgeryStepKind.CloseIncision));

        var region = HumanMedicalLedger.GetRegion(medical, BodyRegion.LeftArm);
        var convertedBleed = GetBleed(medical, surgicalBleedId);

        Assert.Multiple(() =>
        {
            Assert.That(open.Applied, Is.True);
            Assert.That(closed.Applied, Is.True);
            Assert.That(region.Incision, Is.EqualTo(IncisionDepth.Closed));
            Assert.That(convertedBleed.Kind, Is.EqualTo(BleedKind.Internal));
            Assert.That(convertedBleed.Rate, Is.EqualTo(FixedPoint2.New(1)));
            Assert.That(convertedBleed.Active, Is.True);
            Assert.That(convertedBleed.Flags.HasFlag(BleedFlags.Surgical), Is.True);
            Assert.That(HasActiveSurgicalBleed(medical, BodyRegion.LeftArm), Is.False);
        });
    }

    [Test]
    public void ClampBleedersStopsSurgicalBleedingWithoutRetractingIncision()
    {
        var medical = HumanMedicalLedger.CreateDefault();

        var open = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.LeftArm, SurgeryStepKind.OpenIncision));
        var clamped = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.LeftArm, SurgeryStepKind.ClampBleeders));
        var region = HumanMedicalLedger.GetRegion(medical, BodyRegion.LeftArm);

        Assert.Multiple(() =>
        {
            Assert.That(open.Applied, Is.True);
            Assert.That(clamped.Applied, Is.True);
            Assert.That(region.Incision, Is.EqualTo(IncisionDepth.OpenSkin));
            Assert.That(HasActiveSurgicalBleed(medical, BodyRegion.LeftArm), Is.False);
        });
    }

    [Test]
    public void LeftArmInternalBleedSurgeryRepairsBleedAndClosesIncision()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        AddBleed(medical, BodyRegion.LeftArm, BleedKind.Internal, FixedPoint2.New(2));
        var bleedId = FindBleedId(medical, BodyRegion.LeftArm, BleedKind.Internal);

        var open = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.LeftArm, SurgeryStepKind.OpenIncision));
        var clamp = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.LeftArm, SurgeryStepKind.ClampBleeders));
        var retract = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.LeftArm, SurgeryStepKind.RetractIncision));
        var repaired = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.LeftArm, SurgeryStepKind.RepairInternalBleed, bleedSourceId: bleedId));
        var closed = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.LeftArm, SurgeryStepKind.CloseIncision));

        var region = HumanMedicalLedger.GetRegion(medical, BodyRegion.LeftArm);
        var internalBleed = GetBleed(medical, bleedId);

        Assert.Multiple(() =>
        {
            Assert.That(open.Applied, Is.True);
            Assert.That(clamp.Applied, Is.True);
            Assert.That(retract.Applied, Is.True);
            Assert.That(repaired.Applied, Is.True);
            Assert.That(closed.Applied, Is.True);
            Assert.That(region.Incision, Is.EqualTo(IncisionDepth.Closed));
            Assert.That(internalBleed.Active, Is.False);
            Assert.That(internalBleed.Treatment.HasFlag(TreatmentFlags.Closed), Is.True);
            Assert.That(HasActiveBleed(medical, BodyRegion.LeftArm), Is.False);
        });
    }

    [Test]
    public void HeadInternalBleedSurgeryUsesShallowAccessRepairsBleedAndClosesIncision()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        AddBleed(medical, BodyRegion.Head, BleedKind.Internal, FixedPoint2.New(2));
        var bleedId = FindBleedId(medical, BodyRegion.Head, BleedKind.Internal);

        var open = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.Head, SurgeryStepKind.OpenIncision));
        var clamp = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.Head, SurgeryStepKind.ClampBleeders));
        var retract = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.Head, SurgeryStepKind.RetractIncision));
        var repaired = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.Head, SurgeryStepKind.RepairInternalBleed, bleedSourceId: bleedId));
        var closed = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.Head, SurgeryStepKind.CloseIncision));

        var region = HumanMedicalLedger.GetRegion(medical, BodyRegion.Head);
        var internalBleed = GetBleed(medical, bleedId);

        Assert.Multiple(() =>
        {
            Assert.That(open.Applied, Is.True);
            Assert.That(clamp.Applied, Is.True);
            Assert.That(retract.Applied, Is.True);
            Assert.That(repaired.Applied, Is.True);
            Assert.That(closed.Applied, Is.True);
            Assert.That(region.Incision, Is.EqualTo(IncisionDepth.Closed));
            Assert.That(internalBleed.Active, Is.False);
            Assert.That(internalBleed.Treatment.HasFlag(TreatmentFlags.Closed), Is.True);
            Assert.That(HasActiveBleed(medical, BodyRegion.Head), Is.False);
        });
    }

    [Test]
    public void ChestFractureSurgeryRepairsBoneThroughDeepAccessAndClosesIncision()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        BreakRegion(medical, BodyRegion.Chest, FractureSeverity.Simple);

        var open = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.Chest, SurgeryStepKind.OpenIncision));
        var clamp = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.Chest, SurgeryStepKind.ClampBleeders));
        var retract = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.Chest, SurgeryStepKind.RetractIncision));
        var shallowRepair = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.Chest, SurgeryStepKind.ApplyBoneGel));
        var deep = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.Chest, SurgeryStepKind.DeepAccess));
        var gel = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.Chest, SurgeryStepKind.ApplyBoneGel));
        var set = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.Chest, SurgeryStepKind.SetBone));
        var sealedBone = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.Chest, SurgeryStepKind.SealBoneWithGel));
        var mendBoneAccess = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.Chest, SurgeryStepKind.MendBoneAccess));
        var closed = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.Chest, SurgeryStepKind.CloseIncision));

        var region = HumanMedicalLedger.GetRegion(medical, BodyRegion.Chest);

        Assert.Multiple(() =>
        {
            Assert.That(open.Applied, Is.True);
            Assert.That(clamp.Applied, Is.True);
            Assert.That(retract.Applied, Is.True);
            Assert.That(shallowRepair.Applied, Is.False);
            Assert.That(deep.Applied, Is.True);
            Assert.That(gel.Applied, Is.True);
            Assert.That(set.Applied, Is.True);
            Assert.That(sealedBone.Applied, Is.True);
            Assert.That(mendBoneAccess.Applied, Is.True);
            Assert.That(closed.Applied, Is.True);
            Assert.That(region.Skeletal.Broken, Is.False);
            Assert.That(region.Skeletal.Splinted, Is.False);
            Assert.That(region.Incision, Is.EqualTo(IncisionDepth.Closed));
            Assert.That(HasActiveBleed(medical, BodyRegion.Chest), Is.False);
        });
    }

    [Test]
    public void CompoundCoreFractureAllowsDeepAccessFromShallowIncision()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        BreakRegion(medical, BodyRegion.Chest, FractureSeverity.Compound);
        BreakRegion(medical, BodyRegion.Head, FractureSeverity.Shattered);
        DamageOrgan(medical, OrganSlot.LeftLung, FixedPoint2.New(30));
        DamageOrgan(medical, OrganSlot.Brain, FixedPoint2.New(30));
        OpenToShallowAccess(medical, BodyRegion.Chest);
        OpenToShallowAccess(medical, BodyRegion.Head);

        var chestOrganRepair = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.Chest, SurgeryStepKind.RepairOrgan, organSlot: OrganSlot.LeftLung));
        var brainRepair = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.Head, SurgeryStepKind.RepairBrainDamage, organSlot: OrganSlot.Brain));

        Assert.Multiple(() =>
        {
            Assert.That(chestOrganRepair.Applied, Is.True);
            Assert.That(brainRepair.Applied, Is.True);
        });
    }

    [Test]
    public void CompoundAndShatteredCoreFracturesStillRequireAnIncisionBeforeDeepAccess()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        BreakRegion(medical, BodyRegion.Chest, FractureSeverity.Compound);
        BreakRegion(medical, BodyRegion.Head, FractureSeverity.Shattered);
        DamageOrgan(medical, OrganSlot.LeftLung, FixedPoint2.New(30));
        DamageOrgan(medical, OrganSlot.Brain, FixedPoint2.New(30));

        var chestOrganRepair = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.Chest, SurgeryStepKind.RepairOrgan, organSlot: OrganSlot.LeftLung));
        var brainRepair = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.Head, SurgeryStepKind.RepairBrainDamage, organSlot: OrganSlot.Brain));

        Assert.Multiple(() =>
        {
            Assert.That(chestOrganRepair.Applied, Is.False);
            Assert.That(brainRepair.Applied, Is.False);
        });
    }

    [Test]
    public void RightLegAmputationSurgerySeversLimbAndCreatesLegStump()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        OpenToShallowAccess(medical, BodyRegion.RightLeg);

        var severMuscles = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(
                BodyRegion.RightLeg,
                SurgeryStepKind.SeverMuscles,
                procedureId: SurgeryProcedureId.Amputation));
        var amputated = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(
                BodyRegion.RightLeg,
                SurgeryStepKind.AmputateLimb,
                procedureId: SurgeryProcedureId.Amputation));

        var leg = HumanMedicalLedger.GetRegion(medical, BodyRegion.RightLeg);

        Assert.Multiple(() =>
        {
            Assert.That(severMuscles.Applied, Is.True);
            Assert.That(amputated.Applied, Is.True);
            Assert.That(leg.Presence, Is.EqualTo(LimbPresence.Missing));
            Assert.That(leg.Incision, Is.EqualTo(IncisionDepth.Closed));
            Assert.That(medical.Injuries, Has.Some.Matches<InjuryRecord>(injury =>
                injury.Region == BodyRegion.RightLeg &&
                injury.Kind == InjuryKind.Stump &&
                injury.IsOpenStump));
            Assert.That(medical.BleedSources, Has.Some.Matches<BleedSource>(bleed =>
                bleed.Region == BodyRegion.RightLeg &&
                bleed.Kind == BleedKind.Stump &&
                bleed.Active));
            Assert.That(medical.DetachedLimbs, Has.Some.Matches<DetachedLimbRecord>(limb =>
                limb.Region == BodyRegion.RightLeg &&
                !limb.Reattached));
        });
    }

    [Test]
    public void AmputationCanBeCancelledBackToRetractedAccess()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        OpenToShallowAccess(medical, BodyRegion.RightLeg);

        var severMuscles = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(
                BodyRegion.RightLeg,
                SurgeryStepKind.SeverMuscles,
                procedureId: SurgeryProcedureId.Amputation));
        var cancel = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(
                BodyRegion.RightLeg,
                SurgeryStepKind.CancelAmputation,
                procedureId: SurgeryProcedureId.Amputation));
        var leg = HumanMedicalLedger.GetRegion(medical, BodyRegion.RightLeg);

        Assert.Multiple(() =>
        {
            Assert.That(severMuscles.Applied, Is.True);
            Assert.That(cancel.Applied, Is.True);
            Assert.That(leg.Presence, Is.EqualTo(LimbPresence.Present));
            Assert.That(leg.Incision, Is.EqualTo(IncisionDepth.Retracted));
        });
    }

    [Test]
    public void AmputationCancelReleasesCommittedOperation()
    {
        var operations = new List<SurgeryOperationState>();
        var severMuscles = Attempt(
            BodyRegion.RightLeg,
            SurgeryStepKind.SeverMuscles,
            procedureId: SurgeryProcedureId.Amputation);
        var cancel = Attempt(
            BodyRegion.RightLeg,
            SurgeryStepKind.CancelAmputation,
            procedureId: SurgeryProcedureId.Amputation);

        HumanSurgeryProcedureRules.TryReserveOperation(operations, severMuscles, out _);
        HumanSurgeryProcedureRules.MarkOperationApplied(
            operations,
            severMuscles,
            HumanSurgeryProcedureRules.IsProcedureCompleteAfterStep(severMuscles));
        HumanSurgeryProcedureRules.MarkOperationApplied(
            operations,
            cancel,
            HumanSurgeryProcedureRules.IsProcedureCompleteAfterStep(cancel));

        Assert.That(operations, Is.Empty);
    }

    [Test]
    public void StumpRepairChecksStumpInjury()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var missing = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(BodyRegion.RightLeg, SurgeryStepKind.RepairStump));
        var severance = LimbLossRules.CreateTraumaticSeverance(
            BodyRegion.RightLeg,
            BleedKind.Stump,
            FixedPoint2.New(4));
        HumanMedicalLedger.ApplyTransaction(medical, severance);
        OpenToShallowAccess(medical, BodyRegion.RightLeg);
        var stumpId = medical.Injuries[0].Id;
        var bleedId = medical.BleedSources[0].Id;

        var repaired = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(
                BodyRegion.RightLeg,
                SurgeryStepKind.RepairStump,
                injuryId: stumpId,
                bleedSourceId: bleedId));
        var stump = medical.Injuries[0];

        Assert.Multiple(() =>
        {
            Assert.That(missing.Applied, Is.False);
            Assert.That(repaired.Applied, Is.True);
            Assert.That(stump.IsOpenStump, Is.False);
        });
    }

    [Test]
    public void StumpRepairRequiresShallowAccess()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var severance = LimbLossRules.CreateTraumaticSeverance(
            BodyRegion.RightArm,
            BleedKind.Stump,
            FixedPoint2.New(4));
        HumanMedicalLedger.ApplyTransaction(medical, severance);
        var stumpId = medical.Injuries[0].Id;
        var bleedId = medical.BleedSources[0].Id;

        var repaired = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(
                BodyRegion.RightArm,
                SurgeryStepKind.RepairStump,
                injuryId: stumpId,
                bleedSourceId: bleedId,
                procedureId: SurgeryProcedureId.SealStump));

        Assert.Multiple(() =>
        {
            Assert.That(repaired.Applied, Is.False);
            Assert.That(repaired.FailureReason, Does.Contain("access"));
            Assert.That(medical.Injuries[0].IsOpenStump, Is.True);
            Assert.That(medical.BleedSources[0].Active, Is.True);
        });
    }

    [Test]
    public void RemoveEscharDebridesSevereBurnWithoutRemovingBurnInjury()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        AddBurn(medical, BodyRegion.LeftArm, FixedPoint2.New(55), InjuryFlags.Necrotic);
        var injuryId = medical.Injuries[0].Id;
        var beforeDamage = medical.Injuries[0].Damage;

        var closedSkin = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(
                BodyRegion.LeftArm,
                SurgeryStepKind.RemoveEschar,
                injuryId: injuryId,
                procedureId: SurgeryProcedureId.RemoveEschar));
        OpenToShallowAccess(medical, BodyRegion.LeftArm);
        var result = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(
                BodyRegion.LeftArm,
                SurgeryStepKind.RemoveEschar,
                injuryId: injuryId,
                procedureId: SurgeryProcedureId.RemoveEschar));
        var injury = medical.Injuries[0];

        Assert.Multiple(() =>
        {
            Assert.That(closedSkin.Applied, Is.False);
            Assert.That(closedSkin.FailureReason, Does.Contain("access"));
            Assert.That(result.Applied, Is.True);
            Assert.That(injury.Kind, Is.EqualTo(InjuryKind.Burn));
            Assert.That(injury.Damage, Is.EqualTo(beforeDamage));
            Assert.That(injury.Flags.HasFlag(InjuryFlags.Necrotic), Is.False);
            Assert.That(injury.Flags.HasFlag(InjuryFlags.Debrided), Is.True);
        });
    }

    [Test]
    public void UnanesthetizedSurgeryRequestsPainEventWithoutAddingDamage()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var beforeInjuries = medical.Injuries.Count;
        var beforeDamage = HumanMedicalLedger.GetRegion(medical, BodyRegion.Chest).BruteDamage;

        var result = HumanSurgerySystem.TryApplySurgery(
            medical,
            Attempt(
                BodyRegion.Chest,
                SurgeryStepKind.OpenIncision,
                anesthetized: false,
                painkilled: false));
        var after = HumanMedicalLedger.GetRegion(medical, BodyRegion.Chest);

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(result.PainEventRequired, Is.True);
            Assert.That(medical.Injuries, Has.Count.EqualTo(beforeInjuries));
            Assert.That(after.BruteDamage, Is.EqualTo(beforeDamage));
            Assert.That(medical.BleedSources, Has.Count.EqualTo(1));
            Assert.That(medical.BleedSources[0].Kind, Is.EqualTo(BleedKind.External));
            Assert.That(medical.BleedSources[0].Active, Is.True);
        });
    }

    private static SurgeryAttempt Attempt(
        BodyRegion region,
        SurgeryStepKind step,
        OrganSlot organSlot = OrganSlot.None,
        int injuryId = 0,
        int bleedSourceId = 0,
        bool anesthetized = true,
        bool painkilled = true,
        SurgeryProcedureId procedureId = SurgeryProcedureId.None)
    {
        return new SurgeryAttempt(
            region,
            step,
            organSlot,
            injuryId,
            bleedSourceId,
            anesthetized,
            painkilled,
            ProcedureId: procedureId);
    }

    private static void OpenToDeepAccess(
        Content.Shared._CMU14.Medical.Human.Components.HumanMedicalComponent medical,
        BodyRegion region)
    {
        OpenToShallowAccess(medical, region);
        HumanSurgerySystem.TryApplySurgery(medical, Attempt(region, SurgeryStepKind.DeepAccess));
    }

    private static void OpenToShallowAccess(
        Content.Shared._CMU14.Medical.Human.Components.HumanMedicalComponent medical,
        BodyRegion region)
    {
        HumanSurgerySystem.TryApplySurgery(medical, Attempt(region, SurgeryStepKind.OpenIncision));
        HumanSurgerySystem.TryApplySurgery(medical, Attempt(region, SurgeryStepKind.ClampBleeders));
        HumanSurgerySystem.TryApplySurgery(medical, Attempt(region, SurgeryStepKind.RetractIncision));
    }

    private static void DamageOrgan(
        Content.Shared._CMU14.Medical.Human.Components.HumanMedicalComponent medical,
        OrganSlot slot,
        FixedPoint2 amount)
    {
        var transaction = new MedicalTransaction(BodyRegion.Chest);
        transaction.Add(MedicalEffect.AddOrganDamage(slot, amount));
        HumanMedicalLedger.ApplyTransaction(medical, transaction);
    }

    private static void BreakRegion(
        Content.Shared._CMU14.Medical.Human.Components.HumanMedicalComponent medical,
        BodyRegion region,
        FractureSeverity severity = FractureSeverity.Simple)
    {
        var transaction = new MedicalTransaction(region);
        transaction.Add(MedicalEffect.SetSkeletalState(region, broken: true, splinted: true, severity));
        HumanMedicalLedger.ApplyTransaction(medical, transaction);
    }

    private static void AddBleed(
        Content.Shared._CMU14.Medical.Human.Components.HumanMedicalComponent medical,
        BodyRegion region,
        BleedKind kind,
        FixedPoint2 rate)
    {
        var transaction = new MedicalTransaction(region);
        transaction.Add(MedicalEffect.AddBleedSource(region, kind, rate));
        HumanMedicalLedger.ApplyTransaction(medical, transaction);
    }

    private static void AddCut(
        Content.Shared._CMU14.Medical.Human.Components.HumanMedicalComponent medical,
        BodyRegion region,
        FixedPoint2 damage)
    {
        var transaction = new MedicalTransaction(region);
        transaction.Add(MedicalEffect.AddInjury(
            region,
            InjuryKind.Cut,
            InjuryStage.Moderate,
            damage));
        HumanMedicalLedger.ApplyTransaction(medical, transaction);
    }

    private static void AddBurn(
        Content.Shared._CMU14.Medical.Human.Components.HumanMedicalComponent medical,
        BodyRegion region,
        FixedPoint2 damage,
        InjuryFlags flags)
    {
        var transaction = new MedicalTransaction(region);
        transaction.Add(MedicalEffect.AddRegionDamage(region, FixedPoint2.Zero, damage));
        transaction.Add(MedicalEffect.AddInjury(
            region,
            InjuryKind.Burn,
            InjuryRules.GetStage(InjuryKind.Burn, damage),
            damage));
        HumanMedicalLedger.ApplyTransaction(medical, transaction);

        var injury = medical.Injuries[0];
        injury.Flags |= flags;
        medical.Injuries[0] = injury;
    }

    private static void SetRegionPresence(
        Content.Shared._CMU14.Medical.Human.Components.HumanMedicalComponent medical,
        BodyRegion region,
        LimbPresence presence)
    {
        var transaction = new MedicalTransaction(region);
        transaction.Add(MedicalEffect.SetRegionPresence(region, presence));
        HumanMedicalLedger.ApplyTransaction(medical, transaction);
    }

    private static int FindBleedIndex(
        Content.Shared._CMU14.Medical.Human.Components.HumanMedicalComponent medical,
        BleedKind kind)
    {
        for (var i = 0; i < medical.BleedSources.Count; i++)
        {
            if (medical.BleedSources[i].Kind == kind)
                return i;
        }

        Assert.Fail($"Expected to find bleed kind {kind}.");
        return -1;
    }

    private static int FindBleedId(
        Content.Shared._CMU14.Medical.Human.Components.HumanMedicalComponent medical,
        BodyRegion region,
        BleedKind kind)
    {
        for (var i = 0; i < medical.BleedSources.Count; i++)
        {
            var bleed = medical.BleedSources[i];
            if (bleed.Region == region && bleed.Kind == kind)
                return bleed.Id;
        }

        Assert.Fail($"Expected to find {kind} bleed in {region}.");
        return 0;
    }

    private static BleedSource GetBleed(
        Content.Shared._CMU14.Medical.Human.Components.HumanMedicalComponent medical,
        int bleedId)
    {
        for (var i = 0; i < medical.BleedSources.Count; i++)
        {
            if (medical.BleedSources[i].Id == bleedId)
                return medical.BleedSources[i];
        }

        Assert.Fail($"Expected to find bleed source {bleedId}.");
        return default;
    }

    private static bool HasActiveBleed(
        Content.Shared._CMU14.Medical.Human.Components.HumanMedicalComponent medical,
        BodyRegion region)
    {
        for (var i = 0; i < medical.BleedSources.Count; i++)
        {
            var bleed = medical.BleedSources[i];
            if (bleed.Region == region && bleed.Active)
                return true;
        }

        return false;
    }

    private static bool HasActiveSurgicalBleed(
        Content.Shared._CMU14.Medical.Human.Components.HumanMedicalComponent medical,
        BodyRegion region)
    {
        for (var i = 0; i < medical.BleedSources.Count; i++)
        {
            var bleed = medical.BleedSources[i];
            if (bleed.Region == region &&
                bleed.Kind == BleedKind.External &&
                bleed.Flags.HasFlag(BleedFlags.Surgical) &&
                bleed.Active)
            {
                return true;
            }
        }

        return false;
    }
}
