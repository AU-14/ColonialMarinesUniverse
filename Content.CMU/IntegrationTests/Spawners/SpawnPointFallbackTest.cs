#nullable enable
using System.Collections.Generic;
using System.Numerics;
using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Spawners;

[TestFixture]
public sealed class SpawnPointFallbackTest
{
    private const string ColonyAdministrator = "AU14JobCivilianColonyAdministrator";

    // no marker for the job anywhere: the catch-all used to take whichever spawner the query
    // returned first, observer markers included, so everyone stacked on one tile
    [Test]
    public async Task JobWithoutMarkerFallsBackToRandomNonObserverSpawn()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var transform = entities.System<SharedTransformSystem>();

            var markers = entities.EntityQueryEnumerator<SpawnPointComponent>();
            while (markers.MoveNext(out var uid, out _))
            {
                entities.DeleteEntity(uid);
            }

            EntityUid Marker(Vector2 offset, SpawnPointType type, string? job)
            {
                var uid = entities.SpawnEntity(null, map.GridCoords.Offset(offset));
                var point = entities.AddComponent<SpawnPointComponent>(uid);
                point.SpawnType = type;
                point.Job = job;
                return uid;
            }

            var observer = Marker(new Vector2(0.5f, 0.5f), SpawnPointType.Observer, null);
            var first = Marker(new Vector2(3.5f, 0.5f), SpawnPointType.Job, "Passenger");
            var second = Marker(new Vector2(6.5f, 0.5f), SpawnPointType.Job, "Passenger");

            MapCoordinates Spot(EntityUid uid) =>
                transform.ToMapCoordinates(entities.GetComponent<TransformComponent>(uid).Coordinates);

            var observerSpot = Spot(observer);
            var used = new HashSet<MapCoordinates>();
            for (var i = 0; i < 40; i++)
            {
                var ev = new PlayerSpawningEvent(ColonyAdministrator, null, null);
                entities.EventBus.RaiseEvent(EventSource.Local, ev);
                Assert.That(ev.SpawnResult, Is.Not.Null, "the catch-all has to put them somewhere");

                var spot = Spot(ev.SpawnResult!.Value);
                Assert.That(spot, Is.Not.EqualTo(observerSpot), $"spawned on an observer marker (try {i})");
                used.Add(spot);
                entities.DeleteEntity(ev.SpawnResult.Value);
            }

            Assert.That(used, Is.EquivalentTo(new[] { Spot(first), Spot(second) }),
                "the catch-all should pick at random, not always the first spawner");
        });

        await pair.CleanReturnAsync();
    }
}
