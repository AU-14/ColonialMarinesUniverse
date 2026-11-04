using Content.Shared._RMC14.Xenonids.Screech;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Xenonids;

[TestFixture]
public sealed class CMUReportedScreechTest
{
    [Test]
    public async Task ScatterFollowsCurrentHolderAndExpiresWithoutAffectingNextHolder()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var hands = entities.System<SharedHandsSystem>();
            var user = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            var next = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            var firstGun = entities.SpawnEntity("CMWeaponPistolM1984", map.GridCoords);
            var secondGun = entities.SpawnEntity("CMWeaponPistolM1984", map.GridCoords);
            Assert.That(hands.TryPickupAnyHand(user, firstGun, checkActionBlocker: false), Is.True);
            var baseline = entities.GetComponent<GunComponent>(firstGun).MinAngleModified;
            entities.AddComponent<ScreechBlindComponent>(user).EndsAt =
                pair.Server.ResolveDependency<IGameTiming>().CurTime + TimeSpan.FromSeconds(5);
            Assert.That(entities.GetComponent<GunComponent>(firstGun).MinAngleModified.Degrees,
                Is.EqualTo(baseline.Degrees + 45).Within(0.01));

            Assert.That(hands.TryDrop(user, firstGun, checkActionBlocker: false), Is.True);
            Assert.That(hands.TryPickupAnyHand(next, firstGun, checkActionBlocker: false), Is.True);
            Assert.That(entities.GetComponent<GunComponent>(firstGun).MinAngleModified, Is.EqualTo(baseline));
            Assert.That(hands.TryPickupAnyHand(user, secondGun, checkActionBlocker: false), Is.True);
            Assert.That(entities.GetComponent<GunComponent>(secondGun).MinAngleModified.Degrees,
                Is.EqualTo(baseline.Degrees + 45).Within(0.01), "A newly held gun must inherit the holder's screech penalty.");

            entities.AddComponent<ScreechBlindComponent>(next).EndsAt =
                pair.Server.ResolveDependency<IGameTiming>().CurTime + TimeSpan.FromSeconds(5);
            entities.RemoveComponent<ScreechBlindComponent>(user);
            Assert.That(entities.GetComponent<GunComponent>(secondGun).MinAngleModified, Is.EqualTo(baseline));
            Assert.That(entities.GetComponent<GunComponent>(firstGun).MinAngleModified.Degrees,
                Is.EqualTo(baseline.Degrees + 45).Within(0.01), "The former holder's expiry must not clear the new holder's penalty.");
            entities.RemoveComponent<ScreechBlindComponent>(next);
            Assert.That(entities.GetComponent<GunComponent>(firstGun).MinAngleModified, Is.EqualTo(baseline));
        });
        await pair.CleanReturnAsync();
    }
}
