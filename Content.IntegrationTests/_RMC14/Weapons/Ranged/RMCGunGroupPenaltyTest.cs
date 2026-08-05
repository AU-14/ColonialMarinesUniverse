#nullable enable
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._RMC14.Weapons.Ranged;

[TestFixture, TestOf(typeof(RMCGunGroupPenaltySystem))]
public sealed class RMCGunGroupPenaltyTest
{
    [Test]
    public async Task DroppingGunDoesNotRefreshNonGunHeldItem()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var handsSystem = server.System<SharedHandsSystem>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var user = entMan.SpawnEntity(null, map.GridCoords);
            var hands = entMan.EnsureComponent<HandsComponent>(user);
            handsSystem.AddHand((user, hands), "gun", HandLocation.Right);
            handsSystem.AddHand((user, hands), "magazine", HandLocation.Left);

            var gun = entMan.SpawnEntity("RMCWeaponRifleM54C", map.GridCoords);
            var magazine = entMan.SpawnEntity("CMMagazineRifleM54C", map.GridCoords);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<GunComponent>(gun), Is.True);
                Assert.That(entMan.HasComponent<GunGroupPenaltyComponent>(gun), Is.True);
                Assert.That(entMan.HasComponent<GunComponent>(magazine), Is.False);
                Assert.That(handsSystem.TryPickup(user, gun, "gun"), Is.True);
                Assert.That(handsSystem.TryPickup(user, magazine, "magazine"), Is.True);
            });

            Assert.That(handsSystem.TryDrop(user, gun), Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
