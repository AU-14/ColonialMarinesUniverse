#nullable enable
using System.Linq;
using Content.Shared.Interaction;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Upgrades;
using Content.Shared.Weapons.Ranged.Upgrades.Components;

namespace Content.IntegrationTests.Tests.Weapons.Ranged;

[TestFixture, TestOf(typeof(GunUpgradeSystem))]
public sealed class GunUpgradeTest
{
    [Test]
    public async Task InsertedUpgradeImmediatelyRefreshesModifiers()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var upgradeSystem = server.System<GunUpgradeSystem>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var gun = entMan.SpawnEntity("WeaponProtoKineticAccelerator", map.GridCoords);
            var upgrade = entMan.SpawnEntity("PKAUpgradeFireRate", map.GridCoords);
            var gunComponent = entMan.GetComponent<GunComponent>(gun);
            var upgradeable = entMan.GetComponent<UpgradeableGunComponent>(gun);
            var fireRateUpgrade = entMan.GetComponent<GunUpgradeFireRateComponent>(upgrade);

            Assert.That(gunComponent.FireRateModified, Is.EqualTo(gunComponent.FireRate).Within(0.0001f));

            var ev = new AfterInteractUsingEvent(gun, upgrade, gun, map.GridCoords, canReach: true);
            entMan.EventBus.RaiseLocalEvent(gun, ev);

            Assert.Multiple(() =>
            {
                Assert.That(ev.Handled, Is.True);
                Assert.That(upgradeSystem.GetCurrentUpgrades((gun, upgradeable)).Any(ent => ent.Owner == upgrade), Is.True);
                Assert.That(
                    gunComponent.FireRateModified,
                    Is.EqualTo(gunComponent.FireRate * fireRateUpgrade.Coefficient).Within(0.0001f));
            });
        });

        await pair.CleanReturnAsync();
    }
}
