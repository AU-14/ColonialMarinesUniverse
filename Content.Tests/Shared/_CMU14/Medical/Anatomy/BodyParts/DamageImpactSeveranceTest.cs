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
