using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Xenonids.Hive;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._CMU14.Xenonids;

[TestFixture]
public sealed class CMUXenoHiveAssignmentTest : GameTest
{
    [Test]
    public async Task HivelessXenoDoesNotCreateHive()
    {
        var map = await Pair.CreateTestMap();
        var xeno = EntityUid.Invalid;

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityQueryEnumerator<HiveComponent>().MoveNext(out _, out _), Is.False);

            xeno = SSpawnAtPosition("CMUXenoLarvaHL", map.GridCoords);

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.EntityQueryEnumerator<HiveComponent>().MoveNext(out _, out _), Is.False);
                Assert.That(SEntMan.HasComponent<HiveMemberComponent>(xeno), Is.False);
            });
        });

        await Server.WaitPost(() => SDeleteNow(xeno));
    }

    [Test]
    public async Task ManuallySpawnedXenosCreateAndReuseHive()
    {
        var map = await Pair.CreateTestMap();
        var queen = EntityUid.Invalid;
        var larva = EntityUid.Invalid;
        var hive = EntityUid.Invalid;

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityQueryEnumerator<HiveComponent>().MoveNext(out _, out _), Is.False);

            queen = SSpawnAtPosition("CMXenoQueen", map.GridCoords);
            larva = SSpawnAtPosition("CMXenoLarva", map.GridCoords);
        });

        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            var hiveSystem = SEntMan.System<SharedXenoHiveSystem>();
            var queenHive = hiveSystem.GetHive(queen);

            Assert.That(queenHive, Is.Not.Null);
            hive = queenHive!.Value.Owner;
            Assert.That(hiveSystem.GetHive(larva)?.Owner, Is.EqualTo(hive));
        });

        await Server.WaitPost(() =>
        {
            SDeleteNow(queen);
            SDeleteNow(larva);
            SDeleteNow(hive);
        });
    }
}
