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
}
