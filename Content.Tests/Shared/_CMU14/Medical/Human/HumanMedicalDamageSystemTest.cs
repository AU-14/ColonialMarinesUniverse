using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._CMU14.Medical.Human.Damage;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using HumanOrganSlot = Content.Shared._CMU14.Medical.Human.Data.OrganSlot;

namespace Content.Tests.Shared._CMU14.Medical.Human;

[TestFixture]
public sealed class HumanMedicalDamageSystemTest
{
    [Test]
    public void DamageRequestMutatesResolvedLedgerRegion()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        var result = HumanMedicalDamageSystem.TryApplyDamageToLedger(
            medical,
            BodyRegion.LeftArm,
            Damage("Slash", 20),
            new CMUTraumaContactResult(
                CMUTraumaMechanism.Slash,
                CMUTraumaDepth.Bone,
                BoneContact: true,
                OrganContact: false,
                VascularContact: false,
                OrganPassThrough: 0f,
                InternalBleedRate: 0f,
                HighEnergy: false),
            new MedicalRngContext(BoneRoll: 0f));

        var region = HumanMedicalLedger.GetRegion(medical, BodyRegion.LeftArm);

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(region.BruteDamage, Is.EqualTo(FixedPoint2.New(20)));
            Assert.That(region.Skeletal.Broken, Is.True);
            Assert.That(medical.Injuries, Has.Some.Matches<InjuryRecord>(injury =>
                injury.Region == BodyRegion.LeftArm &&
                injury.Kind == InjuryKind.Cut));
        });
    }

    [Test]
    public void SlashDamageCreatesActiveExternalBleedSource()
    {
        var medical = HumanMedicalLedger.CreateDefault();

        var result = HumanMedicalDamageSystem.TryApplyDamageToLedger(
            medical,
            BodyRegion.RightArm,
            Damage("Slash", 20),
            CMUTraumaContactResult.SoftTissue(CMUTraumaMechanism.Slash),
            new MedicalRngContext());

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(medical.BleedSources, Has.Some.Matches<BleedSource>(bleed =>
                bleed.Region == BodyRegion.RightArm &&
                bleed.Kind == BleedKind.External &&
                bleed.Active &&
                bleed.Rate > FixedPoint2.Zero));
        });
    }

    [Test]
    public void PiercingDamageCreatesPunctureWoundAndBleeding()
    {
        var medical = HumanMedicalLedger.CreateDefault();

        var result = HumanMedicalDamageSystem.TryApplyDamageToLedger(
            medical,
            BodyRegion.RightArm,
            Damage("Piercing", 20),
            CMUTraumaContactResult.SoftTissue(CMUTraumaMechanism.Pierce),
            new MedicalRngContext());

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(medical.Injuries, Has.Some.Matches<InjuryRecord>(injury =>
                injury.Region == BodyRegion.RightArm &&
                injury.Kind == InjuryKind.Puncture));
            Assert.That(medical.BleedSources, Has.Some.Matches<BleedSource>(bleed =>
                bleed.Region == BodyRegion.RightArm &&
                bleed.Kind == BleedKind.External &&
                bleed.Active));
        });
    }

    [Test]
    public void ChestOrganDamageUsesRngSelectedEligibleOrgan()
    {
        var medical = HumanMedicalLedger.CreateDefault();

        var result = HumanMedicalDamageSystem.TryApplyDamageToLedger(
            medical,
            BodyRegion.Chest,
            Damage("Slash", 30),
            new CMUTraumaContactResult(
                CMUTraumaMechanism.Slash,
                CMUTraumaDepth.Deep,
                BoneContact: false,
                OrganContact: true,
                VascularContact: false,
                OrganPassThrough: 1f,
                InternalBleedRate: 0f,
                HighEnergy: false),
            new MedicalRngContext(OrganRoll: 0.99f));

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(HumanMedicalLedger.GetOrgan(medical, HumanOrganSlot.Stomach).Damage, Is.GreaterThan(FixedPoint2.Zero));
            Assert.That(HumanMedicalLedger.GetOrgan(medical, HumanOrganSlot.LeftLung).Damage, Is.EqualTo(FixedPoint2.Zero));
        });
    }

    [Test]
    public void HumanLedgerFilterRejectsMissingLedgerSynthsAndXenos()
    {
        Assert.Multiple(() =>
        {
            Assert.That(HumanMedicalDamageSystem.CanProcessBody(hasHumanLedger: true, isSynth: false, isXeno: false), Is.True);
            Assert.That(HumanMedicalDamageSystem.CanProcessBody(hasHumanLedger: false, isSynth: false, isXeno: false), Is.False);
            Assert.That(HumanMedicalDamageSystem.CanProcessBody(hasHumanLedger: true, isSynth: true, isXeno: false), Is.False);
            Assert.That(HumanMedicalDamageSystem.CanProcessBody(hasHumanLedger: true, isSynth: false, isXeno: true), Is.False);
        });
    }

    [Test]
    public void AnatomyRegionOverridesBodyPartFallback()
    {
        var anatomy = new AnatomyRegionComponent
        {
            Region = BodyRegion.RightHand,
        };

        Assert.Multiple(() =>
        {
            Assert.That(HumanMedicalDamageSystem.ResolveBodyRegion(BodyPartType.Arm, anatomy), Is.EqualTo(BodyRegion.RightHand));
            Assert.That(HumanMedicalDamageSystem.ResolveBodyRegion(BodyPartType.Head, null), Is.EqualTo(BodyRegion.Head));
            Assert.That(HumanMedicalDamageSystem.ResolveBodyRegion(BodyPartType.Torso, null), Is.EqualTo(BodyRegion.Chest));
        });
    }

    [Test]
    public void SynthAndXenoFilteredRequestsLeaveLedgerUnchanged()
    {
        var medical = HumanMedicalLedger.CreateDefault();

        var synthResult = HumanMedicalDamageSystem.TryApplyDamageToLedger(
            medical,
            BodyRegion.Chest,
            Damage("Slash", 20),
            CMUTraumaContactResult.SoftTissue(CMUTraumaMechanism.Slash),
            new MedicalRngContext(),
            isSynth: true);
        var xenoResult = HumanMedicalDamageSystem.TryApplyDamageToLedger(
            medical,
            BodyRegion.Chest,
            Damage("Slash", 20),
            CMUTraumaContactResult.SoftTissue(CMUTraumaMechanism.Slash),
            new MedicalRngContext(),
            isXeno: true);

        Assert.Multiple(() =>
        {
            Assert.That(synthResult.Applied, Is.False);
            Assert.That(xenoResult.Applied, Is.False);
            Assert.That(medical.Revision, Is.Zero);
        });
    }

    private static DamageSpecifier Damage(string type, int amount)
    {
        return new DamageSpecifier
        {
            DamageDict =
            {
                [type] = FixedPoint2.New(amount),
            },
        };
    }
}
