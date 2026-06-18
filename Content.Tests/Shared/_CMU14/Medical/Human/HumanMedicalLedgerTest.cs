using System;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Diagnostics;
using Content.Shared._CMU14.Medical.Human.Rules;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._CMU14.Medical.Human.Care;
using Content.Shared.FixedPoint;
using NUnit.Framework;

namespace Content.Tests.Shared._CMU14.Medical.Human;

[TestFixture]
public sealed class HumanMedicalLedgerTest
{
    [Test]
    public void TransactionAppliesLedgerChangesOnceAndBuildsUiReadySummary()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var transaction = new MedicalTransaction(BodyRegion.Chest);

        transaction.Add(MedicalEffect.AddRegionDamage(BodyRegion.Chest, FixedPoint2.New(20), FixedPoint2.Zero));
        transaction.Add(MedicalEffect.AddInjury(
            BodyRegion.Chest,
            InjuryKind.Cut,
            InjuryStage.Deep,
            FixedPoint2.New(20)));
        transaction.Add(MedicalEffect.SetSkeletalState(BodyRegion.LeftArm, broken: true, splinted: false));
        transaction.Add(MedicalEffect.AddOrganDamage(OrganSlot.LeftLung, FixedPoint2.New(30)));
        transaction.Add(MedicalEffect.AddBleedSource(
            BodyRegion.Chest,
            BleedKind.Internal,
            FixedPoint2.New(2),
            sourceInjuryId: 1));

        var result = HumanMedicalLedger.ApplyTransaction(medical, transaction);
        var summaryRebuilt = HumanMedicalLedger.RebuildSummaryIfDirty(medical);
        var summary = MedicalSummaryBuilder.Build(medical);
        var activity = MedicalActivityClassifier.Classify(medical);

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(result.Revision, Is.EqualTo(1));
            Assert.That(medical.Revision, Is.EqualTo(1));
            Assert.That(summaryRebuilt, Is.True);
            Assert.That(medical.Summary.Revision, Is.EqualTo(1));
            Assert.That(medical.DirtyFlags.HasFlag(MedicalDirtyFlags.Summary), Is.False);
            Assert.That(HumanMedicalLedger.GetRegion(medical, BodyRegion.Chest).BruteDamage, Is.EqualTo(FixedPoint2.New(20)));
            Assert.That(medical.Injuries, Has.Count.EqualTo(1));
            Assert.That(HumanMedicalLedger.GetRegion(medical, BodyRegion.LeftArm).Skeletal.Broken, Is.True);
            Assert.That(HumanMedicalLedger.GetOrgan(medical, OrganSlot.LeftLung).Status, Is.EqualTo(OrganDamageStatus.Broken));
            Assert.That(medical.BleedSources, Has.Count.EqualTo(1));
            Assert.That(summary.HasInternalBleeding, Is.True);
            Assert.That(summary.HasBrokenUnsplintedLimb, Is.True);
            Assert.That(summary.HasOrganDamage, Is.True);
            Assert.That(summary.Alerts.HasFlag(MedicalAlertFlags.InternalBleeding), Is.True);
            Assert.That(activity.HasFlag(MedicalActivityFlags.ActiveBleeding), Is.True);
            Assert.That(activity.HasFlag(MedicalActivityFlags.ActiveOrganSymptoms), Is.True);
            Assert.That(activity.HasFlag(MedicalActivityFlags.ActiveMedicalSummaryDirty), Is.False);
        });
    }

    [Test]
    public void TransactionDefersSummaryRebuildUntilDirtyPass()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var transaction = new MedicalTransaction(BodyRegion.Chest);
        transaction.Add(MedicalEffect.AddBleedSource(
            BodyRegion.Chest,
            BleedKind.Internal,
            FixedPoint2.New(2)));

        var result = HumanMedicalLedger.ApplyTransaction(medical, transaction);
        var activity = MedicalActivityClassifier.Classify(medical);

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(medical.Summary.Revision, Is.Zero);
            Assert.That(medical.DirtyFlags.HasFlag(MedicalDirtyFlags.Summary), Is.True);
            Assert.That(activity.HasFlag(MedicalActivityFlags.ActiveMedicalSummaryDirty), Is.True);
        });

        Assert.That(HumanMedicalLedger.RebuildSummaryIfDirty(medical), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(medical.Summary.Revision, Is.EqualTo(1));
            Assert.That(medical.DirtyFlags.HasFlag(MedicalDirtyFlags.Summary), Is.False);
            Assert.That(medical.Summary.HasInternalBleeding, Is.True);
        });
    }

    [Test]
    public void DefaultLedgerUsesEnumIndexedRegionAndOrganArrays()
    {
        var medical = HumanMedicalLedger.CreateDefault();

        Assert.Multiple(() =>
        {
            Assert.That(medical.Regions, Has.Length.EqualTo((int) BodyRegion.RightFoot + 1));
            Assert.That(medical.Organs, Has.Length.EqualTo((int) OrganSlot.Ears + 1));
            Assert.That(medical.Regions[(int) BodyRegion.RightFoot].Region, Is.EqualTo(BodyRegion.RightFoot));
            Assert.That(medical.Regions[(int) BodyRegion.Head].Region, Is.EqualTo(BodyRegion.Head));
            Assert.That(medical.Organs[(int) OrganSlot.LeftLung].Slot, Is.EqualTo(OrganSlot.LeftLung));
            Assert.That(medical.Organs[(int) OrganSlot.Heart].Slot, Is.EqualTo(OrganSlot.Heart));
        });
    }

    [Test]
    public void TransactionExposesEffectsAsMemoryForVerifierSafeLocalSpanIteration()
    {
        var transaction = new MedicalTransaction(BodyRegion.Chest);
        transaction.Add(MedicalEffect.AddRegionDamage(
            BodyRegion.Chest,
            FixedPoint2.New(5),
            FixedPoint2.Zero));

        var effects = transaction.Effects;
        var span = effects.Span;
        ref readonly var effect = ref span[0];

        Assert.That(effects.Length, Is.EqualTo(1));
        Assert.That(typeof(MedicalTransaction).GetProperty(nameof(MedicalTransaction.Effects))?.PropertyType, Is.EqualTo(typeof(ReadOnlyMemory<MedicalEffect>)));
        Assert.That(effect.Kind, Is.EqualTo(MedicalEffectKind.AddRegionDamage));
        Assert.That(effect.Region, Is.EqualTo(BodyRegion.Chest));
    }

    [Test]
    public void FailedTransactionLeavesLedgerUnchanged()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var transaction = new MedicalTransaction(BodyRegion.RightArm);

        for (var i = 0; i <= HumanMedicalComponent.MaxInjuriesPerRegion; i++)
        {
            transaction.Add(MedicalEffect.AddInjury(
                BodyRegion.RightArm,
                InjuryKind.InternalBleed,
                InjuryStage.Small,
                FixedPoint2.New(10)));
        }

        var result = HumanMedicalLedger.ApplyTransaction(medical, transaction);

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.False);
            Assert.That(medical.Revision, Is.Zero);
            Assert.That(medical.Injuries, Is.Empty);
            Assert.That(medical.DirtyFlags, Is.EqualTo(MedicalDirtyFlags.None));
        });
    }

    [Test]
    public void TransactionRollsBackEarlierEffectsWhenLaterEffectFails()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var transaction = new MedicalTransaction(BodyRegion.Chest);

        transaction.Add(MedicalEffect.AddRegionDamage(
            BodyRegion.Chest,
            FixedPoint2.New(10),
            FixedPoint2.Zero));
        transaction.Add(MedicalEffect.AddOrganDamage(
            (OrganSlot) byte.MaxValue,
            FixedPoint2.New(10)));

        var result = HumanMedicalLedger.ApplyTransaction(medical, transaction);

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.False);
            Assert.That(medical.Revision, Is.Zero);
            Assert.That(HumanMedicalLedger.GetRegion(medical, BodyRegion.Chest).BruteDamage, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(medical.DirtyFlags, Is.EqualTo(MedicalDirtyFlags.None));
        });
    }

    [Test]
    public void TraumaticSeveranceCreatesMissingRegionStumpBleedAndDetachedRecord()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var transaction = LimbLossRules.CreateTraumaticSeverance(
            BodyRegion.LeftLeg,
            BleedKind.Stump,
            FixedPoint2.New(4),
            BleedFlags.Arterial);

        var result = HumanMedicalLedger.ApplyTransaction(medical, transaction);
        var region = HumanMedicalLedger.GetRegion(medical, BodyRegion.LeftLeg);
        var summary = MedicalSummaryBuilder.Build(medical);

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(region.Presence, Is.EqualTo(LimbPresence.Missing));
            Assert.That(medical.Injuries, Has.Some.Matches<InjuryRecord>(injury => injury.Kind == InjuryKind.Stump));
            Assert.That(medical.BleedSources, Has.Some.Matches<BleedSource>(bleed =>
                bleed.Region == BodyRegion.LeftLeg &&
                bleed.Kind == BleedKind.Stump &&
                bleed.Flags.HasFlag(BleedFlags.Arterial)));
            Assert.That(medical.DetachedLimbs, Has.Count.EqualTo(1));
            Assert.That(summary.HasOpenStump, Is.True);
            Assert.That(summary.Alerts.HasFlag(MedicalAlertFlags.MissingLimb), Is.True);
        });
    }

    [Test]
    public void FullLimbSeveranceAnchorsStumpOnMissingLimbRegion()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var transaction = LimbLossRules.CreateTraumaticSeverance(
            BodyRegion.RightArm,
            BleedKind.Stump,
            FixedPoint2.New(4),
            BleedFlags.Arterial);

        var result = HumanMedicalLedger.ApplyTransaction(medical, transaction);
        var arm = HumanMedicalLedger.GetRegion(medical, BodyRegion.RightArm);

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(arm.Presence, Is.EqualTo(LimbPresence.Missing));
            Assert.That(medical.Injuries, Has.Some.Matches<InjuryRecord>(injury =>
                injury.Region == BodyRegion.RightArm &&
                injury.Kind == InjuryKind.Stump &&
                injury.IsOpenStump));
            Assert.That(medical.BleedSources, Has.Some.Matches<BleedSource>(bleed =>
                bleed.Region == BodyRegion.RightArm &&
                bleed.Kind == BleedKind.Stump &&
                bleed.Active));
        });
    }

    [Test]
    public void DistalLimbSeveranceAnchorsStumpOnParentLimbRegion()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var transaction = LimbLossRules.CreateTraumaticSeverance(
            BodyRegion.RightHand,
            BleedKind.Stump,
            FixedPoint2.New(4));

        var result = HumanMedicalLedger.ApplyTransaction(medical, transaction);
        var hand = HumanMedicalLedger.GetRegion(medical, BodyRegion.RightHand);
        var arm = HumanMedicalLedger.GetRegion(medical, BodyRegion.RightArm);

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(hand.Presence, Is.EqualTo(LimbPresence.Missing));
            Assert.That(arm.Presence, Is.EqualTo(LimbPresence.Present));
            Assert.That(medical.Injuries, Has.Some.Matches<InjuryRecord>(injury =>
                injury.Region == BodyRegion.RightArm &&
                injury.Kind == InjuryKind.Stump &&
                injury.IsOpenStump));
            Assert.That(medical.BleedSources, Has.Some.Matches<BleedSource>(bleed =>
                bleed.Region == BodyRegion.RightArm &&
                bleed.Kind == BleedKind.Stump &&
                bleed.Active));
        });
    }

    [Test]
    public void ResetToHealthyClearsLedgerRecordsAndAdvancesRevision()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var transaction = LimbLossRules.CreateTraumaticSeverance(
            BodyRegion.LeftArm,
            BleedKind.Stump,
            FixedPoint2.New(4),
            BleedFlags.Arterial);
        HumanMedicalLedger.ApplyTransaction(medical, transaction);

        HumanMedicalLedger.ResetToHealthy(medical);
        HumanMedicalLedger.RebuildSummaryIfDirty(medical);

        Assert.Multiple(() =>
        {
            Assert.That(medical.Revision, Is.EqualTo(2));
            Assert.That(medical.Injuries, Is.Empty);
            Assert.That(medical.BleedSources, Is.Empty);
            Assert.That(medical.DetachedLimbs, Is.Empty);
            Assert.That(medical.Regions[(int) BodyRegion.LeftArm].Presence, Is.EqualTo(LimbPresence.Present));
            Assert.That(medical.Regions[(int) BodyRegion.LeftArm].Skeletal.Broken, Is.False);
            Assert.That(medical.Organs[(int) OrganSlot.Heart].Status, Is.EqualTo(OrganDamageStatus.None));
            Assert.That(medical.Summary.HudStatus, Is.EqualTo(HudStatus.Healthy));
            Assert.That(medical.DirtyFlags.HasFlag(MedicalDirtyFlags.Summary), Is.False);
        });
    }

    [Test]
    public void RepairAllSkeletalDamageClearsBrokenSplintedAndKnittingState()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var transaction = new MedicalTransaction(BodyRegion.LeftArm);
        transaction.Add(MedicalEffect.SetSkeletalState(BodyRegion.LeftArm, broken: true, splinted: false));
        HumanMedicalLedger.ApplyTransaction(medical, transaction);
        HumanTreatmentSystem.TryApplyTreatment(
            medical,
            new TreatmentAttempt(
                TreatmentKind.Cast,
                BodyRegion.LeftArm,
                Amount: FixedPoint2.New(30)));

        var result = HumanMedicalLedger.RepairAllSkeletalDamage(medical);
        var leftArm = HumanMedicalLedger.GetRegion(medical, BodyRegion.LeftArm);

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(result.Revision, Is.EqualTo(3));
            Assert.That(leftArm.Skeletal.Broken, Is.False);
            Assert.That(leftArm.Skeletal.Splinted, Is.False);
            Assert.That(leftArm.Skeletal.Knitting, Is.False);
            Assert.That(leftArm.Skeletal.KnittingSecondsRemaining, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(medical.DirtyFlags.HasFlag(MedicalDirtyFlags.Summary), Is.True);
        });
    }

    [Test]
    public void TransactionSetsOrganMissingStateWithoutDeletingDamage()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var damage = new MedicalTransaction(BodyRegion.Chest);
        damage.Add(MedicalEffect.AddOrganDamage(OrganSlot.Heart, FixedPoint2.New(20)));
        HumanMedicalLedger.ApplyTransaction(medical, damage);

        var remove = new MedicalTransaction(BodyRegion.Chest);
        remove.Add(MedicalEffect.SetOrganMissing(OrganSlot.Heart, missing: true));
        var removed = HumanMedicalLedger.ApplyTransaction(medical, remove);

        var reinsert = new MedicalTransaction(BodyRegion.Chest);
        reinsert.Add(MedicalEffect.SetOrganMissing(OrganSlot.Heart, missing: false));
        var reinserted = HumanMedicalLedger.ApplyTransaction(medical, reinsert);
        var heart = HumanMedicalLedger.GetOrgan(medical, OrganSlot.Heart);

        Assert.Multiple(() =>
        {
            Assert.That(removed.Applied, Is.True);
            Assert.That(reinserted.Applied, Is.True);
            Assert.That(heart.Missing, Is.False);
            Assert.That(heart.Damage, Is.EqualTo(FixedPoint2.New(20)));
            Assert.That(heart.Status, Is.EqualTo(OrganDamageStatus.Bruised));
        });
    }
}
