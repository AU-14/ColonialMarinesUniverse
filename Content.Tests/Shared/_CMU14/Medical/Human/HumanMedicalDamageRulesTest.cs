using System.Linq;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Rules;
using Content.Shared.FixedPoint;
using NUnit.Framework;

namespace Content.Tests.Shared._CMU14.Medical.Human;

[TestFixture]
public sealed class HumanMedicalDamageRulesTest
{
    [Test]
    public void SlashDamageCreatesCutFractureOrganAndInternalBleedEffects()
    {
        var transaction = MedicalDamageRules.CreateDamageTransaction(
            BodyRegion.Chest,
            FixedPoint2.New(35),
            FixedPoint2.Zero,
            new MedicalDamageContext(
                InjuryKind.Cut,
                BoneContact: true,
                OrganContact: true,
                VascularContact: true,
                OrganSlot.LeftLung,
                OrganDamageScale: 0.25f,
                InternalBleedRate: FixedPoint2.New(0.3)),
            new MedicalRngContext(BoneRoll: 0.5f));

        var effects = transaction.Effects.ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(effects.Any(effect => effect.Kind == MedicalEffectKind.AddRegionDamage), Is.True);
            Assert.That(effects.Any(effect => effect.Kind == MedicalEffectKind.AddInjury &&
                                             effect.InjuryKind == InjuryKind.Cut &&
                                             effect.InjuryStage == InjuryStage.Flesh), Is.True);
            Assert.That(effects.Any(effect => effect.Kind == MedicalEffectKind.SetSkeletalState &&
                                             effect.Broken), Is.True);
            Assert.That(effects.Any(effect => effect.Kind == MedicalEffectKind.AddOrganDamage &&
                                             effect.OrganSlot == OrganSlot.LeftLung), Is.True);
            Assert.That(effects.Any(effect => effect.Kind == MedicalEffectKind.AddBleedSource &&
                                             effect.BleedKind == BleedKind.Internal), Is.True);
        });
    }

    [Test]
    public void BurnDamageCreatesBurnInjuryAndRegionDamage()
    {
        var transaction = MedicalDamageRules.CreateDamageTransaction(
            BodyRegion.LeftArm,
            FixedPoint2.Zero,
            FixedPoint2.New(30),
            new MedicalDamageContext(InjuryKind.Burn),
            new MedicalRngContext());

        var effects = transaction.Effects.ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(effects.Any(effect => effect.Kind == MedicalEffectKind.AddRegionDamage &&
                                             effect.BurnDamage == FixedPoint2.New(30)), Is.True);
            Assert.That(effects.Any(effect => effect.Kind == MedicalEffectKind.AddInjury &&
                                             effect.InjuryKind == InjuryKind.Burn &&
                                             effect.InjuryStage == InjuryStage.Severe), Is.True);
        });
    }

    [Test]
    public void NoneRegionCreatesEmptyTransaction()
    {
        var transaction = MedicalDamageRules.CreateDamageTransaction(
            BodyRegion.None,
            FixedPoint2.New(50),
            FixedPoint2.New(50),
            new MedicalDamageContext(
                InjuryKind.Cut,
                BoneContact: true,
                OrganContact: true,
                VascularContact: true,
                OrganSlot.Heart,
                OrganDamageScale: 1f,
                InternalBleedRate: FixedPoint2.New(1)),
            new MedicalRngContext(BoneRoll: 0f));

        Assert.That(transaction.Count, Is.Zero);
    }

    [Test]
    public void SmallDamageDoesNotCreateNoiseInjuries()
    {
        var transaction = MedicalDamageRules.CreateDamageTransaction(
            BodyRegion.RightHand,
            FixedPoint2.New(4),
            FixedPoint2.Zero,
            new MedicalDamageContext(InjuryKind.Bruise),
            new MedicalRngContext());

        var effects = transaction.Effects.ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(effects.Any(effect => effect.Kind == MedicalEffectKind.AddRegionDamage), Is.True);
            Assert.That(effects.Any(effect => effect.Kind == MedicalEffectKind.AddInjury), Is.False);
        });
    }

    [Test]
    public void BoneContactUsesRngBeforeAddingFractureEffect()
    {
        var transaction = MedicalDamageRules.CreateDamageTransaction(
            BodyRegion.LeftArm,
            FixedPoint2.New(5),
            FixedPoint2.Zero,
            new MedicalDamageContext(
                InjuryKind.Bruise,
                BoneContact: true),
            new MedicalRngContext(BoneRoll: 0.99f));

        Assert.That(transaction.Effects.ToArray().Any(effect => effect.Kind == MedicalEffectKind.SetSkeletalState), Is.False);
    }
}
