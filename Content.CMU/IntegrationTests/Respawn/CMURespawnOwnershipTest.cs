using System.Linq;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Station.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Respawn;

[TestFixture]
public sealed class CMURespawnOwnershipTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task PendingRespawnCannotDeleteAReplacementOrRevivedBody(bool revive)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var session = server.PlayerMan.Sessions.Single();
        EntityUid survivor = default;
        EntityUid rule = default;
        await server.WaitAssertion(() =>
        {
            var station = entities.SpawnEntity(null, map.GridCoords);
            entities.AddComponent<StationDataComponent>(station);
            var ticker = entities.System<GameTicker>();
            Assert.That(ticker.StartGameRule("RespawnDeadRule", out rule), Is.True);
            var tracker = entities.GetComponent<RespawnTrackerComponent>(rule);
#pragma warning disable RA0002 // Configure the real rule for a short destructive-respawn regression.
            tracker.DeleteBody = true;
            tracker.RespawnDelay = TimeSpan.FromSeconds(1);
#pragma warning restore RA0002
            var body = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            var minds = entities.System<MindSystem>();
            var mind = minds.CreateMind(session.UserId, "Respawn regression");
            minds.TransferTo(mind, body);
            minds.SetUserId(mind, session.UserId);
            var mobs = entities.System<MobStateSystem>();
            mobs.ChangeMobState(body, MobState.Dead);
            Assert.That(tracker.RespawnQueue, Does.ContainKey(session.UserId));

            if (revive)
            {
                mobs.ChangeMobState(body, MobState.Alive);
                survivor = body;
            }
            else
            {
                survivor = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                minds.TransferTo(mind, survivor);
            }
        });
        await pair.RunTicksSync(pair.SecondsToTicks(2));
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.EntityExists(survivor), Is.True);
            Assert.That(session.AttachedEntity, Is.EqualTo(survivor));
            Assert.That(entities.GetComponent<RespawnTrackerComponent>(rule).RespawnQueue, Is.Empty);
        });
        await pair.CleanReturnAsync();
    }
}
