using System;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using NUnit.Framework;

namespace Content.Tests.Shared.CMU14.Medical.Anatomy.BodyParts;

[TestFixture, TestOf(typeof(DamageImpactSeverance))]
public sealed class DamageImpactSeveranceTest
{
    private static readonly FixedPoint2 MajorLimbThreshold = FixedPoint2.New(170);

    [TestCase(35, DamageImpactEnergy.Low, 30)]
    [TestCase(40, DamageImpactEnergy.Medium, 15)]
    [TestCase(70, DamageImpactEnergy.High, 5)]
    public void BallisticTierMatchesRepresentativeHitCount(
        float piercing,
        DamageImpactEnergy energy,
        int expectedHits)
    {
        var damage = Damage(piercing: piercing);
        var impact = new DamageImpact(
            DamageImpactDelivery.Projectile,
            DamageImpactContact.Generic,
            DamageImpactPenetration.Medium,
            energy);

        var perHit = DamageImpactSeverance.Calculate(damage, impact);

        Assert.That(HitsToThreshold(perHit), Is.EqualTo(expectedHits));
    }

    [Test]
    public void PointBlankDoublesSeveranceWithoutChangingDamage()
    {
        var damage = Damage(piercing: 40);
        var normal = new DamageImpact(
            DamageImpactDelivery.Projectile,
            DamageImpactContact.Generic,
            DamageImpactPenetration.Medium,
            DamageImpactEnergy.Medium);
        var pointBlank = normal with { Context = DamageImpactContext.PointBlank };

        Assert.Multiple(() =>
        {
            Assert.That(DamageImpactSeverance.Calculate(damage, pointBlank),
                Is.EqualTo(DamageImpactSeverance.Calculate(damage, normal) * 2));
            Assert.That(damage.DamageDict["Piercing"], Is.EqualTo(FixedPoint2.New(40)));
        });
    }

    [Test]
    public void FullPointBlankBuckshotCanSeverMajorLimb()
    {
        var pellet = DamageImpactSeverance.Calculate(
            Damage(piercing: 65),
            new DamageImpact(
                DamageImpactDelivery.Projectile,
                DamageImpactContact.Fragment,
                DamageImpactPenetration.Medium,
                DamageImpactEnergy.Severe,
                DamageImpactContext.PointBlank));

        Assert.That(pellet * 4, Is.GreaterThanOrEqualTo(MajorLimbThreshold));
    }

    [Test]
    public void AxeOutpacesBluntStabAndThrownDamage()
    {
        var axe = DamageImpactSeverance.Calculate(
            Damage(blunt: 25, slash: 20),
            new DamageImpact(DamageImpactDelivery.Melee, DamageImpactContact.Slash, DamageImpactPenetration.Low, DamageImpactEnergy.High));
        var blunt = DamageImpactSeverance.Calculate(
            Damage(blunt: 45),
            new DamageImpact(DamageImpactDelivery.Melee, DamageImpactContact.Crush, DamageImpactPenetration.None, DamageImpactEnergy.High));
        var stab = DamageImpactSeverance.Calculate(
            Damage(piercing: 45),
            new DamageImpact(DamageImpactDelivery.Melee, DamageImpactContact.Stab, DamageImpactPenetration.Medium, DamageImpactEnergy.High));
        var thrownAxe = DamageImpactSeverance.Calculate(
            Damage(blunt: 25, slash: 20),
            new DamageImpact(DamageImpactDelivery.Thrown, DamageImpactContact.Slash, DamageImpactPenetration.Low, DamageImpactEnergy.High));

        Assert.Multiple(() =>
        {
            Assert.That(HitsToThreshold(axe), Is.InRange(5, 6));
            Assert.That(blunt, Is.LessThan(axe));
            Assert.That(stab, Is.LessThan(axe));
            Assert.That(FixedPoint2.Abs(thrownAxe - axe * 0.15f), Is.LessThanOrEqualTo(FixedPoint2.Epsilon));
        });
    }

    [Test]
    public void BurnDoesNotContributeAndCloseBlastOutpacesDistantBlast()
    {
        var impact = DamageImpact.Explosion;
        var close = DamageImpactSeverance.Calculate(Damage(blunt: 80, heat: 20), impact);
        var distant = DamageImpactSeverance.Calculate(Damage(blunt: 20, heat: 20), impact);
        var burnOnly = DamageImpactSeverance.Calculate(Damage(heat: 100), impact);

        Assert.Multiple(() =>
        {
            Assert.That(close, Is.GreaterThan(distant));
            Assert.That(burnOnly, Is.EqualTo(FixedPoint2.Zero));
        });
    }

    [Test]
    public void DamageTypeWeightsAreAppliedIndependently()
    {
        var impact = new DamageImpact(
            DamageImpactDelivery.Generic,
            DamageImpactContact.Generic,
            DamageImpactPenetration.Low,
            DamageImpactEnergy.Unspecified);

        Assert.Multiple(() =>
        {
            Assert.That(DamageImpactSeverance.Calculate(Damage(slash: 100), impact), Is.EqualTo(FixedPoint2.New(25)));
            Assert.That(DamageImpactSeverance.Calculate(Damage(piercing: 100), impact), Is.EqualTo(FixedPoint2.New(16.25f)));
            Assert.That(DamageImpactSeverance.Calculate(Damage(blunt: 100), impact), Is.EqualTo(FixedPoint2.New(6.25f)));
        });
    }

    [Test]
    public void AutomaticHeadSeveranceProtectsBallisticAndRevivableTargets()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SharedBodyPartHealthSystem.CanAutomaticallySeverHead(
                DamageImpact.ForProjectile(Damage(piercing: 200)), MobState.Alive, true, false), Is.False);
            Assert.That(SharedBodyPartHealthSystem.CanAutomaticallySeverHead(
                DamageImpact.MeleeSlash, MobState.Alive, true, false), Is.True);
            Assert.That(SharedBodyPartHealthSystem.CanAutomaticallySeverHead(
                DamageImpact.Explosion, MobState.Alive, true, false), Is.True);
            Assert.That(SharedBodyPartHealthSystem.CanAutomaticallySeverHead(
                DamageImpact.MeleeSlash, MobState.Critical, true, false), Is.False);
            Assert.That(SharedBodyPartHealthSystem.CanAutomaticallySeverHead(
                DamageImpact.Explosion, MobState.Dead, true, false), Is.False);
            Assert.That(SharedBodyPartHealthSystem.CanAutomaticallySeverHead(
                DamageImpact.Explosion, MobState.Dead, true, true), Is.True);
            Assert.That(SharedBodyPartHealthSystem.CanAutomaticallySeverHead(
                DamageImpact.Explosion, MobState.Dead, false, false), Is.True);
        });
    }

    private static int HitsToThreshold(FixedPoint2 perHit)
        => (int)System.Math.Ceiling(MajorLimbThreshold.Float() / perHit.Float());

    private static DamageSpecifier Damage(float blunt = 0, float piercing = 0, float slash = 0, float heat = 0)
    {
        var damage = new DamageSpecifier();
        if (blunt > 0)
            damage.DamageDict["Blunt"] = FixedPoint2.New(blunt);
        if (piercing > 0)
            damage.DamageDict["Piercing"] = FixedPoint2.New(piercing);
        if (slash > 0)
            damage.DamageDict["Slash"] = FixedPoint2.New(slash);
        if (heat > 0)
            damage.DamageDict["Heat"] = FixedPoint2.New(heat);
        return damage;
    }
}
