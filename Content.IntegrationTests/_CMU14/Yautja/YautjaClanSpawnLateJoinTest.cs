#nullable enable
using System.Numerics;
using Content.Server.GameTicking;
using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Content.Shared.GameTicking;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaClanSpawnLateJoinTest
{
    private const string ColonyAdministrator = "AU14JobCivilianColonyAdministrator";

    // the clan marker used to be a LateJoin spawn point, and the generic late join code takes
    // those for any job. a colony administrator ended up on the hunter ship that way
    [Test]
    public async Task LateJoinColonistNeverSpawnsOnHunterShipClanMarker()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = false, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var transform = entities.System<SharedTransformSystem>();
            Assert.That(entities.System<GameTicker>().RunLevel, Is.EqualTo(GameRunLevel.InRound),
                "late join spawn rules only apply in round");

            // late joins take any fitting job marker first, so clear the round's own markers
            // or the LateJoin branch never runs and the test proves nothing
            var markers = entities.EntityQueryEnumerator<SpawnPointComponent>();
            while (markers.MoveNext(out var uid, out _))
            {
                entities.DeleteEntity(uid);
            }

            var clan = entities.SpawnEntity("CMUHunterShipMarkerClanSpawn", map.GridCoords);
            var clanSpot = transform.ToMapCoordinates(entities.GetComponent<TransformComponent>(clan).Coordinates);

            var colonyLateJoin = entities.SpawnEntity(null, map.GridCoords.Offset(new Vector2(3, 0)));
            entities.AddComponent<SpawnPointComponent>(colonyLateJoin).SpawnType = SpawnPointType.LateJoin;

            for (var i = 0; i < 40; i++)
            {
                var ev = new PlayerSpawningEvent(ColonyAdministrator, null, null);
                entities.EventBus.RaiseEvent(EventSource.Local, ev);
                Assert.That(ev.SpawnResult, Is.Not.Null);

                var spot = transform.ToMapCoordinates(entities.GetComponent<TransformComponent>(ev.SpawnResult!.Value).Coordinates);
                Assert.That(spot, Is.Not.EqualTo(clanSpot),
                    $"a late joining colony administrator spawned on the hunter ship clan marker (try {i})");

                entities.DeleteEntity(ev.SpawnResult.Value);
            }
        });

        await pair.CleanReturnAsync();
    }
}
