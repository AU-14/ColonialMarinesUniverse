using Content.Shared.Physics;
using Robust.Shared.Physics;

namespace Content.IntegrationTests.Tests.Weapons.Ranged;

[TestFixture]
public sealed class EnergyProjectileHoloCollisionTest
{
    private static readonly string[] EnergyProjectilePrototypes =
    {
        "BulletLaser",
        "BulletTaser",
        "BulletDisabler",
        "BulletDisablerPractice",
        "BulletDisablerSmg",
        "WatcherBolt",
    };

    private static readonly string[] HologramPrototypes =
    {
        "MobHoloparasiteGuardian",
        "MobCarpHolo",
    };

    [Test]
    public async Task EnergyProjectilesCollideWithHolograms()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var opaqueLayer = (int) CollisionGroup.Opaque;
            var requiredMask = (int) (CollisionGroup.Opaque |
                                      CollisionGroup.Impassable |
                                      CollisionGroup.BulletImpassable);
            var hologramLayers = HologramPrototypes
                .Select(prototype =>
                {
                    var hologram = entMan.SpawnEntity(prototype, map.GridCoords);
                    var fixture = entMan
                        .GetComponent<FixturesComponent>(hologram)
                        .Fixtures.Values
                        .First(entry => (entry.CollisionLayer & opaqueLayer) != 0);
                    return (Prototype: prototype, fixture.CollisionLayer);
                })
                .ToArray();

            foreach (var prototype in EnergyProjectilePrototypes)
            {
                var projectile = entMan.SpawnEntity(prototype, map.GridCoords);
                var projectileFixture = entMan
                    .GetComponent<FixturesComponent>(projectile)
                    .Fixtures["projectile"];

                Assert.That(
                    projectileFixture.CollisionMask & opaqueLayer,
                    Is.EqualTo(opaqueLayer),
                    $"{prototype} must include the opaque hologram layer.");

                foreach (var hologram in hologramLayers)
                {
                    Assert.That(
                        projectileFixture.CollisionMask & hologram.CollisionLayer,
                        Is.Not.EqualTo(0),
                        $"{prototype} must collide with {hologram.Prototype}.");
                }

                if (prototype == "WatcherBolt")
                    continue;

                Assert.That(
                    projectileFixture.CollisionMask & requiredMask,
                    Is.EqualTo(requiredMask),
                    $"{prototype} must retain wall and bullet blocking while hitting holograms.");
            }
        });

        await pair.CleanReturnAsync();
    }
}
