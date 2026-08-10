using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using NUnit.Framework;

namespace Content.Tests.Shared._CMU14.Medical.Anatomy.BodyParts;

[TestFixture]
public sealed class SeveranceDamageTest
{
    [TestCase("Piercing", 40, 1)]
    [TestCase("Blunt", 40, 6)]
    [TestCase("Slash", 40, 40)]
    [TestCase("Caustic", 40, 4)]
    [TestCase("Heat", 40, 0)]
    public void DefaultCoefficientsWeightDamageTypes(string type, int damageAmount, int expected)
    {
        var health = new BodyPartHealthComponent();
        var damage = Damage(type, damageAmount);

        var result = SharedBodyPartHealthSystem.CalculateSeveranceDamage(
            damage,
            health.SeveranceDamageCoefficients);

        Assert.That(result, Is.EqualTo(FixedPoint2.New(expected)));
    }

    [Test]
    public void SourceMultiplierOnlyScalesSeveranceDamage()
    {
        var health = new BodyPartHealthComponent();
        var damage = Damage("Piercing", 40);

        var result = SharedBodyPartHealthSystem.CalculateSeveranceDamage(
            damage,
            health.SeveranceDamageCoefficients,
            multiplier: 10f);

        Assert.That(result, Is.EqualTo(FixedPoint2.New(10)));
        Assert.That(damage.GetTotal(), Is.EqualTo(FixedPoint2.New(40)));
    }

    [Test]
    public void NegativeAndHealingDamageDoNotContribute()
    {
        var health = new BodyPartHealthComponent();
        var damage = new DamageSpecifier();
        damage.DamageDict["Piercing"] = FixedPoint2.New(-40);
        damage.DamageDict["Slash"] = FixedPoint2.New(10);

        var result = SharedBodyPartHealthSystem.CalculateSeveranceDamage(
            damage,
            health.SeveranceDamageCoefficients,
            multiplier: 0f);

        Assert.That(result, Is.EqualTo(FixedPoint2.Zero));
    }

    private static DamageSpecifier Damage(string type, int amount)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[type] = FixedPoint2.New(amount);
        return damage;
    }
}
