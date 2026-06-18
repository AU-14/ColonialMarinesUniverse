using Content.Shared._CMU14.Medical.Chemistry.Data;
using Content.Shared._CMU14.Medical.Chemistry.Rules;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared.FixedPoint;
using NUnit.Framework;

namespace Content.Tests.Shared._CMU14.Medical.Chemistry;

[TestFixture]
public sealed class HumanChemicalLedgerRulesTest
{
    [Test]
    public void BicaridineHealsHighestPriorityBruteRegionAndInjury()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        AddInjury(medical, BodyRegion.RightArm, InjuryKind.Bruise, FixedPoint2.New(12));
        AddInjury(medical, BodyRegion.Chest, InjuryKind.Cut, FixedPoint2.New(8));

        var plan = HumanChemicalLedgerRules.CreatePlan(
            medical,
            new HumanChemicalTick("CMBicaridine", FixedPoint2.New(1), FixedPoint2.New(5)));

        var result = HumanMedicalLedger.ApplyTransaction(medical, plan.Transaction);
        var chest = HumanMedicalLedger.GetRegion(medical, BodyRegion.Chest);
        var arm = HumanMedicalLedger.GetRegion(medical, BodyRegion.RightArm);

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(chest.BruteDamage, Is.EqualTo(FixedPoint2.New(7)));
            Assert.That(arm.BruteDamage, Is.EqualTo(FixedPoint2.New(12)));
            Assert.That(medical.Injuries[1].Damage, Is.EqualTo(FixedPoint2.New(7)));
        });
    }

    [Test]
    public void DermalineRespectsNecroticBurnHealingFloor()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        AddInjury(medical, BodyRegion.LeftArm, InjuryKind.Burn, FixedPoint2.New(6), InjuryFlags.Necrotic);

        var plan = HumanChemicalLedgerRules.CreatePlan(
            medical,
            new HumanChemicalTick("CMDermaline", FixedPoint2.New(1), FixedPoint2.New(5)));

        var result = HumanMedicalLedger.ApplyTransaction(medical, plan.Transaction);
        var arm = HumanMedicalLedger.GetRegion(medical, BodyRegion.LeftArm);

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(arm.BurnDamage, Is.EqualTo(FixedPoint2.New(5)));
            Assert.That(medical.Injuries[0].Damage, Is.EqualTo(FixedPoint2.New(5)));
        });
    }

    [Test]
    public void PeridaxonSuppressesOrganSymptomsWithoutRepairingDamage()
    {
        var medical = HumanMedicalLedger.CreateDefault();
        AddOrganDamage(medical, OrganSlot.Heart, FixedPoint2.New(30));

        var plan = HumanChemicalLedgerRules.CreatePlan(
            medical,
            new HumanChemicalTick("CMPeridaxon", FixedPoint2.New(1), FixedPoint2.New(5)));

        var result = HumanMedicalLedger.ApplyTransaction(medical, plan.Transaction);
        var heart = HumanMedicalLedger.GetOrgan(medical, OrganSlot.Heart);

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(heart.Damage, Is.EqualTo(FixedPoint2.New(30)));
            Assert.That(heart.Flags.HasFlag(OrganFlags.Stasis), Is.True);
        });
    }

    [Test]
    public void OxycodoneCriticalOverdoseDamagesBrainAndLiver()
    {
        var medical = HumanMedicalLedger.CreateDefault();

        var plan = HumanChemicalLedgerRules.CreatePlan(
            medical,
            new HumanChemicalTick("CMUOxycodone", FixedPoint2.New(1), FixedPoint2.New(30)));

        var result = HumanMedicalLedger.ApplyTransaction(medical, plan.Transaction);

        Assert.Multiple(() =>
        {
            Assert.That(result.Applied, Is.True);
            Assert.That(HumanMedicalLedger.GetOrgan(medical, OrganSlot.Brain).Damage, Is.EqualTo(FixedPoint2.New(4)));
            Assert.That(HumanMedicalLedger.GetOrgan(medical, OrganSlot.Liver).Damage, Is.EqualTo(FixedPoint2.New(12)));
        });
    }

    private static void AddInjury(
        HumanMedicalComponent medical,
        BodyRegion region,
        InjuryKind kind,
        FixedPoint2 damage,
        InjuryFlags flags = InjuryFlags.None)
    {
        var transaction = new MedicalTransaction(region);
        transaction.Add(MedicalEffect.AddRegionDamage(
            region,
            kind == InjuryKind.Burn ? FixedPoint2.Zero : damage,
            kind == InjuryKind.Burn ? damage : FixedPoint2.Zero));
        transaction.Add(MedicalEffect.AddInjury(
            region,
            kind,
            InjuryStage.Moderate,
            damage));
        HumanMedicalLedger.ApplyTransaction(medical, transaction);

        if (flags == InjuryFlags.None)
            return;

        var injury = medical.Injuries[^1];
        injury.Flags |= flags;
        medical.Injuries[^1] = injury;
    }

    private static void AddOrganDamage(
        HumanMedicalComponent medical,
        OrganSlot organ,
        FixedPoint2 damage)
    {
        var transaction = new MedicalTransaction(BodyRegion.Chest);
        transaction.Add(MedicalEffect.AddOrganDamage(organ, damage));
        HumanMedicalLedger.ApplyTransaction(medical, transaction);
    }
}
