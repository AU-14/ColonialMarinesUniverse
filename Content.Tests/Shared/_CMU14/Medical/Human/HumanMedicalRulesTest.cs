using System.Linq;
using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Rules;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._CMU14.Medical.Human.Effects;
using Content.Shared.FixedPoint;
using NUnit.Framework;

namespace Content.Tests.Shared._CMU14.Medical.Human;

[TestFixture]
public sealed class HumanMedicalRulesTest
{
    [TestCase(0, InjuryStage.None)]
    [TestCase(14, InjuryStage.Small)]
    [TestCase(15, InjuryStage.Deep)]
    [TestCase(24, InjuryStage.Deep)]
    [TestCase(25, InjuryStage.Flesh)]
    [TestCase(49, InjuryStage.Flesh)]
    [TestCase(50, InjuryStage.Gaping)]
    [TestCase(59, InjuryStage.Gaping)]
    [TestCase(60, InjuryStage.GapingBig)]
    [TestCase(69, InjuryStage.GapingBig)]
    [TestCase(70, InjuryStage.Massive)]
    public void CutWoundStagesUseCm13Thresholds(int damage, InjuryStage expected)
    {
        Assert.That(InjuryRules.GetStage(InjuryKind.Cut, FixedPoint2.New(damage)), Is.EqualTo(expected));
    }

    [TestCase(0, InjuryStage.None)]
    [TestCase(4, InjuryStage.None)]
    [TestCase(5, InjuryStage.Tiny)]
    [TestCase(9, InjuryStage.Tiny)]
    [TestCase(10, InjuryStage.Small)]
    [TestCase(19, InjuryStage.Small)]
    [TestCase(20, InjuryStage.Moderate)]
    [TestCase(29, InjuryStage.Moderate)]
    [TestCase(30, InjuryStage.Large)]
    [TestCase(49, InjuryStage.Large)]
    [TestCase(50, InjuryStage.Huge)]
    [TestCase(79, InjuryStage.Huge)]
    [TestCase(80, InjuryStage.Monumental)]
    public void BruiseWoundStagesUseCm13Thresholds(int damage, InjuryStage expected)
    {
        Assert.That(InjuryRules.GetStage(InjuryKind.Bruise, FixedPoint2.New(damage)), Is.EqualTo(expected));
    }

    [TestCase(0, InjuryStage.None)]
    [TestCase(14, InjuryStage.Moderate)]
    [TestCase(15, InjuryStage.Large)]
    [TestCase(29, InjuryStage.Large)]
    [TestCase(30, InjuryStage.Severe)]
    [TestCase(39, InjuryStage.Severe)]
    [TestCase(40, InjuryStage.Deep)]
    [TestCase(49, InjuryStage.Deep)]
    [TestCase(50, InjuryStage.Carbonised)]
    public void BurnWoundStagesUseCm13Thresholds(int damage, InjuryStage expected)
    {
        Assert.That(InjuryRules.GetStage(InjuryKind.Burn, FixedPoint2.New(damage)), Is.EqualTo(expected));
    }

    [TestCase(0, OrganDamageStatus.None)]
    [TestCase(1, OrganDamageStatus.LittleBruised)]
    [TestCase(9, OrganDamageStatus.LittleBruised)]
    [TestCase(10, OrganDamageStatus.Bruised)]
    [TestCase(29, OrganDamageStatus.Bruised)]
    [TestCase(30, OrganDamageStatus.Broken)]
    public void OrganDamageUsesCm13Thresholds(int damage, OrganDamageStatus expected)
    {
        Assert.That(OrganRules.GetStatus(FixedPoint2.New(damage)), Is.EqualTo(expected));
    }

    [Test]
    public void FractureRollUsesCm13DamageMultiplier()
    {
        var rollDamage = SkeletalRules.GetFractureRollDamage(FixedPoint2.New(20));

        Assert.Multiple(() =>
        {
            Assert.That(rollDamage, Is.EqualTo(FixedPoint2.New(60)));
            Assert.That(SkeletalRules.GetFractureChancePercent(rollDamage), Is.EqualTo(120));
        });
    }

    [Test]
    public void FractureEvaluationUsesDeterministicRngInput()
    {
        var input = new SkeletalRuleInput(
            BodyRegion.LeftArm,
            FixedPoint2.New(5),
            BoneContact: true,
            AlreadyBroken: false,
            Splinted: false);

        var success = SkeletalRules.EvaluateFracture(input, new MedicalRngContext(BoneRoll: 0.29f));
        var failure = SkeletalRules.EvaluateFracture(input, new MedicalRngContext(BoneRoll: 0.30f));

        Assert.Multiple(() =>
        {
            Assert.That(success.ShouldBreak, Is.True);
            Assert.That(success.ChancePercent, Is.EqualTo(30));
            Assert.That(failure.ShouldBreak, Is.False);
            Assert.That(failure.ChancePercent, Is.EqualTo(30));
        });
    }

    [Test]
    public void FractureSeverityUsesHairlineSimpleCompoundAndShatteredNames()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SkeletalRules.GetSeverity(FixedPoint2.New(10)), Is.EqualTo(FractureSeverity.Hairline));
            Assert.That(SkeletalRules.GetSeverity(FixedPoint2.New(45)), Is.EqualTo(FractureSeverity.Simple));
            Assert.That(SkeletalRules.GetSeverity(FixedPoint2.New(95)), Is.EqualTo(FractureSeverity.Compound));
            Assert.That(SkeletalRules.GetSeverity(FixedPoint2.New(125)), Is.EqualTo(FractureSeverity.Shattered));
        });
    }

    [Test]
    public void FractureEvaluationRejectsMissingBoneContactAndAlreadyBrokenRegions()
    {
        var noContact = new SkeletalRuleInput(
            BodyRegion.LeftArm,
            FixedPoint2.New(50),
            BoneContact: false,
            AlreadyBroken: false,
            Splinted: false);
        var alreadyBroken = new SkeletalRuleInput(
            BodyRegion.LeftArm,
            FixedPoint2.New(50),
            BoneContact: true,
            AlreadyBroken: true,
            Splinted: false);

        Assert.Multiple(() =>
        {
            Assert.That(SkeletalRules.EvaluateFracture(noContact, new MedicalRngContext(BoneRoll: 0f)).ShouldBreak, Is.False);
            Assert.That(SkeletalRules.EvaluateFracture(alreadyBroken, new MedicalRngContext(BoneRoll: 0f)).ShouldBreak, Is.False);
        });
    }

    [TestCase(19, PainTier.None)]
    [TestCase(20, PainTier.Mild)]
    [TestCase(29, PainTier.Mild)]
    [TestCase(30, PainTier.Discomforting)]
    [TestCase(39, PainTier.Discomforting)]
    [TestCase(40, PainTier.Moderate)]
    [TestCase(59, PainTier.Moderate)]
    [TestCase(60, PainTier.Distressing)]
    [TestCase(69, PainTier.Distressing)]
    [TestCase(70, PainTier.Severe)]
    [TestCase(79, PainTier.Severe)]
    [TestCase(80, PainTier.Horrible)]
    public void PainTiersUseCm13Thresholds(int pain, PainTier expected)
    {
        Assert.That(PainTierThresholds.Get(PainTier.None, FixedPoint2.New(pain)), Is.EqualTo(expected));
    }

    [Test]
    public void EmbeddedObjectMovementDamageDefaultsToHalfPerFragment()
    {
        var input = new EmbeddedObjectMovementInput(Count: 3);

        Assert.That(
            HumanMovementDebuffRules.GetEmbeddedObjectMovementDamage(input),
            Is.EqualTo(FixedPoint2.New(1.5)));
    }

    [TestCase(2, 2)]
    [TestCase(0.5, 0.5)]
    [TestCase(0.6, 0.6)]
    [TestCase(3, 3)]
    public void EmbeddedObjectMovementDamageSupportsCm13FixedOverrides(double configuredDamage, double expected)
    {
        var input = new EmbeddedObjectMovementInput(
            Count: 4,
            MoveDamage: FixedPoint2.New(configuredDamage));

        Assert.That(
            HumanMovementDebuffRules.GetEmbeddedObjectMovementDamage(input),
            Is.EqualTo(FixedPoint2.New(expected)));
    }

    [Test]
    public void SplintBreakChanceUsesFreshNonBurnDamage()
    {
        var result = HumanMovementDebuffRules.EvaluateSplintBreak(
            new SplintBreakInput(
                BodyRegion.LeftLeg,
                NonBurnDamage: FixedPoint2.New(6),
                Splinted: true),
            roll: 0.64f);

        Assert.Multiple(() =>
        {
            Assert.That(result.ShouldBreak, Is.True);
            Assert.That(result.ChancePercent, Is.EqualTo(65));
        });
    }

    [Test]
    public void SplintBreakIgnoresBurnDamageAndSmallHits()
    {
        var burnOnly = HumanMovementDebuffRules.EvaluateSplintBreak(
            new SplintBreakInput(
                BodyRegion.LeftLeg,
                NonBurnDamage: FixedPoint2.Zero,
                Splinted: true),
            roll: 0f);
        var tooSmall = HumanMovementDebuffRules.EvaluateSplintBreak(
            new SplintBreakInput(
                BodyRegion.LeftLeg,
                NonBurnDamage: FixedPoint2.New(5),
                Splinted: true),
            roll: 0f);

        Assert.Multiple(() =>
        {
            Assert.That(burnOnly.ShouldBreak, Is.False);
            Assert.That(tooSmall.ShouldBreak, Is.False);
        });
    }

    [Test]
    public void UnsplintedCoreFractureMovementCanDamageOrgans()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var chest = medical.Regions[(int) BodyRegion.Chest];
        chest.Skeletal.Broken = true;
        medical.Regions[(int) BodyRegion.Chest] = chest;

        var result = HumanMovementDebuffRules.EvaluateUnsplintedFractureMovement(
            medical,
            new HumanMovementDebuffRng(
                OrganDamageRoll: 0f,
                OrganDamageAmountRoll: 0.5f,
                OrganSlotRoll: 0f,
                InternalBleedRoll: 1f,
                CrippledLegsRoll: 1f));
        var effects = result.Transaction?.Effects.ToArray() ?? [];

        Assert.Multiple(() =>
        {
            Assert.That(result.BonesMoved, Is.True);
            Assert.That(result.BonesMovedRegion, Is.EqualTo(BodyRegion.Chest));
            Assert.That(effects.Any(effect => effect.Kind == MedicalEffectKind.AddOrganDamage &&
                                             effect.OrganDamage == FixedPoint2.New(4)), Is.True);
        });
    }

    [Test]
    public void UnsplintedFractureMovementCanCreateInternalBleeding()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var arm = medical.Regions[(int) BodyRegion.LeftArm];
        arm.Skeletal.Broken = true;
        medical.Regions[(int) BodyRegion.LeftArm] = arm;

        var result = HumanMovementDebuffRules.EvaluateUnsplintedFractureMovement(
            medical,
            new HumanMovementDebuffRng(
                OrganDamageRoll: 1f,
                InternalBleedRoll: 0f,
                InternalBleedAmountRoll: 0f,
                BrokenRegionRoll: 0f,
                CrippledLegsRoll: 1f));
        var effects = result.Transaction?.Effects.ToArray() ?? [];

        Assert.Multiple(() =>
        {
            Assert.That(result.BonesCut, Is.True);
            Assert.That(result.BonesCutRegion, Is.EqualTo(BodyRegion.LeftArm));
            Assert.That(effects.Any(effect => effect.Kind == MedicalEffectKind.AddInjury &&
                                             effect.InjuryKind == InjuryKind.InternalBleed &&
                                             effect.InjuryDamage == FixedPoint2.New(3)), Is.True);
            Assert.That(effects.Any(effect => effect.Kind == MedicalEffectKind.AddBleedSource &&
                                             effect.BleedKind == BleedKind.Internal), Is.True);
        });
    }

    [Test]
    public void BothCrippledLegSidesCanDropPatient()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var leftLeg = medical.Regions[(int) BodyRegion.LeftLeg];
        leftLeg.Skeletal.Broken = true;
        medical.Regions[(int) BodyRegion.LeftLeg] = leftLeg;
        var rightFoot = medical.Regions[(int) BodyRegion.RightFoot];
        rightFoot.Presence = LimbPresence.Missing;
        medical.Regions[(int) BodyRegion.RightFoot] = rightFoot;

        var result = HumanMovementDebuffRules.EvaluateUnsplintedFractureMovement(
            medical,
            new HumanMovementDebuffRng(
                OrganDamageRoll: 1f,
                InternalBleedRoll: 1f,
                CrippledLegsRoll: 0f));

        Assert.That(result.CrippledLegsShouldDrop, Is.True);
    }

    [Test]
    public void ProtectedRegionsRejectTraumaticSeverance()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LimbLossRules.CanTraumaticallySever(BodyRegion.Head), Is.True);
            Assert.That(LimbLossRules.CanTraumaticallySever(BodyRegion.Chest), Is.False);
            Assert.That(LimbLossRules.CanTraumaticallySever(BodyRegion.LeftArm), Is.True);
            Assert.That(LimbLossRules.CanTraumaticallySever(BodyRegion.RightLeg), Is.True);
        });
    }
}
