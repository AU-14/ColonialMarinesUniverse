using Content.IntegrationTests.Fixtures;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.IntegrationTests.Tests.Weapons;

[TestFixture]
[TestOf(typeof(SharedGunSystem))]
public sealed class SpentCartridgeLoadingTest : GameTest
{
    [Test]
    public async Task SpentShotgunShellCannotBeLoadedAgain()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var gunSystem = entMan.System<SharedGunSystem>();
            var shotgun = entMan.SpawnEntity("WeaponShotgunM42A2", map.GridCoords);
            var spentShell = entMan.SpawnEntity("CMShellShotgunBuckshot1", map.GridCoords);
            var liveShell = entMan.SpawnEntity("CMShellShotgunBuckshot1", map.GridCoords);
            var provider = entMan.GetComponent<BallisticAmmoProviderComponent>(shotgun);
            var cartridge = entMan.GetComponent<CartridgeAmmoComponent>(spentShell);

            cartridge.Spent = true;

            Assert.Multiple(() =>
            {
                Assert.That(gunSystem.CanInsertBallistic((shotgun, provider), spentShell), Is.False,
                    "a fired shell must not be accepted by a shotgun or another ballistic ammo holder");
                Assert.That(gunSystem.CanInsertBallistic((shotgun, provider), liveShell), Is.True,
                    "rejecting fired shells must not prevent loading unused shells");
            });
        });
    }
}
