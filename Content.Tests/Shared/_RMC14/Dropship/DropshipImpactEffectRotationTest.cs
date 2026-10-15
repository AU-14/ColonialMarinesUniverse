using Content.Shared._RMC14.Dropship.Weapon;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Shared._RMC14.Dropship;

[TestFixture]
public sealed class DropshipImpactEffectRotationTest
{
    [Test]
    public void OccluderImpactEffectsRoundRandomRotationToCardinal()
    {
        var rotation = SharedDropshipWeaponSystem.GetImpactEffectRotation(
            Angle.FromDegrees(45),
            hasOccluder: true);

        Assert.That(rotation.GetDir(), Is.EqualTo(Direction.East));
    }

    [Test]
    public void NonOccluderImpactEffectsKeepRandomRotation()
    {
        var rotation = SharedDropshipWeaponSystem.GetImpactEffectRotation(
            Angle.FromDegrees(45),
            hasOccluder: false);

        Assert.That(rotation, Is.EqualTo(Angle.FromDegrees(45)));
    }

    [TestCase(1, 3, true)]
    [TestCase(0, 3, false)]
    [TestCase(3, 4, true)]
    [TestCase(2, 4, false)]
    [TestCase(1, 2, true)]
    public void CeilingLevelPenetrationChecksUseCorrectThresholds(int zLevelPenetration, short ceilingLevel, bool expected)
    {
        Assert.That(
            SharedDropshipWeaponSystem.CanHitCeilingLevel(zLevelPenetration, ceilingLevel),
            Is.EqualTo(expected));
    }
}
