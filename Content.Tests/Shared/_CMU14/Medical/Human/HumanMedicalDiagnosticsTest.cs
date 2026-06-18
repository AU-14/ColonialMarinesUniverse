using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Diagnostics;
using Content.Shared._CMU14.Medical.Human.Surgery;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._CMU14.Medical.Human.Care;
using Content.Shared._CMU14.Medical.Machines;
using Content.Shared._RMC14.Medical.Scanner;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using BodyPartSymmetry = Content.Shared.Body.Part.BodyPartSymmetry;
using BodyPartType = Content.Shared.Body.Part.BodyPartType;

namespace Content.Tests.Shared._CMU14.Medical.Human;

[TestFixture]
public sealed class HumanMedicalDiagnosticsTest
{
    [Test]
    public void HudReceivesCompactMedicalSummary()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var summaryComponent = new HumanMedicalSummaryComponent();
        AddInternalBleed(medical, BodyRegion.Chest, FixedPoint2.New(2));
        HumanMedicalLedger.RebuildSummaryIfDirty(medical);

        var state = HumanMedicalScannerBuiSystem.BuildHudSummaryState(medical, summaryComponent);

        Assert.Multiple(() =>
        {
            Assert.That(summaryComponent.Summary.Revision, Is.EqualTo(medical.Revision));
            Assert.That(state.Summary.Revision, Is.EqualTo(medical.Revision));
            Assert.That(state.Summary.HasInternalBleeding, Is.True);
            Assert.That(state.Summary.Alerts.HasFlag(MedicalAlertFlags.InternalBleeding), Is.True);
        });
    }

    [Test]
    public void SummaryChangesOnlyAfterRevisionChanges()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var first = HumanMedicalScannerBuiSystem.BuildHudSummaryState(medical);
        var second = HumanMedicalScannerBuiSystem.BuildHudSummaryState(medical);

        AddInternalBleed(medical, BodyRegion.Chest, FixedPoint2.New(2));
        var beforeRebuild = HumanMedicalScannerBuiSystem.BuildHudSummaryState(medical);
        HumanMedicalLedger.RebuildSummaryIfDirty(medical);
        var afterRebuild = HumanMedicalScannerBuiSystem.BuildHudSummaryState(medical);

        Assert.Multiple(() =>
        {
            Assert.That(second.Summary.Revision, Is.EqualTo(first.Summary.Revision));
            Assert.That(beforeRebuild.Summary.Revision, Is.EqualTo(first.Summary.Revision));
            Assert.That(beforeRebuild.Summary.HasInternalBleeding, Is.False);
            Assert.That(afterRebuild.Summary.Revision, Is.GreaterThan(first.Summary.Revision));
            Assert.That(afterRebuild.Summary.HasInternalBleeding, Is.True);
        });
    }

    [Test]
    public void ScannerCanRequestFullLedgerDetailsByRevision()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        AddInternalBleed(medical, BodyRegion.Chest, FixedPoint2.New(2));
        OpenIncision(medical, BodyRegion.Chest);
        HumanMedicalLedger.RebuildSummaryIfDirty(medical);

        var response = HumanMedicalScannerBuiSystem.BuildLedgerResponse(
            medical,
            new HumanMedicalScannerFullLedgerRequestMessage(-1));

        Assert.Multiple(() =>
        {
            Assert.That(response.Kind, Is.EqualTo(HumanMedicalScannerResponseKind.FullLedger));
            Assert.That(response.FullLedger, Is.Not.Null);
            Assert.That(response.FullLedger!.Revision, Is.EqualTo(medical.Revision));
            Assert.That(response.FullLedger.Regions, Has.Length.EqualTo(HumanMedicalComponent.RegionSlotCount));
            Assert.That(response.FullLedger.Organs, Has.Length.EqualTo(HumanMedicalComponent.OrganSlotCount));
            Assert.That(response.FullLedger.BleedSources, Has.Length.EqualTo(2));
            Assert.That(ContainsBleedKind(response.FullLedger.BleedSources, BleedKind.Internal), Is.True);
            Assert.That(ContainsBleedKind(response.FullLedger.BleedSources, BleedKind.External), Is.True);
            Assert.That(response.FullLedger.Summary.HasInternalBleeding, Is.True);
        });
    }

    [Test]
    public void CurrentScannerRevisionGetsCompactNoChangeResponse()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        AddInternalBleed(medical, BodyRegion.Chest, FixedPoint2.New(2));
        HumanMedicalLedger.RebuildSummaryIfDirty(medical);

        var response = HumanMedicalScannerBuiSystem.BuildLedgerResponse(
            medical,
            new HumanMedicalScannerFullLedgerRequestMessage(medical.Revision));

        Assert.Multiple(() =>
        {
            Assert.That(response.Kind, Is.EqualTo(HumanMedicalScannerResponseKind.NoChange));
            Assert.That(response.FullLedger, Is.Null);
            Assert.That(response.Revision, Is.EqualTo(medical.Revision));
        });
    }

    [Test]
    public void ClientSummaryContainsUiReadyStatusFlags()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        AddInternalBleed(medical, BodyRegion.Chest, FixedPoint2.New(3));
        DamageOrgan(medical, OrganSlot.LeftLung, FixedPoint2.New(30));
        BreakRegion(medical, BodyRegion.LeftArm);
        OpenIncision(medical, BodyRegion.Chest);
        HumanMedicalLedger.RebuildSummaryIfDirty(medical);

        var state = HumanMedicalScannerBuiSystem.BuildHudSummaryState(medical);
        var summary = state.Summary;

        Assert.Multiple(() =>
        {
            Assert.That(summary.HudStatus, Is.EqualTo(HudStatus.Critical));
            Assert.That(summary.WorstBleed, Is.AtLeast(BleedSeverity.Moderate));
            Assert.That(summary.HasInternalBleeding, Is.True);
            Assert.That(summary.HasBrokenUnsplintedLimb, Is.True);
            Assert.That(summary.HasOrganDamage, Is.True);
            Assert.That(summary.HasOpenIncision, Is.True);
            Assert.That(summary.Alerts.HasFlag(MedicalAlertFlags.Critical), Is.True);
            Assert.That(summary.Alerts.HasFlag(MedicalAlertFlags.InternalBleeding), Is.True);
            Assert.That(summary.Alerts.HasFlag(MedicalAlertFlags.BrokenUnsplintedLimb), Is.True);
            Assert.That(summary.Alerts.HasFlag(MedicalAlertFlags.OrganDamage), Is.True);
            Assert.That(summary.Alerts.HasFlag(MedicalAlertFlags.OpenIncision), Is.True);
        });
    }

    [Test]
    public void SummaryShowsCoreFractureEvenWhenSplinted()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var transaction = new MedicalTransaction(BodyRegion.Chest);
        transaction.Add(MedicalEffect.SetSkeletalState(
            BodyRegion.Chest,
            broken: true,
            splinted: true));
        HumanMedicalLedger.ApplyTransaction(medical, transaction);
        HumanMedicalLedger.RebuildSummaryIfDirty(medical);

        var summary = HumanMedicalScannerBuiSystem.BuildHudSummaryState(medical).Summary;

        Assert.Multiple(() =>
        {
            Assert.That(summary.HasCoreFracture, Is.True);
            Assert.That(summary.HasBrokenUnsplintedLimb, Is.False);
            Assert.That(summary.Alerts.HasFlag(MedicalAlertFlags.CoreFracture), Is.True);
        });
    }

    [Test]
    public void SummaryShowsSuppressedInternalBleedAsNeedsSurgery()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        AddInternalBleed(medical, BodyRegion.LeftLeg, FixedPoint2.New(2));

        var result = HumanTreatmentSystem.TryApplyTreatment(
            medical,
            new TreatmentAttempt(TreatmentKind.TemporaryBleedSuppression, BodyRegion.LeftLeg));
        HumanMedicalLedger.RebuildSummaryIfDirty(medical);
        var summary = HumanMedicalScannerBuiSystem.BuildHudSummaryState(medical).Summary;

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(summary.HasInternalBleeding, Is.True);
            Assert.That(summary.HasSuppressedBleeding, Is.True);
            Assert.That(summary.Alerts.HasFlag(MedicalAlertFlags.InternalBleeding), Is.True);
            Assert.That(summary.Alerts.HasFlag(MedicalAlertFlags.SuppressedBleedingNeedsSurgery), Is.True);
            Assert.That(summary.Alerts.HasFlag(MedicalAlertFlags.ActiveBleeding), Is.False);
        });
    }

    [Test]
    public void SummaryShowsSevereBurnUntilTissueDamageImproves()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var transaction = new MedicalTransaction(BodyRegion.LeftArm);
        transaction.Add(MedicalEffect.AddRegionDamage(
            BodyRegion.LeftArm,
            FixedPoint2.Zero,
            FixedPoint2.New(30)));
        transaction.Add(MedicalEffect.AddInjury(
            BodyRegion.LeftArm,
            InjuryKind.Burn,
            InjuryStage.Severe,
            FixedPoint2.New(30)));
        HumanMedicalLedger.ApplyTransaction(medical, transaction);
        HumanMedicalLedger.RebuildSummaryIfDirty(medical);

        var summary = HumanMedicalScannerBuiSystem.BuildHudSummaryState(medical).Summary;

        Assert.Multiple(() =>
        {
            Assert.That(summary.HasSevereBurn, Is.True);
            Assert.That(summary.Alerts.HasFlag(MedicalAlertFlags.SevereBurn), Is.True);
        });
    }

    [Test]
    public void HealthScannerStateReceivesHumanMedicalLedgerReadout()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        AddInternalBleed(medical, BodyRegion.Chest, FixedPoint2.New(2));
        HumanMedicalLedger.RebuildSummaryIfDirty(medical);

        var state = new HealthScannerBuiState(
            default,
            FixedPoint2.Zero,
            FixedPoint2.Zero,
            null,
            null,
            bleeding: false);

        HumanMedicalScannerBuiSystem.FillHealthScannerState(
            medical,
            state,
            includeFullLedger: true);

        Assert.Multiple(() =>
        {
            Assert.That(state.CMUHumanMedicalSummary, Is.Not.Null);
            Assert.That(state.CMUHumanMedicalSummary!.Value.Revision, Is.EqualTo(medical.Revision));
            Assert.That(state.CMUHumanMedicalSummary.Value.HasInternalBleeding, Is.True);
            Assert.That(state.CMUHumanMedicalLedger, Is.Not.Null);
            Assert.That(state.CMUHumanMedicalLedger!.BleedSources, Has.Length.EqualTo(1));
            Assert.That(state.CMUParts, Is.Not.Null);
            Assert.That(state.CMUParts, Has.Count.EqualTo(1));
            Assert.That(state.CMUInternalBleeds, Is.Not.Null);
            Assert.That(state.CMUInternalBleeds, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void HealthScannerStateDoesNotExposeUnsupportedPulseOrShockRisk()
    {
        var stateType = typeof(HealthScannerBuiState);

        Assert.Multiple(() =>
        {
            Assert.That(stateType.GetField("CMUHeartBpm"), Is.Null);
            Assert.That(stateType.GetField("CMUHeartStopped"), Is.Null);
            Assert.That(stateType.GetField("CMUPainShockRisk"), Is.Null);
            Assert.That(stateType.GetField("CMUPainShockSuppressed"), Is.Null);
        });
    }

    [Test]
    public void HealthScannerStateBuildsAnalyzerReadoutsFromHumanLedger()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        AddRegionDamage(medical, BodyRegion.LeftArm, FixedPoint2.New(20), FixedPoint2.Zero);
        AddRegionDamage(medical, BodyRegion.RightArm, FixedPoint2.New(45), FixedPoint2.New(15));
        AddInjury(medical, BodyRegion.RightArm, InjuryKind.Puncture, InjuryStage.Massive, FixedPoint2.New(45));
        AddInternalBleed(medical, BodyRegion.RightArm, FixedPoint2.New(2));
        BreakRegion(medical, BodyRegion.RightArm);
        DamageOrgan(medical, OrganSlot.Heart, FixedPoint2.New(35));
        HumanMedicalLedger.RebuildSummaryIfDirty(medical);

        var state = new HealthScannerBuiState(
            default,
            FixedPoint2.Zero,
            FixedPoint2.Zero,
            null,
            null,
            bleeding: false);
        state.Damage.Brute = FixedPoint2.New(999);
        state.Damage.Burn = FixedPoint2.New(999);
        state.Damage.Total = FixedPoint2.New(999);

        HumanMedicalScannerBuiSystem.FillHealthScannerState(
            medical,
            state,
            includeFullLedger: true);

        var leftArm = FindPart(state, BodyPartType.Arm, BodyPartSymmetry.Left);
        var rightArm = FindPart(state, BodyPartType.Arm, BodyPartSymmetry.Right);

        Assert.Multiple(() =>
        {
            Assert.That(state.Damage.Brute, Is.EqualTo(FixedPoint2.New(65)));
            Assert.That(state.Damage.Burn, Is.EqualTo(FixedPoint2.New(15)));
            Assert.That(state.Damage.Total, Is.EqualTo(FixedPoint2.New(80)));
            Assert.That(state.Damage.UntreatedBruteWounds, Is.True);
            Assert.That(state.CMUParts, Is.Not.Null);
            Assert.That(state.CMUParts, Has.Count.EqualTo(2));
            Assert.That(leftArm, Is.Not.Null);
            Assert.That(rightArm, Is.Not.Null);
            Assert.That(rightArm!.Value.Current, Is.LessThan(leftArm!.Value.Current));
            Assert.That(rightArm.Value.WoundDescriptor, Is.EqualTo(WoundSize.Massive));
            Assert.That(rightArm.Value.WoundMechanism, Is.EqualTo(WoundMechanism.Bullet));
            Assert.That(state.CMUFractures, Has.Some.Matches<CMUFractureReadout>(
                fracture => fracture.Part == BodyPartType.Arm && fracture.Symmetry == BodyPartSymmetry.Right));
            Assert.That(state.CMUInternalBleeds, Has.Some.Matches<CMUInternalBleedReadout>(
                bleed => bleed.Part == BodyPartType.Arm &&
                    bleed.Symmetry == BodyPartSymmetry.Right &&
                    bleed.ExactLocationKnown));
            Assert.That(state.CMUOrgans, Has.Some.Matches<CMUOrganReadout>(
                organ => organ.OrganName == "heart" && organ.Current < organ.Max));
        });
    }

    [Test]
    public void BodyScannerLinesCanBeBuiltFromHumanMedicalLedger()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        AddInternalBleed(medical, BodyRegion.Chest, FixedPoint2.New(2));
        DamageOrgan(medical, OrganSlot.LeftLung, FixedPoint2.New(30));
        BreakRegion(medical, BodyRegion.LeftArm);
        HumanMedicalLedger.RebuildSummaryIfDirty(medical);

        var lines = new List<CMUBodyScannerScanLine>();

        HumanMedicalScannerBuiSystem.AppendBodyScannerLines(medical, lines, PassthroughLoc);

        Assert.Multiple(() =>
        {
            Assert.That(lines, Has.Some.Matches<CMUBodyScannerScanLine>(
                line => line.Category == CMUBodyScannerScanCategory.Vitals));
            Assert.That(lines, Has.Some.Matches<CMUBodyScannerScanLine>(
                line => line.Category == CMUBodyScannerScanCategory.Body));
            Assert.That(lines, Has.Some.Matches<CMUBodyScannerScanLine>(
                line => line.Category == CMUBodyScannerScanCategory.Organs));
        });
    }

    [Test]
    public void BodyScannerLinesShowSuppressedBleedStillNeedsSurgery()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        AddInternalBleed(medical, BodyRegion.LeftLeg, FixedPoint2.New(2));
        HumanTreatmentSystem.TryApplyTreatment(
            medical,
            new TreatmentAttempt(TreatmentKind.TemporaryBleedSuppression, BodyRegion.LeftLeg));
        HumanMedicalLedger.RebuildSummaryIfDirty(medical);
        var lines = new List<CMUBodyScannerScanLine>();

        HumanMedicalScannerBuiSystem.AppendBodyScannerLines(medical, lines, PassthroughLoc);

        Assert.That(lines, Has.Some.Matches<CMUBodyScannerScanLine>(
            line => line.Text.Contains("cmu-body-scanner-human-region-bleed-suppressed-needs-surgery")));
    }

    private static void AddInternalBleed(
        HumanMedicalComponent medical,
        BodyRegion region,
        FixedPoint2 rate)
    {
        var transaction = new MedicalTransaction(region);
        transaction.Add(MedicalEffect.AddBleedSource(region, BleedKind.Internal, rate));
        HumanMedicalLedger.ApplyTransaction(medical, transaction);
    }

    private static bool ContainsBleedKind(BleedSource[] sources, BleedKind kind)
    {
        foreach (var source in sources)
        {
            if (source.Kind == kind)
                return true;
        }

        return false;
    }

    private static void DamageOrgan(
        HumanMedicalComponent medical,
        OrganSlot slot,
        FixedPoint2 amount)
    {
        var transaction = new MedicalTransaction(BodyRegion.Chest);
        transaction.Add(MedicalEffect.AddOrganDamage(slot, amount));
        HumanMedicalLedger.ApplyTransaction(medical, transaction);
    }

    private static void AddRegionDamage(
        HumanMedicalComponent medical,
        BodyRegion region,
        FixedPoint2 brute,
        FixedPoint2 burn)
    {
        var transaction = new MedicalTransaction(region);
        transaction.Add(MedicalEffect.AddRegionDamage(region, brute, burn));
        HumanMedicalLedger.ApplyTransaction(medical, transaction);
    }

    private static void AddInjury(
        HumanMedicalComponent medical,
        BodyRegion region,
        InjuryKind kind,
        InjuryStage stage,
        FixedPoint2 damage)
    {
        var transaction = new MedicalTransaction(region);
        transaction.Add(MedicalEffect.AddInjury(region, kind, stage, damage));
        HumanMedicalLedger.ApplyTransaction(medical, transaction);
    }

    private static void BreakRegion(HumanMedicalComponent medical, BodyRegion region)
    {
        var transaction = new MedicalTransaction(region);
        transaction.Add(MedicalEffect.SetSkeletalState(region, broken: true, splinted: false));
        HumanMedicalLedger.ApplyTransaction(medical, transaction);
    }

    private static void OpenIncision(HumanMedicalComponent medical, BodyRegion region)
    {
        HumanSurgerySystem.TryApplySurgery(
            medical,
            new SurgeryAttempt(region, SurgeryStepKind.OpenIncision, PatientAnesthetized: true));
    }

    private static CMUBodyPartReadout? FindPart(
        HealthScannerBuiState state,
        BodyPartType type,
        BodyPartSymmetry symmetry)
    {
        if (state.CMUParts is null)
            return null;

        foreach (var part in state.CMUParts)
        {
            if (part.Type == type && part.Symmetry == symmetry)
                return part;
        }

        return null;
    }

    private static string PassthroughLoc(string key, params (string, object)[] args)
    {
        if (args.Length == 0)
            return key;

        var values = new object[args.Length];
        for (var i = 0; i < args.Length; i++)
        {
            values[i] = args[i].Item2;
        }

        return $"{key} {string.Join(" ", values)}";
    }
}
