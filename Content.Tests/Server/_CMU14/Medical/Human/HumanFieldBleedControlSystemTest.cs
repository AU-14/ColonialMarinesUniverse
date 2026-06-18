using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._CMU14.Medical.Human.Care;
using Content.Shared.FixedPoint;
using NUnit.Framework;

namespace Content.Tests.Server._CMU14.Medical.Human;

[TestFixture]
public sealed class HumanFieldBleedControlSystemTest
{
    [Test]
    public void BasicGauzeRefusesArterialBleeds()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        AddBleed(
            medical,
            BodyRegion.LeftArm,
            BleedKind.External,
            FixedPoint2.New(4),
            BleedFlags.Arterial);

        var created = MedicalBleedControlRules.TryCreateBleedControlAttempt(
            medical,
            BodyRegion.LeftArm,
            stopsArterialBleeding: false,
            out var attempt,
            out var blockedByArterial);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.False);
            Assert.That(blockedByArterial, Is.True);
            Assert.That(attempt, Is.EqualTo(default(TreatmentAttempt)));
        });
    }

    [Test]
    public void TraumaDressingTargetsArterialBleeds()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        AddBleed(
            medical,
            BodyRegion.LeftArm,
            BleedKind.External,
            FixedPoint2.New(4),
            BleedFlags.Arterial);

        var created = MedicalBleedControlRules.TryCreateBleedControlAttempt(
            medical,
            BodyRegion.LeftArm,
            stopsArterialBleeding: true,
            out var attempt,
            out var blockedByArterial);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.True);
            Assert.That(blockedByArterial, Is.False);
            Assert.That(attempt.Kind, Is.EqualTo(TreatmentKind.Gauze));
            Assert.That(attempt.Region, Is.EqualTo(BodyRegion.LeftArm));
            Assert.That(attempt.BleedSourceId, Is.EqualTo(medical.BleedSources[0].Id));
        });
    }

    [Test]
    public void BasicGauzeDoesNotFallbackToDifferentRegion()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        AddBleed(
            medical,
            BodyRegion.LeftArm,
            BleedKind.External,
            FixedPoint2.New(4),
            BleedFlags.Arterial);
        AddBleed(
            medical,
            BodyRegion.RightLeg,
            BleedKind.External,
            FixedPoint2.New(1),
            BleedFlags.None);

        var created = MedicalBleedControlRules.TryCreateBleedControlAttempt(
            medical,
            BodyRegion.LeftArm,
            stopsArterialBleeding: false,
            out var attempt,
            out var blockedByArterial);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.False);
            Assert.That(blockedByArterial, Is.True);
            Assert.That(attempt, Is.EqualTo(default(TreatmentAttempt)));
        });
    }

    [Test]
    public void GauzeTargetsUntreatedCutWhenNoBleedSourceExists()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var transaction = new MedicalTransaction(BodyRegion.RightArm);
        transaction.Add(MedicalEffect.AddInjury(
            BodyRegion.RightArm,
            InjuryKind.Cut,
            InjuryStage.Deep,
            FixedPoint2.New(12)));
        HumanMedicalLedger.ApplyTransaction(medical, transaction);

        var created = MedicalBleedControlRules.TryCreateBleedControlAttempt(
            medical,
            BodyRegion.RightArm,
            stopsArterialBleeding: false,
            out var attempt,
            out var blockedByArterial);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.True);
            Assert.That(blockedByArterial, Is.False);
            Assert.That(attempt.Kind, Is.EqualTo(TreatmentKind.Gauze));
            Assert.That(attempt.Region, Is.EqualTo(BodyRegion.RightArm));
            Assert.That(attempt.InjuryId, Is.EqualTo(medical.Injuries[0].Id));
            Assert.That(attempt.BleedSourceId, Is.EqualTo(0));
        });
    }

    private static void AddBleed(
        HumanMedicalComponent medical,
        BodyRegion region,
        BleedKind kind,
        FixedPoint2 rate,
        BleedFlags flags)
    {
        var transaction = new MedicalTransaction(region);
        transaction.Add(MedicalEffect.AddBleedSource(region, kind, rate, flags: flags));
        HumanMedicalLedger.ApplyTransaction(medical, transaction);
    }
}
