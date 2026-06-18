using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._CMU14.Medical.Human.Effects;
using Content.Shared.FixedPoint;
using NUnit.Framework;

namespace Content.Tests.Shared._CMU14.Medical.Human;

[TestFixture]
public sealed class HumanMedicalPenaltySystemTest
{
    [Test]
    public void BrokenAndMissingLegRegionsReduceMovementFromLedger()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var leftLeg = medical.Regions[(int) BodyRegion.LeftLeg];
        leftLeg.Skeletal.Broken = true;
        medical.Regions[(int) BodyRegion.LeftLeg] = leftLeg;
        var rightFoot = medical.Regions[(int) BodyRegion.RightFoot];
        rightFoot.Presence = LimbPresence.Missing;
        medical.Regions[(int) BodyRegion.RightFoot] = rightFoot;

        var movement = SharedCMUMedicalSpeedSystem.CalculateLedgerMovementMultiplier(
            medical,
            PainTier.Moderate);

        Assert.That(movement, Is.GreaterThanOrEqualTo(0.45f));
    }

    [Test]
    public void PainMovementPenaltyIsLightEnoughToAvoidHeavyRubberbanding()
    {
        var medical = HumanMedicalLedger.CreateDefault();

        Assert.Multiple(() =>
        {
            Assert.That(
                SharedCMUMedicalSpeedSystem.CalculateLedgerMovementMultiplier(medical, PainTier.Moderate),
                Is.GreaterThanOrEqualTo(0.80f));
            Assert.That(
                SharedCMUMedicalSpeedSystem.CalculateLedgerMovementMultiplier(medical, PainTier.Severe),
                Is.GreaterThanOrEqualTo(0.70f));
        });
    }

    [Test]
    public void BrokenLegAndFootMovementPenaltyStaysModerate()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var leftLeg = medical.Regions[(int) BodyRegion.LeftLeg];
        leftLeg.Skeletal.Broken = true;
        medical.Regions[(int) BodyRegion.LeftLeg] = leftLeg;
        var leftFoot = medical.Regions[(int) BodyRegion.LeftFoot];
        leftFoot.Skeletal.Broken = true;
        medical.Regions[(int) BodyRegion.LeftFoot] = leftFoot;

        var movement = SharedCMUMedicalSpeedSystem.CalculateLedgerMovementMultiplier(
            medical,
            PainTier.None);

        Assert.That(movement, Is.GreaterThanOrEqualTo(0.75f));
    }

    [Test]
    public void SplintedBrokenLegIsLessPunishingThanUnsplintedBreak()
    {
        var unsplinted = HumanMedicalLedger.CreateDefault();
        var unsplintedLeg = unsplinted.Regions[(int) BodyRegion.LeftLeg];
        unsplintedLeg.Skeletal.Broken = true;
        unsplinted.Regions[(int) BodyRegion.LeftLeg] = unsplintedLeg;

        var splinted = HumanMedicalLedger.CreateDefault();
        var splintedLeg = splinted.Regions[(int) BodyRegion.LeftLeg];
        splintedLeg.Skeletal.Broken = true;
        splintedLeg.Skeletal.Splinted = true;
        splinted.Regions[(int) BodyRegion.LeftLeg] = splintedLeg;

        Assert.That(
            SharedCMUMedicalSpeedSystem.CalculateLedgerMovementMultiplier(splinted, PainTier.None),
            Is.GreaterThan(SharedCMUMedicalSpeedSystem.CalculateLedgerMovementMultiplier(unsplinted, PainTier.None)));
    }

    [Test]
    public void BrokenArmsAndDamagedEyesIncreaseAimSwayFromLedger()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var arm = medical.Regions[(int) BodyRegion.RightArm];
        arm.Skeletal.Broken = true;
        medical.Regions[(int) BodyRegion.RightArm] = arm;
        var eyes = medical.Organs[(int) OrganSlot.Eyes];
        eyes.Status = OrganDamageStatus.Broken;
        eyes.Damage = FixedPoint2.New(30);
        medical.Organs[(int) OrganSlot.Eyes] = eyes;

        var sway = SharedCMUMedicalSpeedSystem.CalculateLedgerAimSwayMultiplier(
            medical,
            PainTier.Severe);

        Assert.That(sway, Is.GreaterThan(1.45f));
    }

    [Test]
    public void BrainDamageAndPainSlowActionsFromLedger()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var brain = medical.Organs[(int) OrganSlot.Brain];
        brain.Status = OrganDamageStatus.Broken;
        brain.Damage = FixedPoint2.New(30);
        medical.Organs[(int) OrganSlot.Brain] = brain;

        var action = SharedCMUMedicalSpeedSystem.CalculateLedgerActionSpeedMultiplier(
            medical,
            PainTier.Severe);

        Assert.That(action, Is.GreaterThanOrEqualTo(2.0f));
    }
}
