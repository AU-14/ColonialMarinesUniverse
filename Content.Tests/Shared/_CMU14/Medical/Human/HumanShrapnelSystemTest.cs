using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._CMU14.Medical.Human.Damage.Shrapnel;
using NUnit.Framework;

namespace Content.Tests.Shared._CMU14.Medical.Human;

[TestFixture]
public sealed class HumanShrapnelSystemTest
{
    [Test]
    public void BrokenLedgerLegAddsMovementPainPulse()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var leg = medical.Regions[(int) BodyRegion.LeftLeg];
        leg.Skeletal.Broken = true;
        medical.Regions[(int) BodyRegion.LeftLeg] = leg;

        var pulse = SharedCMUShrapnelSystem.ComputeLedgerMovementPainPulse(medical);

        Assert.That(pulse, Is.GreaterThan(0f));
    }

    [Test]
    public void SplintedLedgerLegAddsLessMovementPainThanUnsplintedLeg()
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
            SharedCMUShrapnelSystem.ComputeLedgerMovementPainPulse(splinted),
            Is.LessThan(SharedCMUShrapnelSystem.ComputeLedgerMovementPainPulse(unsplinted)));
    }
}
