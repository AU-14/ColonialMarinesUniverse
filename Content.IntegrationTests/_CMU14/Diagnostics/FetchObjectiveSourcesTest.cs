using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Round.Objectives.Type;
using Content.Shared.CMU14.Round.Objectives.Components;
using Content.Shared.CMU14.Round.Objectives.Type;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class FetchObjectiveSourcesTest : GameTest
{
    [Test]
    public async Task FetchRequiresEnoughUnclaimedItemsOrUnusedMarkers()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var system = SEntMan.System<ObjFetchSystem>();
            var objective = SEntMan.SpawnEntity(null, map.GridCoords);
            var fetch = SEntMan.EnsureComponent<FetchObjectiveComponent>(objective);
            fetch.TargetPrototype = "CMPaper";
            fetch.SpawnCount = 2;
            fetch.FetchCount = 2;
            var item = SEntMan.SpawnEntity("CMPaper", map.GridCoords);
            var markerUid = SEntMan.SpawnEntity(null, map.GridCoords);
            var marker = SEntMan.EnsureComponent<CMUObjectiveMarkerComponent>(markerUid);
            marker.Generic = true;

            Assert.That(system.HasAvailableSources(objective, fetch), Is.True);
            marker.Used = true;
            Assert.That(system.HasAvailableSources(objective, fetch), Is.False);
            marker.Used = false;
            SEntMan.EnsureComponent<FetchItemComponent>(item).ObjectiveUid = objective;
            Assert.That(system.HasAvailableSources(objective, fetch), Is.False);
            fetch.FetchCount = 1;
            Assert.That(system.HasAvailableSources(objective, fetch), Is.True);
            fetch.SpawnCount = 0;
            Assert.That(system.HasAvailableSources(objective, fetch), Is.False);

            SEntMan.DeleteEntity(item);
            SEntMan.DeleteEntity(markerUid);
            SEntMan.DeleteEntity(objective);
        });
    }
}
