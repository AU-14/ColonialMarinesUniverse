using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._CMU14.Medical.Human.Effects;
using Content.Shared.FixedPoint;
using NUnit.Framework;

namespace Content.Tests.Shared._CMU14.Medical.Human;

[TestFixture]
public sealed class HumanPainShockSystemTest
{
    [Test]
    public void LedgerPainSourcesIncludeBrokenBonesWoundsAndInternalBleeding()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var leg = medical.Regions[(int) BodyRegion.LeftLeg];
        leg.Skeletal.Broken = true;
        medical.Regions[(int) BodyRegion.LeftLeg] = leg;

        medical.Injuries.Add(new InjuryRecord
        {
            Id = 1,
            Region = BodyRegion.LeftLeg,
            Kind = InjuryKind.Cut,
            Stage = InjuryStage.Gaping,
            Damage = FixedPoint2.New(30),
        });

        medical.BleedSources.Add(new BleedSource
        {
            Id = 1,
            Region = BodyRegion.LeftLeg,
            Kind = BleedKind.Internal,
            Rate = FixedPoint2.New(0.3),
        });

        var source = SharedPainShockSystem.CalculateLedgerPainSourceProfile(medical);

        Assert.Multiple(() =>
        {
            Assert.That(source.Target, Is.GreaterThan(FixedPoint2.New(45)));
            Assert.That(source.RiseRate, Is.GreaterThan(FixedPoint2.Zero));
        });
    }

    [Test]
    public void ClampedInternalBleedingDoesNotAddPainSource()
    {
        var active = HumanMedicalLedger.CreateDefault();
        active.BleedSources.Add(new BleedSource
        {
            Id = 1,
            Region = BodyRegion.Chest,
            Kind = BleedKind.Internal,
            Rate = FixedPoint2.New(0.3),
        });

        var clamped = HumanMedicalLedger.CreateDefault();
        clamped.BleedSources.Add(new BleedSource
        {
            Id = 1,
            Region = BodyRegion.Chest,
            Kind = BleedKind.Internal,
            Rate = FixedPoint2.New(0.3),
            Treatment = TreatmentFlags.Clamped,
        });

        Assert.That(
            SharedPainShockSystem.CalculateLedgerPainSourceProfile(clamped).Target,
            Is.LessThan(SharedPainShockSystem.CalculateLedgerPainSourceProfile(active).Target));
    }

    [Test]
    public void DamagedVitalOrgansContributePainFromLedgerSlots()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var heart = medical.Organs[(int) OrganSlot.Heart];
        heart.Status = OrganDamageStatus.Broken;
        heart.Damage = FixedPoint2.New(45);
        medical.Organs[(int) OrganSlot.Heart] = heart;

        var source = SharedPainShockSystem.CalculateLedgerPainSourceProfile(medical);

        Assert.That(source.Target, Is.GreaterThanOrEqualTo(FixedPoint2.New(60)));
    }
}
