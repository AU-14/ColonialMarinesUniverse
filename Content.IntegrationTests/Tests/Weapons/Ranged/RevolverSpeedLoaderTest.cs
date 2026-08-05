using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.IntegrationTests.Tests.Weapons.Ranged;

[TestFixture, TestOf(typeof(GunSystem))]
public sealed class RevolverSpeedLoaderTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: CartridgeMagnum
          id: TestCartridgeMagnumSpentSpeedLoader
          components:
          - type: CartridgeAmmo
            proto: BulletMagnum
            spent: true

        - type: entity
          parent: SpeedLoaderMagnum
          id: TestSpeedLoaderMagnumSpent
          components:
          - type: BallisticAmmoProvider
            proto: TestCartridgeMagnumSpentSpeedLoader
            capacity: 1
        """;

    [Test]
    public async Task SpentCartridgeStaysSpentWhenLoadedFromSpeedLoader()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var gun = server.System<GunSystem>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var revolver = entMan.SpawnEntity("WeaponRevolverPirateEmpty", map.GridCoords);
            var speedLoader = entMan.SpawnEntity("TestSpeedLoaderMagnumSpent", map.GridCoords);
            var revolverAmmo = entMan.GetComponent<RevolverAmmoProviderComponent>(revolver);
            var speedLoaderAmmo = entMan.GetComponent<BallisticAmmoProviderComponent>(speedLoader);

            Assert.That(speedLoaderAmmo.Count, Is.EqualTo(1));
            Assert.That(gun.TryRevolverInsert((revolver, revolverAmmo), speedLoader, null), Is.True);
            Assert.That(speedLoaderAmmo.Count, Is.Zero);

            var loadedAmmo = revolverAmmo.AmmoSlots[0];
            Assert.That(loadedAmmo, Is.Not.Null);
            Assert.That(loadedAmmo.Value, Is.Not.EqualTo(speedLoader));
            Assert.That(revolverAmmo.AmmoContainer.ContainedEntities, Does.Contain(loadedAmmo.Value));
            Assert.That(revolverAmmo.Chambers[0], Is.False);
            Assert.That(entMan.GetComponent<CartridgeAmmoComponent>(loadedAmmo.Value).Spent, Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
