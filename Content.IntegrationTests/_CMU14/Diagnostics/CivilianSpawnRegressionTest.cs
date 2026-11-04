#pragma warning disable RA0002 // Create the station configuration before component initialization.
using Content.Server.Spawners.Components;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class CivilianSpawnRegressionTest
{
    [TestCase("AU14JobColonyWorkingJoe")]
    [TestCase("AU14JobCivilianOrbitalArbiter")]
    [TestCase("AU14JobCivilianOrbitalLawyer")]
    public async Task CivilianUsesOwnJobMarkerWhenAssignedStationHasNone(string jobId)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var assignedStation = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            entities.AddComponent(assignedStation, new StationJobsComponent { SetupAvailableJobs = new() });
            var unrelated = entities.SpawnEntity(null, map.GridCoords);
            var unrelatedMarker = entities.AddComponent<SpawnPointComponent>(unrelated);
            unrelatedMarker.SpawnType = SpawnPointType.Job;
            unrelatedMarker.Job = "AU14JobCivilianColonist";
            var coordinates = map.GridCoords.Offset(new Vector2(2, 0));
            var ownMarker = entities.SpawnEntity(null, coordinates);
            var marker = entities.AddComponent<SpawnPointComponent>(ownMarker);
            marker.SpawnType = SpawnPointType.Job;
            marker.Job = new ProtoId<JobPrototype>(jobId);

            var ev = new PlayerSpawningEvent(marker.Job, null, assignedStation);
            entities.EventBus.RaiseEvent(EventSource.Local, ev);
            Assert.That(ev.SpawnResult, Is.Not.Null);
            var result = entities.GetComponent<TransformComponent>(ev.SpawnResult!.Value).Coordinates;
            var transform = entities.System<SharedTransformSystem>();
            Assert.That(transform.ToMapCoordinates(result), Is.EqualTo(transform.ToMapCoordinates(coordinates)),
                "The assigned warship must not hide the civilian's colony job marker.");
        });
        await pair.CleanReturnAsync();
    }
}
