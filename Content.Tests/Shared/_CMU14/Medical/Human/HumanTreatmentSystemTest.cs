using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Rules;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._CMU14.Medical.Human.Care;
using Content.Shared.FixedPoint;
using NUnit.Framework;

namespace Content.Tests.Shared._CMU14.Medical.Human;

[TestFixture]
public sealed class HumanTreatmentSystemTest
{
    [Test]
    public void GauzeClosesExternalBleedSource()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        AddBleed(medical, BodyRegion.Chest, BleedKind.External, FixedPoint2.New(2));
        var bleedId = medical.BleedSources[0].Id;

        var result = HumanTreatmentSystem.TryApplyTreatment(
            medical,
            new TreatmentAttempt(TreatmentKind.Gauze, BodyRegion.Chest, BleedSourceId: bleedId));
        var bleed = medical.BleedSources[0];

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(result.DirtyFlags.HasFlag(MedicalDirtyFlags.Bleeding), Is.True);
            Assert.That(bleed.Active, Is.False);
            Assert.That(bleed.Rate, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(bleed.Treatment.HasFlag(TreatmentFlags.Bandaged), Is.True);
            Assert.That(bleed.Treatment.HasFlag(TreatmentFlags.Closed), Is.True);
        });
    }

    [Test]
    public void BleedSourceArterialFlagSurvivesTransaction()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var transaction = new MedicalTransaction(BodyRegion.Chest);
        transaction.Add(MedicalEffect.AddBleedSource(
            BodyRegion.Chest,
            BleedKind.External,
            FixedPoint2.New(4),
            flags: BleedFlags.Arterial));

        var result = HumanMedicalLedger.ApplyTransaction(medical, transaction);
        var bleed = medical.BleedSources[0];

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(bleed.Flags.HasFlag(BleedFlags.Arterial), Is.True);
            Assert.That(bleed.Active, Is.True);
        });
    }

    [Test]
    public void GauzeBandagesUntreatedCutInjuryWithoutBleeding()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var transaction = new MedicalTransaction(BodyRegion.RightArm);
        transaction.Add(MedicalEffect.AddRegionDamage(
            BodyRegion.RightArm,
            FixedPoint2.New(12),
            FixedPoint2.Zero));
        transaction.Add(MedicalEffect.AddInjury(
            BodyRegion.RightArm,
            InjuryKind.Cut,
            InjuryStage.Deep,
            FixedPoint2.New(12)));
        HumanMedicalLedger.ApplyTransaction(medical, transaction);
        var injuryId = medical.Injuries[0].Id;

        var result = HumanTreatmentSystem.TryApplyTreatment(
            medical,
            new TreatmentAttempt(TreatmentKind.Gauze, BodyRegion.RightArm, InjuryId: injuryId));
        var injury = medical.Injuries[0];

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(result.DirtyFlags.HasFlag(MedicalDirtyFlags.Injuries), Is.True);
            Assert.That(injury.Flags.HasFlag(InjuryFlags.Bandaged), Is.True);
            Assert.That(injury.Flags.HasFlag(InjuryFlags.Closed), Is.False);
            Assert.That(injury.Flags.HasFlag(InjuryFlags.Sutured), Is.False);
            Assert.That(injury.RecoveryRate, Is.GreaterThan(FixedPoint2.Zero));
        });
    }

    [Test]
    public void AdvancedTraumaKitStartsBruteRecoveryWithoutRepairingSurgeryProblems()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var transaction = new MedicalTransaction(BodyRegion.RightArm);
        transaction.Add(MedicalEffect.AddRegionDamage(
            BodyRegion.RightArm,
            FixedPoint2.New(12),
            FixedPoint2.Zero));
        transaction.Add(MedicalEffect.AddInjury(
            BodyRegion.RightArm,
            InjuryKind.Cut,
            InjuryStage.Deep,
            FixedPoint2.New(12)));
        transaction.Add(MedicalEffect.SetSkeletalState(
            BodyRegion.RightArm,
            broken: true,
            splinted: false));
        transaction.Add(MedicalEffect.AddOrganDamage(
            OrganSlot.Heart,
            FixedPoint2.New(15)));
        transaction.Add(MedicalEffect.AddBleedSource(
            BodyRegion.RightArm,
            BleedKind.Internal,
            FixedPoint2.New(1)));
        HumanMedicalLedger.ApplyTransaction(medical, transaction);

        var result = HumanTreatmentSystem.TryApplyTreatment(
            medical,
            new TreatmentAttempt(
                TreatmentKind.Gauze,
                BodyRegion.RightArm,
                InjuryId: medical.Injuries[0].Id,
                Amount: FixedPoint2.New(9)));
        var tick = HumanTreatedWoundHealingSystem.CalculateTreatedWoundHealingTick(medical);
        var heal = HumanMedicalLedger.AdvanceTreatedWoundHealing(medical, FixedPoint2.New(2));
        var region = HumanMedicalLedger.GetRegion(medical, BodyRegion.RightArm);
        var injury = medical.Injuries[0];
        var heart = HumanMedicalLedger.GetOrgan(medical, OrganSlot.Heart);

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(tick.ActiveInjuries, Is.EqualTo(1));
            Assert.That(heal.Applied, Is.True);
            Assert.That(region.BruteDamage, Is.LessThan(FixedPoint2.New(12)));
            Assert.That(region.Skeletal.Broken, Is.True);
            Assert.That(injury.Damage, Is.LessThan(FixedPoint2.New(12)));
            Assert.That(injury.Stage, Is.EqualTo(InjuryRules.GetStage(InjuryKind.Cut, injury.Damage)));
            Assert.That(heart.Damage, Is.EqualTo(FixedPoint2.New(15)));
            Assert.That(medical.BleedSources[0].Active, Is.True);
        });
    }

    [Test]
    public void SurgicalLineHealsTenBruteOnTargetAreaWithoutClearingEverything()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var transaction = new MedicalTransaction(BodyRegion.LeftArm);
        transaction.Add(MedicalEffect.AddRegionDamage(
            BodyRegion.LeftArm,
            FixedPoint2.New(30),
            FixedPoint2.Zero));
        transaction.Add(MedicalEffect.AddInjury(
            BodyRegion.LeftArm,
            InjuryKind.Cut,
            InjuryStage.Massive,
            FixedPoint2.New(30)));
        HumanMedicalLedger.ApplyTransaction(medical, transaction);

        var result = HumanTreatmentSystem.TryApplyTreatment(
            medical,
            new TreatmentAttempt(TreatmentKind.SurgicalLine, BodyRegion.LeftArm));
        var region = HumanMedicalLedger.GetRegion(medical, BodyRegion.LeftArm);
        var injury = medical.Injuries[0];

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(region.BruteDamage, Is.EqualTo(FixedPoint2.New(20)));
            Assert.That(injury.Damage, Is.EqualTo(FixedPoint2.New(20)));
            Assert.That(injury.Flags.HasFlag(InjuryFlags.Sutured), Is.True);
            Assert.That(injury.Flags.HasFlag(InjuryFlags.Closed), Is.False);
        });
    }

    [Test]
    public void SurgicalLineCapsSingleUseAtHalfCurrentAreaBruteDamage()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var transaction = new MedicalTransaction(BodyRegion.LeftHand);
        transaction.Add(MedicalEffect.AddRegionDamage(
            BodyRegion.LeftHand,
            FixedPoint2.New(12),
            FixedPoint2.Zero));
        transaction.Add(MedicalEffect.AddInjury(
            BodyRegion.LeftHand,
            InjuryKind.Cut,
            InjuryStage.Deep,
            FixedPoint2.New(12)));
        HumanMedicalLedger.ApplyTransaction(medical, transaction);

        var result = HumanTreatmentSystem.TryApplyTreatment(
            medical,
            new TreatmentAttempt(TreatmentKind.SurgicalLine, BodyRegion.LeftHand));
        var region = HumanMedicalLedger.GetRegion(medical, BodyRegion.LeftHand);
        var injury = medical.Injuries[0];

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(region.BruteDamage, Is.EqualTo(FixedPoint2.New(6)));
            Assert.That(injury.Damage, Is.EqualTo(FixedPoint2.New(6)));
        });
    }

    [Test]
    public void SalveTreatsBurnDamageAndBurnInjuriesOverTime()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var transaction = new MedicalTransaction(BodyRegion.LeftArm);
        transaction.Add(MedicalEffect.AddRegionDamage(
            BodyRegion.LeftArm,
            FixedPoint2.Zero,
            FixedPoint2.New(8)));
        transaction.Add(MedicalEffect.AddInjury(
            BodyRegion.LeftArm,
            InjuryKind.Burn,
            InjuryStage.Small,
            FixedPoint2.New(8)));
        HumanMedicalLedger.ApplyTransaction(medical, transaction);

        var result = HumanTreatmentSystem.TryApplyTreatment(
            medical,
            new TreatmentAttempt(TreatmentKind.Salve, BodyRegion.LeftArm));
        var region = HumanMedicalLedger.GetRegion(medical, BodyRegion.LeftArm);
        var injury = medical.Injuries[0];

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(result.DirtyFlags.HasFlag(MedicalDirtyFlags.Injuries), Is.True);
            Assert.That(region.BurnDamage, Is.EqualTo(FixedPoint2.New(8)));
            Assert.That(injury.Flags.HasFlag(InjuryFlags.Salved), Is.True);
            Assert.That(injury.RecoveryRate, Is.GreaterThan(FixedPoint2.Zero));
        });

        var heal = HumanMedicalLedger.AdvanceTreatedWoundHealing(medical, FixedPoint2.New(2));
        region = HumanMedicalLedger.GetRegion(medical, BodyRegion.LeftArm);
        injury = medical.Injuries[0];

        Assert.Multiple(() =>
        {
            Assert.That(heal.Applied, Is.True);
            Assert.That(region.BurnDamage, Is.LessThan(FixedPoint2.New(8)));
            Assert.That(injury.Damage, Is.LessThan(FixedPoint2.New(8)));
            Assert.That(injury.Stage, Is.EqualTo(InjuryRules.GetStage(InjuryKind.Burn, injury.Damage)));
        });
    }

    [Test]
    public void SyntheticGraftHealsTenBurnAndTreatsSevereBurn()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var transaction = new MedicalTransaction(BodyRegion.RightArm);
        transaction.Add(MedicalEffect.AddRegionDamage(
            BodyRegion.RightArm,
            FixedPoint2.Zero,
            FixedPoint2.New(30)));
        transaction.Add(MedicalEffect.AddInjury(
            BodyRegion.RightArm,
            InjuryKind.Burn,
            InjuryStage.Severe,
            FixedPoint2.New(30)));
        HumanMedicalLedger.ApplyTransaction(medical, transaction);

        var result = HumanTreatmentSystem.TryApplyTreatment(
            medical,
            new TreatmentAttempt(TreatmentKind.SyntheticGraft, BodyRegion.RightArm));
        var region = HumanMedicalLedger.GetRegion(medical, BodyRegion.RightArm);
        var injury = medical.Injuries[0];

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(region.BurnDamage, Is.EqualTo(FixedPoint2.New(20)));
            Assert.That(injury.Damage, Is.EqualTo(FixedPoint2.New(20)));
            Assert.That(injury.Flags.HasFlag(InjuryFlags.Salved), Is.True);
            Assert.That(injury.RecoveryRate, Is.EqualTo(FixedPoint2.Zero));
        });
    }

    [Test]
    public void SplintSetsBrokenLimbSplintedWithoutDeletingFracture()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var transaction = new MedicalTransaction(BodyRegion.LeftLeg);
        transaction.Add(MedicalEffect.SetSkeletalState(
            BodyRegion.LeftLeg,
            broken: true,
            splinted: false));
        HumanMedicalLedger.ApplyTransaction(medical, transaction);

        var result = HumanTreatmentSystem.TryApplyTreatment(
            medical,
            new TreatmentAttempt(TreatmentKind.Splint, BodyRegion.LeftLeg));
        var skeletal = HumanMedicalLedger.GetRegion(medical, BodyRegion.LeftLeg).Skeletal;

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(skeletal.Broken, Is.True);
            Assert.That(skeletal.Splinted, Is.True);
        });
    }

    [Test]
    public void CastStartsBoneKnittingWithoutImmediatelyDeletingFracture()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var transaction = new MedicalTransaction(BodyRegion.RightLeg);
        transaction.Add(MedicalEffect.SetSkeletalState(
            BodyRegion.RightLeg,
            broken: true,
            splinted: false));
        HumanMedicalLedger.ApplyTransaction(medical, transaction);

        var result = HumanTreatmentSystem.TryApplyTreatment(
            medical,
            new TreatmentAttempt(
                TreatmentKind.Cast,
                BodyRegion.RightLeg,
                Amount: FixedPoint2.New(300)));
        var skeletal = HumanMedicalLedger.GetRegion(medical, BodyRegion.RightLeg).Skeletal;

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(result.DirtyFlags.HasFlag(MedicalDirtyFlags.Skeletal), Is.True);
            Assert.That(skeletal.Broken, Is.True);
            Assert.That(skeletal.Splinted, Is.True);
            Assert.That(skeletal.Knitting, Is.True);
            Assert.That(skeletal.KnittingSecondsRemaining, Is.EqualTo(FixedPoint2.New(300)));
        });
    }

    [Test]
    public void ClampSuppressesInternalBleedSource()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        AddBleed(medical, BodyRegion.Chest, BleedKind.Internal, FixedPoint2.New(1));
        var bleedId = medical.BleedSources[0].Id;

        var result = HumanTreatmentSystem.TryApplyTreatment(
            medical,
            new TreatmentAttempt(TreatmentKind.ClampBleed, BodyRegion.Chest, BleedSourceId: bleedId));
        var bleed = medical.BleedSources[0];

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(bleed.Active, Is.False);
            Assert.That(bleed.Rate, Is.EqualTo(FixedPoint2.New(1)));
            Assert.That(bleed.Treatment.HasFlag(TreatmentFlags.Clamped), Is.True);
        });
    }

    [Test]
    public void TemporaryBleedSuppressionStopsExternalAndInternalBleedsWithoutClosingThem()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        AddBleed(medical, BodyRegion.LeftLeg, BleedKind.External, FixedPoint2.New(2));
        AddBleed(medical, BodyRegion.LeftLeg, BleedKind.Internal, FixedPoint2.New(1));

        var result = HumanTreatmentSystem.TryApplyTreatment(
            medical,
            new TreatmentAttempt(TreatmentKind.TemporaryBleedSuppression, BodyRegion.LeftLeg));
        var external = medical.BleedSources[0];
        var internalBleed = medical.BleedSources[1];

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(external.Active, Is.False);
            Assert.That(internalBleed.Active, Is.False);
            Assert.That(external.Rate, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(internalBleed.Rate, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(external.Treatment.HasFlag(TreatmentFlags.TemporarilySuppressed), Is.True);
            Assert.That(internalBleed.Treatment.HasFlag(TreatmentFlags.TemporarilySuppressed), Is.True);
            Assert.That(external.Treatment.HasFlag(TreatmentFlags.Closed), Is.False);
            Assert.That(internalBleed.Treatment.HasFlag(TreatmentFlags.Closed), Is.False);
            Assert.That(internalBleed.Treatment.HasFlag(TreatmentFlags.Sutured), Is.False);
        });
    }

    [Test]
    public void TourniquetSuppressesDistalSurfaceBleedsUntilRemoved()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        AddBleed(medical, BodyRegion.LeftArm, BleedKind.External, FixedPoint2.New(1));
        AddBleed(medical, BodyRegion.LeftHand, BleedKind.Stump, FixedPoint2.New(2));

        var apply = HumanTreatmentSystem.TryApplyTreatment(
            medical,
            new TreatmentAttempt(
                TreatmentKind.ApplyTourniquet,
                BodyRegion.LeftArm,
                Amount: FixedPoint2.New(300)));
        var arm = HumanMedicalLedger.GetRegion(medical, BodyRegion.LeftArm);

        Assert.Multiple(() =>
        {
            Assert.That(apply.Applied, Is.True);
            Assert.That(arm.Tourniquet.Applied, Is.True);
            Assert.That(arm.Tourniquet.NecrosisSecondsRemaining, Is.EqualTo(FixedPoint2.New(300)));
            Assert.That(medical.BleedSources[0].Active, Is.False);
            Assert.That(medical.BleedSources[1].Active, Is.False);
            Assert.That(medical.BleedSources[0].Treatment.HasFlag(TreatmentFlags.Tourniquetted), Is.True);
            Assert.That(medical.BleedSources[1].Treatment.HasFlag(TreatmentFlags.Tourniquetted), Is.True);
        });

        var remove = HumanTreatmentSystem.TryApplyTreatment(
            medical,
            new TreatmentAttempt(TreatmentKind.RemoveTourniquet, BodyRegion.LeftArm));
        arm = HumanMedicalLedger.GetRegion(medical, BodyRegion.LeftArm);

        Assert.Multiple(() =>
        {
            Assert.That(remove.Applied, Is.True);
            Assert.That(arm.Tourniquet.Applied, Is.False);
            Assert.That(medical.BleedSources[0].Active, Is.True);
            Assert.That(medical.BleedSources[1].Active, Is.True);
            Assert.That(medical.BleedSources[0].Treatment.HasFlag(TreatmentFlags.Tourniquetted), Is.False);
            Assert.That(medical.BleedSources[1].Treatment.HasFlag(TreatmentFlags.Tourniquetted), Is.False);
        });
    }

    [Test]
    public void SutureClosesOpenStumpInjuryAndLinkedBleed()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var transaction = LimbLossRules.CreateTraumaticSeverance(
            BodyRegion.RightArm,
            BleedKind.Stump,
            FixedPoint2.New(4));
        HumanMedicalLedger.ApplyTransaction(medical, transaction);
        var stumpId = medical.Injuries[0].Id;
        var bleedId = medical.BleedSources[0].Id;

        var result = HumanTreatmentSystem.TryApplyTreatment(
            medical,
            new TreatmentAttempt(
                TreatmentKind.Suture,
                BodyRegion.RightArm,
                InjuryId: stumpId,
                BleedSourceId: bleedId));
        var stump = medical.Injuries[0];
        var bleed = medical.BleedSources[0];

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(stump.IsOpenStump, Is.False);
            Assert.That(stump.Flags.HasFlag(InjuryFlags.Sutured), Is.True);
            Assert.That(stump.Flags.HasFlag(InjuryFlags.Closed), Is.True);
            Assert.That(bleed.Active, Is.False);
            Assert.That(bleed.Treatment.HasFlag(TreatmentFlags.Sutured), Is.True);
        });
    }

    [Test]
    public void OrganRepairReducesDamageWithoutDeletingMissingOrganState()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var transaction = new MedicalTransaction(BodyRegion.Chest);
        transaction.Add(MedicalEffect.AddOrganDamage(
            OrganSlot.Heart,
            FixedPoint2.New(30)));
        HumanMedicalLedger.ApplyTransaction(medical, transaction);
        var heart = medical.Organs[(int) OrganSlot.Heart];
        heart.Flags |= OrganFlags.Missing;
        medical.Organs[(int) OrganSlot.Heart] = heart;

        var result = HumanTreatmentSystem.TryApplyTreatment(
            medical,
            new TreatmentAttempt(
                TreatmentKind.RepairOrgan,
                BodyRegion.Chest,
                OrganSlot.Heart));
        var repaired = HumanMedicalLedger.GetOrgan(medical, OrganSlot.Heart);

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(repaired.Damage, Is.LessThan(FixedPoint2.New(30)));
            Assert.That(repaired.Missing, Is.True);
        });
    }

    [Test]
    public void OrganRepairUsesAttemptAmountWhenProvided()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var transaction = new MedicalTransaction(BodyRegion.Chest);
        transaction.Add(MedicalEffect.AddOrganDamage(
            OrganSlot.Liver,
            FixedPoint2.New(30)));
        HumanMedicalLedger.ApplyTransaction(medical, transaction);

        var result = HumanTreatmentSystem.TryApplyTreatment(
            medical,
            new TreatmentAttempt(
                TreatmentKind.RepairOrgan,
                BodyRegion.Chest,
                OrganSlot.Liver,
                Amount: FixedPoint2.New(4)));
        var repaired = HumanMedicalLedger.GetOrgan(medical, OrganSlot.Liver);

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(repaired.Damage, Is.EqualTo(FixedPoint2.New(26)));
            Assert.That(repaired.Status, Is.EqualTo(OrganDamageStatus.Bruised));
        });
    }

    [Test]
    public void ImpossibleTreatmentFailsAndLeavesLedgerUnchanged()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var revision = medical.Revision;
        var before = HumanMedicalLedger.GetRegion(medical, BodyRegion.LeftArm).Skeletal;

        var result = HumanTreatmentSystem.TryApplyTreatment(
            medical,
            new TreatmentAttempt(TreatmentKind.Splint, BodyRegion.LeftArm));
        var after = HumanMedicalLedger.GetRegion(medical, BodyRegion.LeftArm).Skeletal;

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.False);
            Assert.That(result.FailureReason, Is.Not.Empty);
            Assert.That(medical.Revision, Is.EqualTo(revision));
            Assert.That(after.Flags, Is.EqualTo(before.Flags));
        });
    }

    [Test]
    public void TreatmentRulesArePureAndSystemOwnsMutation()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        AddBleed(medical, BodyRegion.Chest, BleedKind.External, FixedPoint2.New(2));
        var before = medical.Revision;

        var result = TreatmentRules.TryCreateTreatmentPlan(
            medical,
            new TreatmentAttempt(
                TreatmentKind.Gauze,
                BodyRegion.Chest,
                BleedSourceId: medical.BleedSources[0].Id));

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(medical.Revision, Is.EqualTo(before));
            Assert.That(medical.BleedSources[0].Active, Is.True);
        });
    }

    private static void AddBleed(
        HumanMedicalComponent medical,
        BodyRegion region,
        BleedKind kind,
        FixedPoint2 rate)
    {
        var transaction = new MedicalTransaction(region);
        transaction.Add(MedicalEffect.AddBleedSource(region, kind, rate));
        HumanMedicalLedger.ApplyTransaction(medical, transaction);
    }
}
