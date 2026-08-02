using Content.Server._CMU14.Yautja;
using Content.Shared.Physics;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaLeapTest
{
    [Test]
    public void LeapPassesMobsAndObjectsButKeepsWallsBlocking()
    {
        var original = (int) (CollisionGroup.Impassable |
                              CollisionGroup.MidImpassable |
                              CollisionGroup.HighImpassable |
                              CollisionGroup.LowImpassable |
                              CollisionGroup.BulletImpassable);

        var actual = YautjaAbilitySystem.GetLeapCollisionMask(original);

        Assert.Multiple(() =>
        {
            Assert.That(actual & (int) CollisionGroup.Impassable, Is.Not.Zero,
                "Walls must remain impassable during a Yautja leap.");
            Assert.That(actual & (int) CollisionGroup.MidImpassable, Is.Zero);
            Assert.That(actual & (int) CollisionGroup.HighImpassable, Is.Zero);
            Assert.That(actual & (int) CollisionGroup.LowImpassable, Is.Zero);
            Assert.That(actual & (int) CollisionGroup.BulletImpassable, Is.Not.Zero,
                "Unrelated collision groups must not be removed by the leap.");
        });
    }
}
