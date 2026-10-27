using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Xenonids.Hive;

namespace Content.IntegrationTests.CMU14.Xenomorphs;

[TestFixture]
public sealed class CMUActiveHiveTest : GameTest
{
    [Test]
    public async Task BankedLarvaeNeedUsableSpawnPoint()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var hives = SEntMan.System<SharedXenoHiveSystem>();
            var hive = SEntMan.SpawnEntity("CMXenoHive", map.GridCoords);
            var hiveEntity = (hive, SEntMan.GetComponent<HiveComponent>(hive));
            hives.ChangeBurrowedLarva(hiveEntity, 1);
            Assert.That(hives.CMUIsHiveActive(hiveEntity), Is.False, "An unreachable larva bank is not a playable hive.");

            var core = SEntMan.SpawnEntity("HiveCoreXeno", map.GridCoords);
            hives.SetHive(core, hive);
            Assert.That(hives.CMUIsHiveActive(hiveEntity), Is.True, "A usable core can revive a hive without living members.");

            SEntMan.EnsureComponent<BurrowedLarvaSpawnBlockedComponent>(core);
            Assert.That(hives.CMUIsHiveActive(hiveEntity), Is.False, "A blocked core must not advertise unavailable larvae.");
        });
    }
}
