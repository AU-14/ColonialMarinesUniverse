using Content.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests.Tests.Weapons.Ranged;

[TestFixture]
public sealed class EnergyShotgunHoloCollisionTest
{
    private static readonly string[] HologramPrototypes =
    {
        "MobHoloparasiteGuardian",
        "MobCarpHolo",
    };

    [Test]
    public async Task LethalLaserPelletsCollideWithHolograms()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var projectile = entMan.SpawnEntity("BulletLaser", map.GridCoords);
            var projectileFixture = entMan
                .GetComponent<FixturesComponent>(projectile)
                .Fixtures["projectile"];
            var requiredMask = (int) (CollisionGroup.Opaque |
                                      CollisionGroup.Impassable |
                                      CollisionGroup.BulletImpassable);

            Assert.That(
                projectileFixture.CollisionMask & requiredMask,
                Is.EqualTo(requiredMask),
                "BulletLaser must retain wall and bullet blocking while adding hologram collisions.");

            foreach (var prototype in HologramPrototypes)
            {
                var hologram = entMan.SpawnEntity(prototype, map.GridCoords);
                var opaqueLayer = (int) CollisionGroup.Opaque;
                var hologramFixture = entMan
                    .GetComponent<FixturesComponent>(hologram)
                    .Fixtures.Values
                    .First(fixture => (fixture.CollisionLayer & opaqueLayer) != 0);

                Assert.That(
                    projectileFixture.CollisionMask & hologramFixture.CollisionLayer,
                    Is.Not.EqualTo(0),
                    $"BulletLaser must collide with {prototype}'s opaque fixture.");
            }
        });

        await pair.CleanReturnAsync();
    }
}
