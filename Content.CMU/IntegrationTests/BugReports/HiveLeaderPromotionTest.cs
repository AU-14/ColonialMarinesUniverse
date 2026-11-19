using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.HiveLeader;
using Content.Shared._RMC14.Xenonids.Watch;

namespace Content.IntegrationTests.CMU14.BugReports;

[TestFixture]
public sealed class HiveLeaderPromotionTest : GameTest
{
    [Test]
    public async Task PromotionRejectsWatchedCoreAndAcceptsHiveMember()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var hive = SEntMan.SpawnEntity("CMUPathogenHive", map.GridCoords);
            var overmind = SEntMan.SpawnEntity("CMU14XenoOvermind", map.GridCoords);
            var core = SEntMan.SpawnEntity("CMU14PathogenBlightCore", map.GridCoords);
            var neo = SEntMan.SpawnEntity("CMU14XenoNeomorph", map.GridCoords);
            var hives = SEntMan.System<SharedXenoHiveSystem>();
            var watch = SEntMan.System<SharedXenoWatchSystem>();
            foreach (var member in new[] { overmind, core, neo })
                hives.SetHive(member, hive);

            watch.SetWatching(overmind, core);
            SEntMan.EventBus.RaiseLocalEvent(overmind, new HiveLeaderActionEvent());
            Assert.That(SEntMan.HasComponent<HiveLeaderComponent>(core), Is.False);
            watch.SetWatching(overmind, neo);
            SEntMan.EventBus.RaiseLocalEvent(overmind, new HiveLeaderActionEvent());
            Assert.That(SEntMan.HasComponent<HiveLeaderComponent>(neo), Is.True);
        });
    }
}
