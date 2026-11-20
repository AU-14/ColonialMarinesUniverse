using Content.IntegrationTests.Fixtures;
using Content.Shared.Mind;
using Content.Server._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.ManageHive;
using Content.Shared._RMC14.Xenonids.Watch;

namespace Content.IntegrationTests.CMU14.Xenomorphs;

public sealed class CMUOvermindDevolutionTest : GameTest
{
    [Test]
    public async Task OvermindCanDevolveOnlyItsOwnWatchedVenator()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var hives = Server.System<XenoHiveSystem>();
            var hive = hives.CreateHive("Pathogen", "CMUPathogenHive");
            var rival = hives.CreateHive("Rival", "CMUCorruptedHive");
            var leader = SSpawnAtPosition("CMU14XenoOvermind", map.GridCoords);
            var venator = SSpawnAtPosition("CMU14XenoVenator", map.GridCoords);
            hives.SetHive(leader, hive);
            hives.SetHive(venator, hive);
            var minds = Server.System<SharedMindSystem>();
            minds.TransferTo(minds.GetOrCreateMind(Pair.Player!.UserId).Owner, leader);
            var watch = Server.System<SharedXenoWatchSystem>();
            watch.Watch(leader, venator);
            Assert.That(watch.TryGetWatched(leader, out var watched), Is.True);
            Assert.That(watched, Is.EqualTo(venator));

            // The target can change hive while the management dialog is open.
            hives.SetHive(venator, rival);
            var command = new ManageHiveDevolveMessageEvent("CMU14XenoBlight", "caste change");
            SEntMan.EventBus.RaiseLocalEvent(leader, command);
            Assert.That(SEntMan.EntityExists(venator), Is.True);
            hives.SetHive(venator, hive);
            SEntMan.EventBus.RaiseLocalEvent(leader, command);
            Assert.That(SEntMan.EntityExists(venator), Is.False);
        });
    }
}
