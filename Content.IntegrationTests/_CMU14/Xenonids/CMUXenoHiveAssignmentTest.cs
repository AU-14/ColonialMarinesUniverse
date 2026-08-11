using Content.IntegrationTests.Fixtures;
using Content.Server.Ghost.Roles;
using Content.Shared._RMC14.Language.Components;
using Content.Shared._RMC14.Language.Prototypes;
using Content.Shared._RMC14.Xenonids.Hive;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

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
        var corruptedHive = EntityUid.Invalid;
        var queen = EntityUid.Invalid;
        var larva = EntityUid.Invalid;
        var hive = EntityUid.Invalid;

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityQueryEnumerator<HiveComponent>().MoveNext(out _, out _), Is.False);

            corruptedHive = SSpawnAtPosition("CMUCorruptedHive", map.GridCoords);
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
            var ghostRoles = SEntMan.System<GhostRoleSystem>().GhostRoles.Select(role => role.Owner);
            var english = new ProtoId<LanguagePrototype>("English");
            var queenLanguages = SEntMan.GetComponent<LanguageComponent>(queen).UnderstoodLanguages;
            var larvaLanguages = SEntMan.GetComponent<LanguageComponent>(larva).UnderstoodLanguages;

            Assert.Multiple(() =>
            {
                Assert.That(hive, Is.Not.EqualTo(corruptedHive));
                Assert.That(hiveSystem.GetHive(larva)?.Owner, Is.EqualTo(hive));
                Assert.That(queenLanguages.Contains(english), Is.False);
                Assert.That(larvaLanguages.Contains(english), Is.False);
                Assert.That(ghostRoles, Does.Not.Contain(queen));
                Assert.That(ghostRoles, Does.Not.Contain(larva));
            });
        });

        await Server.WaitPost(() =>
        {
            SDeleteNow(corruptedHive);
            SDeleteNow(queen);
            SDeleteNow(larva);
            SDeleteNow(hive);
        });
    }
}
