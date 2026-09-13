using Content.IntegrationTests.Fixtures;
using Content.Shared.Stacks;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.IntegrationTests.Tests.Weapons;

[TestFixture]
[TestOf(typeof(SharedGunSystem))]
public sealed class SpentCartridgeLoadingTest : GameTest
{
    [Test]
    public async Task SpawnedShotgunAmmoFiresAsSingleShell()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var shotgun = entMan.SpawnEntity("RMCWeaponShotgunM42A2Filled", map.GridCoords);
            var ammo = new List<(EntityUid? Entity, IShootable Shootable)>();
            var takeAmmo = new TakeAmmoEvent(1, ammo, map.GridCoords, null);

            entMan.EventBus.RaiseLocalEvent(shotgun, takeAmmo);

            Assert.That(ammo, Has.Count.EqualTo(1));
            var shell = ammo[0].Entity;
            Assert.That(shell, Is.Not.Null);
            Assert.That(entMan.GetComponent<StackComponent>(shell!.Value).Count, Is.EqualTo(1),
                "firing a shotgun must eject one spent shell rather than a full handful");
        });
    }

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
