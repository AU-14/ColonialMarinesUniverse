using Content.Server._CMU14.Spawners;
using Content.Server.GameTicking;
using Content.Server.Spawners.Components;
using Content.Shared.Roles;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests._CMU14.Spawners;

[TestFixture]
public sealed class CMURoundSpawnPointSnapshotSystemTest
{
    [Test]
    public async Task SpawnBatchEventsBuildValidateAndBoundSnapshotLifetime()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var snapshot = server.System<CMURoundSpawnPointSnapshotSystem>();
            Assert.That(snapshot.Active, Is.False);
            var point = server.EntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var spawnPoint = server.EntMan.AddComponent<SpawnPointComponent>(point);
            spawnPoint.Job = new ProtoId<JobPrototype>("CMUTestSnapshotJob");
            spawnPoint.SpawnType = SpawnPointType.Job;

            var started = new RoundStartPlayerSpawnBatchEvent();
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, ref started);
            Assert.That(snapshot.Active, Is.True);
            Assert.That(snapshot.ValidateCachedEntry(point), Is.True);

            spawnPoint.Job = new ProtoId<JobPrototype>("CMUTestChangedSnapshotJob");
            Assert.That(snapshot.ValidateCachedEntry(point), Is.False);
            Assert.That(snapshot.Active, Is.False);

            server.EntMan.EventBus.RaiseEvent(EventSource.Local, ref started);
            Assert.That(snapshot.Active, Is.True);

            var finished = new RoundStartPlayerSpawnBatchFinishedEvent();
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, ref finished);
            Assert.That(snapshot.Active, Is.False);
            server.EntMan.DeleteEntity(point);
        });

        await pair.CleanReturnAsync();
    }
}
