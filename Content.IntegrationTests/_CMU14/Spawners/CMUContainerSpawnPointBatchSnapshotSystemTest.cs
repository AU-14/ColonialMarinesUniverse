using Content.Server.GameTicking;
using Content.Server.Spawners.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using NUnit.Framework;
using Robust.Server.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests._CMU14.Spawners;

[TestFixture]
public sealed class CMUContainerSpawnPointBatchSnapshotSystemTest
{
    private static readonly ProtoId<JobPrototype> RequestedJob = "AU14JobCivilianPhysician";

    [Test]
    public async Task SnapshotSelectsRelevantBucketAndIgnoresUnrelatedStaleEntry()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var system = server.System<ContainerSpawnPointSystem>();
            Assert.That(system.CmuRoundStartSnapshotActive, Is.False);

            var irrelevant = server.EntMan.SpawnEntity("CMUTestContainerOtherJobSpawner", map.GridCoords);
            var selected = server.EntMan.SpawnEntity("CMUTestContainerRequestedJobSpawner", map.GridCoords);

            var started = new RoundStartPlayerSpawnBatchEvent();
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, ref started);
            Assert.That(system.CmuRoundStartSnapshotActive, Is.True);

            server.EntMan.DeleteEntity(irrelevant);
            var profile = new HumanoidCharacterProfile()
                .WithSpawnPriorityPreference(SpawnPriorityPreference.Cryosleep);
            var spawn = new PlayerSpawningEvent(RequestedJob, profile, station: null, adminSpawned: false);
            system.HandlePlayerSpawning(spawn);

            Assert.That(spawn.SpawnResult, Is.Not.Null);
            Assert.That(system.CmuRoundStartSnapshotActive, Is.True,
                "A stale marker in an unrelated job bucket must not invalidate the batch snapshot.");
            var container = server.System<ContainerSystem>().GetContainer(selected, "storage");
            Assert.That(container.ContainedEntities, Does.Contain(spawn.SpawnResult!.Value));

            server.EntMan.DeleteEntity(selected);
            var staleSelection = new PlayerSpawningEvent(RequestedJob, profile, station: null, adminSpawned: false);
            system.HandlePlayerSpawning(staleSelection);

            Assert.That(staleSelection.SpawnResult, Is.Null);
            Assert.That(system.CmuRoundStartSnapshotActive, Is.False,
                "A stale marker in the selected bucket must fall back to the live query.");

            var finished = new RoundStartPlayerSpawnBatchFinishedEvent();
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, ref finished);
            Assert.That(system.CmuRoundStartSnapshotActive, Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: CryogenicSleepUnitSpawner
  id: CMUTestContainerOtherJobSpawner
  components:
  - type: ContainerSpawnPoint
    containerId: storage
    job: AU14JobCivilianEngineer

- type: entity
  parent: CryogenicSleepUnitSpawner
  id: CMUTestContainerRequestedJobSpawner
  components:
  - type: ContainerSpawnPoint
    containerId: storage
    job: AU14JobCivilianPhysician
";
}
